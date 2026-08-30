# Wallow Production Deployment Guide

Wallow deploys as a set of Docker containers described by a single compose file,
`docker/docker-compose.production.yml`. The stack is self-contained — it runs the same way on
your laptop and on a server. Every deployment picks exactly **one edge profile**:

- **`--profile direct`** — the bundled Caddy ingress owns `:80`/`:443` on the host and
  terminates TLS. Use it when the server is directly reachable from the internet, or when you
  front the stack with your own reverse proxy (in which case skip both profiles and target the
  published `127.0.0.1` ports instead — see the [Reverse Proxy guide](reverse-proxy.md)).
- **`--profile pangolin`** — no host ports at all; the `newt` tunnel client connects out to a
  [Pangolin](https://pangolin.net) instance that terminates TLS, and the three host-based
  Pangolin resources (apex, `api.`, `auth.`) are declared by `pangolin.*` labels in the compose file.

```bash
pnpm secrets:prod                             # writes docker/.env.production with fresh secrets
cd docker
# then edit the values the script cannot invent — it lists them
docker compose -f docker-compose.production.yml --env-file .env.production --profile direct up --build
docker compose -f docker-compose.production.yml --env-file .env.production logs -f
```

This page covers the architecture, what happens on first boot, the environment variables the
compose file actually reads, how OIDC clients get registered, and how images are built and
promoted.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Routing Topologies](#2-routing-topologies)
3. [First Boot Sequence](#3-first-boot-sequence)
4. [Environment Variables](#4-environment-variables)
5. [OIDC Client Registration](#5-oidc-client-registration)
6. [Connecting External Clients](#6-connecting-external-clients)
7. [CI/CD Pipeline](#7-cicd-pipeline)
8. [Scaling](#8-scaling)

---

## 1. Architecture Overview

```
                        ┌───────────────────────────────────────────────┐
                        │  Your Server                                  │
                        │                                               │
  Users ──► HTTPS ──►   │  ┌─────────────────────────────────────┐      │
                        │  │  Your reverse proxy (TLS + routing)  │      │
                        │  │  /api  → :8080                       │      │
                        │  │  /auth → :8081                       │      │
                        │  │  /     → :8082                       │      │
                        │  └─────────────────────────────────────┘      │
                        │         │            │            │           │
                        │  ┌──────┴──┐  ┌──────┴──┐  ┌──────┴──┐        │
                        │  │ wallow  │  │ wallow  │  │ wallow  │        │
                        │  │ api     │  │ auth    │  │ web     │        │
                        │  │ :8080   │  │ :8080   │  │ :8080   │        │
                        │  └────┬────┘  └─────────┘  └─────────┘        │
                        │       │                                       │
                        │  ┌────┴────┐  ┌─────────┐  ┌──────────┐       │
                        │  │Postgres │  │ Valkey  │  │ GarageHQ │       │
                        │  │ :5432   │  │ :6379   │  │ (S3)     │       │
                        │  │+ replica│  │ (cache) │  └──────────┘       │
                        │  └─────────┘  └─────────┘                     │
                        └───────────────────────────────────────────────┘
```

Every application container listens on **8080 inside the network**; only the host-side port
differs (`API_PORT`, `AUTH_PORT`, `WEB_PORT`). Those host ports are bound to `127.0.0.1` for
local debugging — in direct mode the `caddy` ingress is the only externally reachable
container, and in pangolin mode nothing listens externally at all (newt only dials out).

**Long-running services:**

| Service | Image | Purpose |
|---------|-------|---------|
| `caddy` | `caddy:2-alpine` | Reference reverse-proxy ingress (`--profile direct`); owns `:80`/`:443` and routes `/api`, `/auth`, and the catch-all to the three apps (`docker/caddy/Caddyfile.example`) |
| `newt` | `fosrl/newt` | Pangolin tunnel client (`--profile pangolin`); reads the Docker socket and applies the `pangolin.*` blueprint labels on the three app services as the Pangolin resource |
| `wallow-api` | `ghcr.io/bc-solutions-coder/wallow` | REST API, OIDC provider (OpenIddict), SignalR hub, background jobs |
| `wallow-auth` | `ghcr.io/bc-solutions-coder/wallow-auth` | Node SSR login/register/MFA UI (`apps/wallow-auth`); also a pure reverse proxy for `/v1/**`, `/connect/**`, `/.well-known/**` |
| `wallow-web` | `ghcr.io/bc-solutions-coder/wallow-web` | Node SSR dashboard + BFF, a confidential OIDC client of the API (`apps/wallow-web`) |
| `postgres` | `postgres:18-alpine` | Primary database, one schema per module |
| `postgres-replica` | `ghcr.io/bc-solutions-coder/wallow-postgres-replica` | Streaming read replica (`ConnectionStrings__ReadReplicaConnection`) |
| `valkey` | `valkey/valkey:8.1-alpine` | Cache, SignalR backplane, and the wallow-web BFF session store |
| `garage` | `ghcr.io/bc-solutions-coder/wallow-garage` | S3-compatible object storage |

**One-shot services** (they run, exit, and gate the services that depend on them):

| Service | Purpose |
|---------|---------|
| `wallow-migrations` | Applies EF Core migrations for every module schema, then exits. Idempotent — runs on every deployment. |
| `wallow-seeder` | Seeds roles, API scopes, the bootstrap admin, and OIDC clients. Runs after migrations complete. |
| `api-cert-init` | `chown`s the `api_certs` volume to UID 1654 so the non-root API container can write its OpenIddict certificates. |

**Optional observability** — enabled with `--profile observability`:

| Service | Purpose |
|---------|---------|
| `alloy` | Grafana Alloy OTLP collector on the compose network — gRPC `http://alloy:4317` (the .NET API), HTTP `http://alloy:4318` (the Node apps' logger) |
| `grafana-lgtm` | `grafana/otel-lgtm` dashboards, published on `127.0.0.1:3001` only |

```bash
# profiles stack — keep your edge profile (direct or pangolin) on the command
docker compose -f docker-compose.production.yml --env-file .env.production \
  --profile direct --profile observability up --build
```

The API, auth, and web containers always set `OTEL_EXPORTER_OTLP_ENDPOINT`; without the profile
there is simply no collector listening, and the exporter fails silently.

**Persistent volumes:** `postgres_data`, `postgres_replica_data`, `valkey_data`, `garage_meta`,
`garage_data`, `api_certs`. The OpenIddict signing/encryption certificates live in `api_certs`
and are generated on first API startup — losing that volume invalidates every issued token.

---

## 2. Routing Topologies

The compose file supports two shapes. Pick one and keep the URL variables consistent with it.

**Path-based (the default).** Everything lives under one hostname:

| Public path | Container | Proxy behaviour |
|-------------|-----------|-----------------|
| `wallow.dev/api/*` | `wallow-api:8080` | Forward the **full** path — the app strips `/api` itself via `PathBase`. Do not strip it in the proxy. |
| `wallow.dev/auth/*` | `wallow-auth:8080` | Forward the **full** path — the Node app is built with `AUTH_BASE_PATH=/auth` and serves under that prefix. Do not strip it either. |
| `wallow.dev/*` | `wallow-web:8080` | Forward as-is. |

The proxy strips nothing in this topology. Both bundled edges implement it:

- **Direct mode** — the `caddy` service, configured by `docker/caddy/Caddyfile.example`, owns
  `:80`/`:443` while the three app containers publish only on `127.0.0.1` for debugging. Copy
  the example to `caddy/Caddyfile` and point `CADDYFILE_HOST_PATH` at your copy. Full
  per-service variables and nginx/Caddy examples are in the
  [Reverse Proxy guide](reverse-proxy.md).
- **Pangolin mode** — uses the **subdomain** topology below, not this one. The `pangolin.*`
  blueprint labels on `wallow-api`, `wallow-auth`, and `wallow-web` declare three host-based
  Pangolin resources (`PANGOLIN_API_DOMAIN`, `PANGOLIN_AUTH_DOMAIN`, `PANGOLIN_RESOURCE_DOMAIN`),
  so every app serves at its root and the pulled images work without `up --build` — tunnel
  deployments are usually driven by a pull-only stack manager that cannot rebuild. Set
  `API_PATH_BASE=` and `AUTH_BASE_PATH=` (both empty) and point the public URLs at the
  subdomains. The `newt` service applies the labels continuously through the Docker socket, so
  the compose file is the source of truth and dashboard edits to those resources are overwritten. Pangolin's proxy sends
  `X-Forwarded-Proto: https` straight to the apps; `WALLOW_TRUSTED_PROXIES=private` already
  covers newt's bridge address, so no extra trusted-proxy configuration is needed.

  If your stack manager (Dockhand, Portainer, Komodo) makes per-deploy `--profile` flags
  awkward, `docker/docker-compose.pangolin.yml` runs the same `newt` service as its own
  stack: deploy the main stack with **no** edge profile, then the pangolin stack alongside it
  with `.env.pangolin` (copy `.env.pangolin.example`; `WALLOW_NETWORK` must name the main
  stack's network as `docker network ls` prints it). Run one newt or the other, never both —
  they would steal the Pangolin tunnel from each other, so both copies claim the
  `wallow-newt` container name to make doubling up fail fast.

> `AUTH_BASE_PATH` is a **build** argument, not a runtime one — Vite bakes it into every asset
> URL. The published `wallow-auth` image is built at root, so path-based deployments must run
> `up --build` (which is why that is the documented invocation); a plain `pull` yields an auth
> container whose assets 404 under `/auth`.

**Subdomain.** Set `API_PATH_BASE=` and `AUTH_BASE_PATH=` (both empty) in `.env.production` so
the API and auth app serve at their subdomain roots, and point `API_PUBLIC_URL`,
`AUTH_PUBLIC_URL`, and `COOKIE_DOMAIN` at the subdomains:

```
api.wallow.dev/*  -> wallow-api:8080
auth.wallow.dev/* -> wallow-auth:8080
wallow.dev/*      -> wallow-web:8080
```

Containers speak plain HTTP. TLS termination is entirely your proxy's job.

---

## 3. First Boot Sequence

Startup order is enforced by `depends_on` conditions, so a single `up` command produces a fully
seeded system:

| Step | What happens | Gate |
|------|--------------|------|
| 1. Infrastructure | `postgres`, `postgres-replica`, `valkey`, and `garage` start and pass their health checks | `docker compose up` |
| 2. Migrations | `wallow-migrations` applies EF Core migrations for all module schemas, then exits | `postgres` healthy |
| 3. Seeding | `wallow-seeder` runs the four seed steps below, then exits | `wallow-migrations` completed successfully |
| 4. Cert volume | `api-cert-init` fixes ownership on `api_certs` | — |
| 5. API | `wallow-api` starts; it generates its OpenIddict certificates if the volume is empty | seeder + cert-init completed, infra healthy |
| 6. Frontends | `wallow-auth` and `wallow-web` start | `wallow-api` healthy |

First boot takes a couple of minutes while migrations run and the replica performs its initial
base backup.

### What the seeder does

`wallow-seeder` runs `Wallow.SeederService`, which executes four idempotent steps in order:

1. **Roles** — creates any role in the seed file's `roles` array that doesn't exist (`admin`,
   `manager`, `user`).
2. **API scopes** — inserts any scope from `apiScopes` not already present.
3. **Bootstrap admin** — runs only when the seed file has an `admin` block *and* setup is still
   required. It invokes the same `BootstrapAdminCommand` the setup endpoint uses, so the admin
   arrives fully provisioned: user, organization, and owner membership in one step. The
   production seed file deliberately has **no** `admin` block — see
   [setup mode](#setup-mode--when-no-admin-exists) below for how the first admin is created.
4. **Client sync** — reconciles OIDC clients (see [section 5](#5-oidc-client-registration)).

### The production seed file

The seeder image ships a development `seed.json`. In production the compose file **replaces**
it rather than layering on top of it, so localhost client definitions can never leak into a
production database:

```yaml
volumes:
  - ${SEED_FILE_HOST_PATH:-./seed.production.json}:/app/seed.production.json:ro
environment:
  SEED_FILE_PATH: /app/seed.production.json
```

The reference `docker/seed.production.json` is **committed** — it is secret-less by design
(see below), so it can travel with the repo, which is what git-based stack deploys (Dockhand,
Portainer) need: the default `./seed.production.json` mount resolves next to the compose file
in the checkout, no extra provisioning step. Forks edit it in place (`.gitattributes` marks it
`merge=ours`, like `seed.json`). To use a seed maintained outside the repo instead, point
`SEED_FILE_HOST_PATH` at an absolute path such as `/data/seed/seed.production.json` — it
overrides the committed file entirely. Every other `seed.*.json` variant remains gitignored.

The seed file is deliberately **secret-less**. Client secrets are injected as environment
variables keyed by **clientId** — each `ClientSecrets__<clientId>` value attaches to the seed
client with that id, so the order of the `clients` array never matters:

```yaml
ClientSecrets__wallow-web-client: ${OIDC_CLIENT_SECRET}
```

`wallow-web-client` — the dashboard client every deployment has — is the **only** seeded
production client. It is marked `"firstParty": true`, which is what exempts it from the consent
screen and binds it to no organization; the production seed therefore declares **no
organization, no membership, and no third-party client** — the first organization and its
administrator come from [setup mode](#setup-mode--when-no-admin-exists), and third-party
clients are registered against that organization afterwards. The seeder fails closed in both
misconfiguration directions: a seed client with no secret that does not declare
`"public": true` aborts, and so does a non-empty secret whose clientId matches no client in
the seed file. It also enforces the client/organization invariant at boot: a first-party
client that names an organization, or a third-party client that names none, aborts the seed
with the offending client id.

### Additional OIDC clients — create them through the UI

Extra clients (another frontend's BFF, a service integration, a third-party consumer) are
**not** seeded. Once the deployment is up and an administrator exists, create them from the
dashboard: an organization's detail page manages its OIDC clients — create the client with its
redirect URIs and scopes, then use **rotate secret** to issue the credential you configure the
consuming application with. Service accounts (client-credentials clients for headless
integrations) are managed from the same surface. The equivalent API endpoints live under
`/v1/identity/clients`.

Seeding remains the right tool only for a client that must exist *before* anyone can log in.
A fork with that constraint adds the client to its `seed.production.json` and injects its
secret with one more name-keyed `ClientSecrets__<clientId>` line in the compose file.

### Setup mode — when no admin exists

A fresh production deployment always starts in **setup mode** — the production seed file
deliberately defines no admin, so the first administrator is created by a person, not by
configuration. While setup is required, `SetupMiddleware` returns `503 Service Unavailable`
(as `application/problem+json`) for every request except `/v1/identity/setup`, `/health`,
`/.well-known`, `/connect`, `/openapi`, and `/scalar`. The API is effectively locked until an
admin exists.

The intended path is the auth app's first-run **setup page**, which walks through creating the
administrator account. The same thing can be done over raw HTTP (paths shown with the default
`/api` path base):

```bash
# Check setup status
curl https://wallow.dev/api/v1/identity/setup/status
# → {"setupRequired": true}

# Create the admin account
curl -X POST https://wallow.dev/api/v1/identity/setup/admin \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "YourStrongPassword123!",
    "firstName": "Admin",
    "lastName": "User",
    "organizationName": "Your Organization"
  }'
```

The call creates the user, the named organization, and the admin's owner membership in it.
Setup is considered complete as soon as an active membership grants admin access — there is no
separate flag to flip, and `setup/admin` returns `409 Conflict` once that is true.

---

## 4. Environment Variables

All configuration comes from `.env.production`, which the compose file reads via `--env-file`.
Generate it rather than copying the template by hand:

```bash
pnpm secrets:prod         # scripts/prod-secrets.sh -> docker/.env.production, mode 600
```

That renders `.env.production.example` with all 12 generatable secrets replaced by random values
of the shape each one requires, then prints the two things it could not decide for you: your
SMTP password, and the values that describe *where* this deployment lives (`API_PUBLIC_URL`,
`AUTH_PUBLIC_URL`, `WEB_PUBLIC_URL`, `COOKIE_DOMAIN`, `SMTP_HOST`,
`SMTP_FROM_ADDRESS`, `SEED_FILE_HOST_PATH`). It is bootstrap only — it refuses to
overwrite an existing `.env.production`, because rotating a secret that is already in use is a
database or cluster operation rather than a text substitution.

To generate one by hand:

```bash
openssl rand -hex 32      # general passwords and signing keys, and the Garage secret key
openssl rand -hex 48      # IDENTITY_SIGNING_KEY
openssl rand -hex 12      # Garage access key ID (prefix with 'GK'; 'GK' + exactly 24 hex chars)
```

Use `-hex`, not `-base64`. Base64 emits `+`, `/` and `=`, and the compose file interpolates
several of these into URLs — `VALKEY_PASSWORD` becomes `redis://:${VALKEY_PASSWORD}@valkey:6379`,
where a stray `/` or `@` truncates the host and the BFF quietly fails to reach Valkey.

**Every secret except `GF_ADMIN_PASSWORD` fails closed.** The compose entries are written as
`${VAR:?message}`, so a missing one aborts `docker compose` naming the variable instead of
starting the stack with a blank signing key. `GF_ADMIN_PASSWORD` is the one exception, and not by
choice: Compose interpolates the whole file before applying `profiles:`, so marking it required
would break every deployment that never enables the `observability` profile. Set it yourself
whenever you use that profile.

`.env.production.example` documents every variable inline. The categories:

| Category | Variables |
|----------|-----------|
| **Project** | `COMPOSE_PROJECT_NAME` |
| **Image tag** | `VERSION` |
| **Database** | `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` |
| **Cache** | `VALKEY_PASSWORD` |
| **Storage** | `GARAGE_RPC_SECRET`, `GARAGE_ADMIN_TOKEN`, `GARAGE_ACCESS_KEY`, `GARAGE_SECRET_KEY`, `GARAGE_KEY_NAME`, `GARAGE_BUCKET`, `GARAGE_REGION` |
| **SMTP** | `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_SSL`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`, `SMTP_FROM_NAME` |
| **Security** | `IDENTITY_SIGNING_KEY`, `OPENIDDICT_SIGNING_CERT_PASSWORD`, `OPENIDDICT_ENCRYPTION_CERT_PASSWORD`, `OPENIDDICT_ALLOW_PLAIN_HTTP_ENDPOINTS` |
| **Seeding** | `SEED_FILE_HOST_PATH` |
| **OIDC** | `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`, plus optional per-fork client secrets |
| **BFF session** | `BFF_COOKIE_PASSWORD`; optional: `BFF_COOKIE_PASSWORDS` (rotation), `BFF_COOKIE_SECURE`, `BFF_COOKIE_HOST_PREFIX`, `BFF_COOKIE_NAME`, `BFF_SESSION_TTL_SECONDS`, `BFF_OIDC_SCOPES` |
| **Public URLs** | `API_PUBLIC_URL`, `AUTH_PUBLIC_URL`, `WEB_PUBLIC_URL`, `COOKIE_DOMAIN`, `API_PATH_BASE`, `AUTH_BASE_PATH` |
| **Ingress (direct)** | `CADDYFILE_HOST_PATH`, `INGRESS_HTTP_PORT`, `INGRESS_HTTPS_PORT` |
| **Ingress (pangolin)** | `PANGOLIN_ENDPOINT`, `PANGOLIN_RESOURCE_DOMAIN`, `PANGOLIN_API_DOMAIN`, `PANGOLIN_AUTH_DOMAIN`, `NEWT_ID`, `NEWT_SECRET` |
| **Ports** | `API_PORT`, `AUTH_PORT`, `WEB_PORT` (all bound to `127.0.0.1`) |
| **Observability** | `GF_ADMIN_PASSWORD`, `OTEL_TRACE_SAMPLING_RATIO` |

`OTEL_TRACE_SAMPLING_RATIO` defaults to `0.1`, so nine out of ten traces are dropped before they
reach the collector. That is deliberate — see
[Performance Considerations](observability.md#performance-considerations) — but it surprises
anyone who goes looking for a specific request in Tempo and does not find it.

### Three variables worth calling out

**`VERSION`** selects the image tag for every Wallow image in the stack
(`ghcr.io/bc-solutions-coder/wallow:${VERSION:-latest}` and friends). Use `latest` to roll with
releases or pin a semver tag:

```ini
VERSION=1.2.3
```

**`BFF_COOKIE_PASSWORD`** seals the wallow-web BFF session cookie (iron-webcrypto) and must be at
least 32 characters. It is a **hard requirement** — the compose entry is written as

```yaml
COOKIE_PASSWORD: ${BFF_COOKIE_PASSWORD:?BFF_COOKIE_PASSWORD is required (32+ char secret; see .env.production.example)}
```

so `docker compose up` fails immediately with that message rather than starting a web container
with an insecure or absent session key. Generate it with `openssl rand -hex 32`.

Changing it invalidates every signed-in session — the new value cannot unseal cookies sealed with
the old one. **`BFF_COOKIE_PASSWORDS`** is the overlap window: a JSON object of key ID to secret
whose *first* key seals new cookies while every other key stays valid for unsealing. Leave it
unset and the single password above is used.

**Your first rotation must key the outgoing secret `default`.** A deployment running on plain
`BFF_COOKIE_PASSWORD` seals its cookies with no key ID at all, and iron-webcrypto reads a missing
key ID back as the literal `default`. Publish that same secret under any other ID — `k1`, `v1` —
and every live session fails to unseal with `Cannot find password: default`, which is exactly the
outage rotation exists to avoid. Later rotations may use any allowed ID, because by then every
cookie in the wild carries one.

```ini
BFF_COOKIE_PASSWORDS={"v2":"<new 64-hex>","default":"<the current value>"}
```

Redeploy, wait longer than the session TTL (`BFF_SESSION_TTL_SECONDS`, default `86400`) so every
cookie sealed under the retiring secret has expired, then drop the `default` entry
(`{"v2":"<new 64-hex>"}`) and redeploy again. Each secret must be 32+ characters and each key ID
must be letters, digits or underscores and not all digits; the SDK validates the whole map at
boot rather than failing mid-login. The full procedure, including the optional no-op deploy that
gets a key ID onto cookies before you rotate, is in
[Rotating the Cookie Password](../integrations/bff-pattern.md#rotating-the-cookie-password).

The rest of the BFF's cookie and session contract is optional, and the shipped stack needs none
of it set — but it reaches a deployment either way, so it is passed explicitly and documented in
`.env.production.example` rather than being discoverable only by reading SDK source.

**Two layers, two names.** Everything in the table below is the name you set in
`.env.production`; the compose file strips the `BFF_` prefix on the way into the container, so
`BFF_SESSION_TTL_SECONDS` arrives at the SDK as `SESSION_TTL_SECONDS`, `BFF_COOKIE_PASSWORDS` as
`COOKIE_PASSWORDS`, and so on. The SDK documentation and any error message from inside the
container use the unprefixed form; `.env.production` and this guide use the prefixed one. Both are
correct in their own layer.

| Variable | Default | Notes |
|----------|---------|-------|
| `BFF_COOKIE_SECURE` | `true` | Only the literal `false` clears it. |
| `BFF_COOKIE_HOST_PREFIX` | `true` | `__Host-` prefix; also requires Secure. |
| `BFF_COOKIE_NAME` | `__Host-wallow_bff` | `wallow_bff` when either flag above is off. |
| `BFF_SESSION_TTL_SECONDS` | `86400` | Malformed values throw at boot, never fall back. |
| `BFF_OIDC_SCOPES` | `openid profile email offline_access` | Space-separated. |

The two flags **fail secure** — a typo such as `False` or `no` leaves the cookie protected rather
than exposing it, and only the exact string `false` turns either off. Clear them only for a
plain-HTTP deployment. Override `BFF_COOKIE_NAME` when two Wallow deployments share one origin:
`__Host-` forces `path=/`, so their session cookies would otherwise collide on the name. Dropping
`offline_access` from the scopes costs you refresh tokens, and with them the silent renewal that
keeps a session alive to the TTL.

**`OPENIDDICT_ALLOW_PLAIN_HTTP_ENDPOINTS`** controls whether the OIDC endpoints
(`/connect/**` and discovery) will answer plain-HTTP requests. Outside Development the answer is
**no** by default, so a deployment that accidentally exposes Kestrel directly cannot serve
authorization codes and tokens in the clear. This stack ships it as `true` because TLS terminates
at the reverse proxy and the BFF resolves discovery over the private Docker network
(`http://wallow-api:8080/.well-known/openid-configuration`), which never traverses the proxy and
so carries no `X-Forwarded-Proto`. Set it to `false` if Kestrel serves HTTPS itself. The value must
parse as a boolean — a typo such as `yes` fails startup rather than silently picking a side.

---

## 5. OIDC Client Registration

Clients are declared in the `clients` array of your seed file and reconciled by
`PreRegisteredClientSyncService`, which the seeder invokes on every run. Each sync:

1. **Creates or updates** each client in the seed file — redirect URIs, post-logout URIs,
   permissions, and scopes are all brought in line with the definition.
2. **Auto-creates an organization** for a client's `TenantName` when one doesn't exist, and
   links the client to that tenant.
3. **Adds seed members** — users listed in the client's `SeedMembers` are added to that
   organization (a warning is logged for any email that has no account yet).
4. **Deletes** clients that were previously created from config (tagged `source: config`) but
   are no longer in the seed file.

The whole sync is idempotent, so re-running the seeder is always safe.

### Client Types

| Type | Has Secret? | Grant Type | Use Case |
|------|-------------|------------|----------|
| **Confidential** (e.g. the `wallow-web` BFF) | Yes | Authorization Code + PKCE | Server-side apps with a secure backend |
| **Public** (e.g. a mobile app) | No | Authorization Code + PKCE | SPAs, mobile apps, CLI tools |
| **Service Account** (client id prefixed `sa-`) | Yes | Client Credentials | Backend-to-backend, M2M |

### Available Scopes

Standard OIDC scopes — `openid`, `profile`, `email`, `offline_access` — plus the API scopes
defined in `ApiScopes.ValidScopes`:

| Category | Scopes |
|----------|--------|
| Identity — Users | `users.read`, `users.write`, `users.manage` |
| Identity — Roles | `roles.read`, `roles.write`, `roles.manage` |
| Identity — Organizations | `organizations.read`, `organizations.write`, `organizations.manage` |
| Identity — API Keys | `apikeys.read`, `apikeys.write`, `apikeys.manage` |
| Storage | `storage.read`, `storage.write` |
| Announcements | `announcements.read`, `announcements.manage`, `changelog.manage` |
| Notifications | `notifications.read`, `notifications.write` |
| Inquiries | `inquiries.read`, `inquiries.write` |
| Configuration | `configuration.read`, `configuration.manage` |
| Platform | `webhooks.manage` |

A scope must also exist as a seeded `ApiScope` row before a client can be granted it, which is
what the seeder's API-scope step guarantees.

---

## 6. Connecting External Clients

All examples below assume path-based routing with `API_PUBLIC_URL=https://wallow.dev/api`.

**Discovery endpoint:** `https://wallow.dev/api/.well-known/openid-configuration`

### OAuth2 Authorization Code Flow (SPAs / Mobile)

Add your app to the seed file's `clients` array, redeploy so the seeder syncs it, then use any
standard OIDC library:

```typescript
import { UserManager } from 'oidc-client-ts';

const mgr = new UserManager({
  authority: 'https://wallow.dev/api',
  client_id: 'my-spa-client',
  redirect_uri: 'https://myapp.com/callback',
  post_logout_redirect_uri: 'https://myapp.com/',
  scope: 'openid email profile offline_access',
  response_type: 'code',
});

await mgr.signinRedirect();
const user = await mgr.signinRedirectCallback();
```

For a browser app that should never hold a token, use the BFF pattern instead — see
[BFF Pattern](../integrations/bff-pattern.md) and the
[TypeScript SDK guide](../integrations/typescript-sdk.md).

### Client Credentials Flow (Service Accounts)

```bash
curl -X POST https://wallow.dev/api/connect/token \
  -d "grant_type=client_credentials" \
  -d "client_id=sa-my-backend" \
  -d "client_secret=<your-secret>" \
  -d "scope=inquiries.read inquiries.write"
```

### API Keys (Backend Services)

API keys authenticate via the `X-Api-Key` header as an alternative to a bearer token, and are
scoped to the creating user's tenant.

```bash
# Create an API key (requires an access token)
curl -X POST https://wallow.dev/api/v1/identity/auth/keys \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "My Backend", "scopes": ["inquiries.read"]}'

# Use the API key
curl -H "X-Api-Key: <your-key>" \
  https://wallow.dev/api/v1/inquiries
```

### SignalR Real-Time Updates

The hub is mounted at `/hubs/realtime` (outside the versioned API prefix, but still behind the
path base):

```typescript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('https://wallow.dev/api/hubs/realtime', {
    accessTokenFactory: () => getAccessToken()
  })
  .withAutomaticReconnect()
  .build();

connection.on('ReceiveNotification', (notification) => {
  console.log('New notification:', notification);
});

await connection.start();
```

---

## 7. CI/CD Pipeline

Two workflows split the work: **deploy builds**, **publish promotes**. See
[Versioning](versioning.md) for how release-please decides version numbers.

**1. Merge to `main` → `.github/workflows/deploy.yml`.** No tests run here (they passed on the
PR). It builds the solution, then builds and pushes seven multi-arch (amd64 + arm64) image
families to GHCR, each tagged `:nightly` and `:<short-sha>`:

| Image | Built from |
|-------|-----------|
| `ghcr.io/bc-solutions-coder/wallow` | `api/src/Wallow.Api` (`PublishContainer`) |
| `…/wallow-auth` | `apps/wallow-auth/Dockerfile` (build context = repo root) |
| `…/wallow-web` | `apps/wallow-web/Dockerfile` (build context = repo root) |
| `…/wallow-migrations` | `api/src/Wallow.MigrationService` |
| `…/wallow-seeder` | `api/src/Wallow.SeederService` |
| `…/wallow-garage` | `docker/images/garage` |
| `…/wallow-postgres-replica` | `docker/images/postgres-replica` |

Deploy skips release-please merge commits, which change only version and changelog files.

**2. Merge the Release PR** — release-please creates the `vX.Y.Z` git tag and GitHub Release.

**3. Tag push → `.github/workflows/publish.yml`.** This workflow **builds nothing**. It waits for
the `:<short-sha>` images from the deploy run on the same commit (polling for up to 30 minutes),
then retags each of the seven families to `:X.Y.Z`, `:X.Y`, and `:latest` via
`docker buildx imagetools create`, and finally scans the API image with Trivy, failing on
CRITICAL or HIGH findings.

**Tag tiers:**

| Tag | Meaning |
|-----|---------|
| `:nightly` | Latest `main` commit — bleeding edge, may be broken |
| `:latest` | Current released version |
| `:X.Y.Z` / `:X.Y` | Pinned release versions |
| `:<short-sha>` | A specific commit (internal plumbing between the two workflows) |

### Deploying an update

```bash
cd docker
docker compose -f docker-compose.production.yml --env-file .env.production pull
# use the same edge profile the deployment runs with (direct or pangolin)
docker compose -f docker-compose.production.yml --env-file .env.production --profile direct up -d
```

`wallow-migrations` and `wallow-seeder` re-run on every `up` and are idempotent, so schema
changes and new seed entries are applied as part of the restart. To pin a release, set `VERSION`
in `.env.production` before pulling.

---

## 8. Scaling

- **Database:** swap `postgres` for managed PostgreSQL (RDS, Hetzner, Supabase) and repoint
  `ConnectionStrings__DefaultConnection` / `ConnectionStrings__ReadReplicaConnection`.
- **Cache:** use managed Redis/Valkey; the API and the wallow-web BFF both point at it.
- **App instances:** run multiple `wallow-api` containers behind a load balancer. SignalR uses
  the Valkey backplane, so multi-instance fan-out works without sticky sessions. The
  `api_certs` volume must be shared or replaced with pre-provisioned certificates so all
  instances sign with the same key.
- **Storage:** replace GarageHQ with managed S3 (AWS, Cloudflare R2) by changing the
  `Storage__S3__*` values.

### Account Creation Methods

| Method | When it runs | What gets created |
|--------|--------------|-------------------|
| **Seeder admin bootstrap** | Every seeder run, but acts only while no admin user exists | Admin user with the `admin` role |
| **Client sync + `SeedMembers`** | Every seeder run | OIDC clients, organizations, org memberships |
| **Setup endpoint** | While the API is in setup mode | Admin user, created over HTTP |
| **Self-registration** | Anytime | Users register themselves via `/v1/identity/auth/register` |
| **External OAuth** (Google, Microsoft, GitHub, Apple) | Anytime, if providers are configured | Users auto-created on first external login |
| **Invitations** | Anytime | Org admins invite by email; the account is created on acceptance |
