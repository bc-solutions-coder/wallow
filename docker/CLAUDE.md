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
docker compose -f docker-compose.production.yml --env-file .env.production up --build
```

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
  `envsubst` at startup, so every knob (region, ports, RPC secret, admin token, key, bucket)
  comes from env. Dev and test only differ by the tag they build it as (`:v2.2.0` / `:test`).
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
  so nothing may "fix" it by giving them defaults. Pairings are `docker-compose.yml` and
  `docker-compose.test.yml` → `.env.example`, `docker-compose.production.yml` →
  `.env.production.example`; a new compose file needs a new entry in the script's `pairs` list.
- `.env`, `.env.example`, and `seed.json` are `merge=ours` in `.gitattributes` so fork values
  survive upstream merges. Never commit a real `.env` / `.env.production`.
