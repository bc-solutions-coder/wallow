# Wallow Production Deployment Guide

Wallow deploys as a set of Docker containers described by a single compose file,
`docker/docker-compose.production.yml`. The stack is self-contained — it runs the same way on
your laptop and on a server. TLS and routing are **not** part of the stack: you put your own
reverse proxy in front of the published container ports.

```bash
pnpm secrets:prod                             # writes docker/.env.production with fresh secrets
cd docker
# then edit the values the script cannot invent — it lists them
docker compose -f docker-compose.production.yml --env-file .env.production up --build
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
local debugging — the `caddy` ingress is the only externally reachable container.

**Long-running services:**

| Service | Image | Purpose |
|---------|-------|---------|
| `caddy` | `caddy:2-alpine` | Reference reverse-proxy ingress; owns `:80`/`:443` and routes `/api`, `/auth`, and the catch-all to the three apps (`docker/caddy/Caddyfile.example`) |
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
docker compose -f docker-compose.production.yml --env-file .env.production \
  --profile observability up --build
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

The proxy strips nothing in this topology. The compose file ships a reference ingress that
implements it — the `caddy` service, configured by `docker/caddy/Caddyfile.example`, which owns
`:80`/`:443` while the three app containers publish only on `127.0.0.1` for debugging. Copy the
example to `caddy/Caddyfile` and point `CADDYFILE_HOST_PATH` at your copy. Full per-service
variables and nginx/Caddy examples are in the
[Reverse Proxy guide](reverse-proxy.md).

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
3. **Bootstrap admin** — creates the initial admin user *only if setup is still required*, i.e.
   there is no user in the `admin` role yet. Email, password, and name come from the
   `Admin__*` settings.
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

`seed.production.json` matches the `seed.*.json` gitignore pattern, so it does **not** travel
with the repo — you must place it on the server yourself and point `SEED_FILE_HOST_PATH` at it
(an absolute path such as `/data/seed/seed.production.json` is typical; the default
`./seed.production.json` resolves next to the compose file).

The seed file is deliberately **secret-less**. Client secrets are injected as environment
variables indexed against the `clients` array, and **the index order is load-bearing** — it must
match the array order in your `seed.production.json`:

```yaml
Clients__0__Secret: ${OIDC_CLIENT_SECRET}
Clients__1__Secret: ${BCORDES_CLIENT_SECRET}
Clients__2__Secret: ${BCORDES_BFF_SECRET}
Clients__3__Secret: ${BCORDES_BFF_AUTHCODE_SECRET}
```

Index 0 is the `wallow-web-client` dashboard client; the rest are the fork's own clients and are
commented out in `.env.production.example` until you need them. If you add or reorder clients in
the seed file, update these indices in the compose file to match.

### Setup mode — when no admin exists

If the seeder never creates an admin (no `ADMIN_EMAIL`/`ADMIN_PASSWORD`, or the seed step was
skipped), the API comes up in **setup mode**. `SetupMiddleware` returns `503 Service
Unavailable` for every request except `/v1/identity/setup`, `/health`, `/.well-known`,
`/connect`, `/openapi`, and `/scalar`. The API is effectively locked until an admin exists.

Recover by creating the admin over HTTP (paths shown with the default `/api` path base):

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
    "lastName": "User"
  }'
```

Setup is considered complete as soon as a user holds the `admin` role — there is no separate
flag to flip, and both `setup/admin` and `setup/complete` return `409 Conflict` once that is
true.

**Recommendation:** set `ADMIN_EMAIL` and `ADMIN_PASSWORD` in `.env.production` so the stack is
operational on first boot with no manual step.

---

## 4. Environment Variables

All configuration comes from `.env.production`, which the compose file reads via `--env-file`.
Generate it rather than copying the template by hand:

```bash
pnpm secrets:prod         # scripts/prod-secrets.sh -> docker/.env.production, mode 600
```

That renders `.env.production.example` with all 13 generatable secrets replaced by random values
of the shape each one requires, then prints the two things it could not decide for you: your
SMTP password, and the values that describe *where* this deployment lives (`API_PUBLIC_URL`,
`COOKIE_DOMAIN`, `ADMIN_EMAIL`, `SEED_FILE_HOST_PATH`). It is bootstrap only — it refuses to
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
| **Seeding** | `SEED_FILE_HOST_PATH`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`, `ADMIN_FIRST_NAME`, `ADMIN_LAST_NAME` |
| **OIDC** | `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`, plus optional per-fork client secrets |
| **BFF session** | `BFF_COOKIE_PASSWORD`, `BFF_COOKIE_PASSWORDS` (optional — rotation) |
| **Public URLs** | `API_PUBLIC_URL`, `AUTH_PUBLIC_URL`, `WEB_PUBLIC_URL`, `COOKIE_DOMAIN`, `API_PATH_BASE`, `AUTH_BASE_PATH` |
| **Ingress** | `CADDYFILE_HOST_PATH`, `INGRESS_HTTP_PORT`, `INGRESS_HTTPS_PORT` |
| **Ports** | `API_PORT`, `AUTH_PORT`, `WEB_PORT` (all bound to `127.0.0.1`) |
| **Observability** | `GF_ADMIN_PASSWORD` |

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

```ini
BFF_COOKIE_PASSWORDS={"k2":"<new 64-hex>","k1":"<the current value>"}
```

Redeploy, wait longer than `SESSION_TTL_SECONDS` (default `86400`) so every `k1` cookie has
expired, then drop `k1` and redeploy again. Each secret must be 32+ characters and each key ID
must be letters, digits or underscores and not all digits; the SDK validates the whole map at
boot rather than failing mid-login.

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
| Identity — Service Accounts | `serviceaccounts.read`, `serviceaccounts.write`, `serviceaccounts.manage` |
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
docker compose -f docker-compose.production.yml --env-file .env.production up -d
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
