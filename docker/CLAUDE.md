# docker — Compose Stacks Agent Guide

Three independent compose files, each a complete stack. Always run them from **this
directory** — the relative build contexts (`./images/`, `..`) depend on it.

| File | Stack |
|------|-------|
| `docker-compose.yml` | Dev infrastructure only — Postgres, Valkey, Mailpit, Garage (S3), Alloy + Grafana LGTM, the DocFX docs site; ClamAV behind `--profile clamav`. No app containers. |
| `docker-compose.test.yml` | Containerised E2E stack — the same infra plus `wallow-migrations` / `wallow-seeder` / `wallow-api` (prebuilt `*:test` images) and the `wallow-auth` / `wallow-web` / `bff-example` Node apps. Driven by `./scripts/e2e.sh`, not by hand. |
| `docker-compose.production.yml` | Full production topology — pulls `ghcr.io/bc-solutions-coder/*` images, adds a Postgres replica, hardened API settings and a reference Caddy ingress. Its header comment documents path-based vs subdomain routing. |

```bash
cp .env.example .env        # required before the dev stack; GF_ADMIN_PASSWORD must be set
docker compose up -d        # or: pnpm backend:infra from the repo root

pnpm secrets:prod           # from the repo root — writes docker/.env.production
docker compose -f docker-compose.production.yml --env-file .env.production up --build
```

- **The production stack fails closed.** Every secret in `docker-compose.production.yml` uses
  `${VAR:?message}`, so a missing one aborts `docker compose` naming the variable instead of
  starting a stack with a blank signing key. Each secret carries that `:?` at **exactly one**
  site, deliberately: a missing required variable aborts the whole interpolation pass regardless
  of which service references it, so one marker per secret is the entire guarantee and repeating
  it at the other four `POSTGRES_PASSWORD` sites would buy nothing. Two documented exceptions —
  `GF_ADMIN_PASSWORD` *cannot* fail closed (interpolation runs before `profiles:` are applied, so
  requiring it would break every deployment that never enables `observability`), and the
  `BCORDES_*` client secrets stay bare because the **seeder** is what fails on them. Watch the
  YAML when writing a message: a plain scalar containing colon-space parses as a mapping, so write
  ``generate with `openssl rand -hex 32` `` and never `generate: openssl ...`.
- **`scripts/prod-secrets.sh`** (`pnpm secrets:prod`) renders `.env.production.example` into
  `.env.production` with the 13 generatable secrets randomised to their required shapes
  (`GK` + 24 hex for the Garage key ID, 64 hex for its secret, `-hex 48` for the signing key, a
  mixed-class password for `ADMIN_PASSWORD`). Bootstrap only — it refuses to overwrite an existing
  file, because rotating a live secret is a database/cluster operation, not a text substitution.
  Adding a new secret to the example file means adding it to that script's `secret_for` case.
- **Cookie rotation** is `BFF_COOKIE_PASSWORDS` — a JSON map of key ID to secret whose first key
  seals while the rest still unseal, so rotating `BFF_COOKIE_PASSWORD` need not log everyone out.
  The SDK treats an empty string as unset (`packages/sdk/src/server/config.ts`), which is why
  compose can pass `${BFF_COOKIE_PASSWORDS:-}` unconditionally. Procedure is in the example file.

- **Image build contexts**: app Dockerfiles live with their apps (`apps/wallow-*/Dockerfile`)
  and build from the **repo root** context so `workspace:*` deps resolve. Only the docs site
  (`docker/docs/Dockerfile`) and the Garage images (`docker/images/`) are built from here.
- **Ingress (production stack only)** — the `caddy` service owns `:80`/`:443` and is the sole
  externally reachable container; the three app services publish on `127.0.0.1` for debugging
  only. Routing lives in `caddy/Caddyfile.example` (copy it and point `CADDYFILE_HOST_PATH` at
  your copy). Both the `/api` and `/auth` prefixes are **kept**, never stripped — each app
  rebases itself. `AUTH_BASE_PATH` is a **build** arg, not a runtime env, so path-based
  deployments must `up --build`; the published `wallow-auth` image is root-mounted.
- **One Garage image** — `images/garage/` serves all three stacks (and the Aspire AppHost).
  It has no committed `garage.toml`: the entrypoint renders `garage.toml.template` through
  `envsubst` at startup, so its knobs (region, RPC secret, admin token, key, bucket) come from
  env. `GARAGE_S3_PORT` / `GARAGE_ADMIN_PORT` are the exception — the template still reads them,
  but both compose files pass them as the literals `3900` / `3903` rather than as `${VAR}`. They
  are container-internal, and the only thing a knob could do is desync the listener from the host
  mapping (dev) or from `Storage__S3__Endpoint` (production), which are themselves literals.
  Dev and test only differ by the tag they build it as (`:v2.2.0` / `:test`).
- The API container entrypoint lives with its code at `api/src/Wallow.Api/entrypoint.sh` (no
  compose file references it) — `Wallow.Api.csproj` publishes it as `Content` and wires it via
  `ContainerEntrypoint`.
- **Adding a `${VAR}` to a compose file means adding it to that file's `.env.example`.**
  `pnpm lint:env` (`scripts/check-env.sh`, in the `pnpm check` chain) fails otherwise. It asserts
  documentation COMPLETENESS, not requiredness — a `${V:-default}` still needs an entry, because a
  defaulted knob is still a knob a fork has to be able to find. A commented-out line counts, which
  is what lets `.env.production.example` document the optional `BCORDES_*` client secrets without
  setting them: their bare `${VAR}` form in `docker-compose.production.yml` is deliberate
  fail-closed design (an unset secret aborts the seeder rather than registering a public client),
  so nothing may "fix" it by giving them defaults. **Full-line comments are not scanned**, so
  prose explaining the `${VAR}` grammar is documentation rather than a reference; a trailing
  comment on a value line still is, because clipping from the first `#` could drop a real
  reference out of a quoted value. An app's own env keys are invisible to this check by
  construction — it walks compose references only, which is why the wallow-web service passes the
  SDK's five optional cookie/session knobs explicitly instead of letting them go undocumented.
  Pairings are `docker-compose.yml` and
  `docker-compose.test.yml` → `.env.example`, `docker-compose.production.yml` →
  `.env.production.example`, `turbo-cache/docker-compose.yml` → `turbo-cache/.env.example`; a new
  compose file needs a new entry in the script's `pairs` list.
- `.env`, `.env.example`, and `seed.json` are `merge=ours` in `.gitattributes` so fork values
  survive upstream merges. Never commit a real `.env` / `.env.production`.
