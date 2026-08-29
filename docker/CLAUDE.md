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

- **The production stack fails closed.** Every secret uses `${VAR:?message}`, at **exactly
  one** site per secret — a missing variable aborts the whole interpolation pass regardless of
  which service references it, so one marker is the entire guarantee. Two documented
  exceptions: `GF_ADMIN_PASSWORD` cannot fail closed (interpolation runs before `profiles:`
  apply, so requiring it would break deployments that never enable `observability`), and the
  `BCORDES_*` client secrets stay bare because the **seeder** is what fails on them. Watch the
  YAML when writing a `:?` message: a plain scalar containing colon-space parses as a mapping,
  so write ``generate with `openssl rand -hex 32` `` and never `generate: openssl ...`.
- **`scripts/prod-secrets.sh`** (`pnpm secrets:prod`) renders `.env.production.example` into
  `.env.production` with the generatable secrets randomised to their required shapes.
  Bootstrap only — it refuses to overwrite an existing file, because rotating a live secret is
  a database/cluster operation, not a text substitution. A new secret in the example file also
  needs an entry in the script's `secret_for` case.
- **Cookie rotation** is `BFF_COOKIE_PASSWORDS` — a JSON map of key ID to secret whose first
  key seals while the rest still unseal, so rotation need not log everyone out. The SDK treats
  an empty string as unset (`packages/sdk/src/server/config.ts`), which is why compose can
  pass `${BFF_COOKIE_PASSWORDS:-}` unconditionally. Procedure is in the example file.
- **Image build contexts**: app Dockerfiles live with their apps (`apps/wallow-*/Dockerfile`)
  and build from the **repo root** context so `workspace:*` deps resolve. Only the docs site
  (`docker/docs/Dockerfile`) and the Garage images (`docker/images/`) are built from here.
- **Ingress (production stack only)** — the `caddy` service owns `:80`/`:443` and is the sole
  externally reachable container; the app services publish on `127.0.0.1` for debugging only.
  Routing lives in `caddy/Caddyfile.example` (copy it and point `CADDYFILE_HOST_PATH` at your
  copy). Both the `/api` and `/auth` prefixes are **kept**, never stripped — each app rebases
  itself. `AUTH_BASE_PATH` is a **build** arg, not a runtime env, so path-based deployments
  must `up --build`; the published `wallow-auth` image is root-mounted.
- **One Garage image** — `images/garage/` serves all three stacks (and the Aspire AppHost).
  No committed `garage.toml`: the entrypoint renders `garage.toml.template` through `envsubst`
  at startup, so its knobs (region, RPC secret, admin token, key, bucket) come from env.
  Exception: `GARAGE_S3_PORT` / `GARAGE_ADMIN_PORT` are passed as the literals `3900` /
  `3903` — they are container-internal, and a knob could only desync the listener from the
  host mapping (dev) or from `Storage__S3__Endpoint` (production), which are themselves
  literals. Dev and test differ only by the tag they build (`:v2.2.0` / `:test`).
- The API container entrypoint lives with its code at `api/src/Wallow.Api/entrypoint.sh` (no
  compose file references it) — `Wallow.Api.csproj` publishes it as `Content` and wires it via
  `ContainerEntrypoint`.
- **Adding a `${VAR}` to a compose file means adding it to that file's `.env.example`.**
  `pnpm lint:env` (`scripts/check-env.sh`, in the `pnpm check` chain) fails otherwise. It
  asserts documentation COMPLETENESS, not requiredness — a `${V:-default}` still needs an
  entry. A commented-out line counts, which lets `.env.production.example` document the
  optional `BCORDES_*` secrets without setting them (their bare `${VAR}` form is deliberate
  fail-closed design — never "fix" it with defaults). Full-line comments are not scanned;
  a trailing comment on a value line is. The check walks compose references only, so an app's
  own env keys are invisible to it — which is why the wallow-web service passes the SDK's
  optional cookie/session knobs explicitly. Pairings: `docker-compose.yml` and
  `docker-compose.test.yml` → `.env.example`; `docker-compose.production.yml` →
  `.env.production.example`; `turbo-cache/docker-compose.yml` → `turbo-cache/.env.example`. A
  new compose file needs a new entry in the script's `pairs` list.
- `.env`, `.env.example`, and `seed.json` are `merge=ours` in `.gitattributes` so fork values
  survive upstream merges. Never commit a real `.env` / `.env.production`.
