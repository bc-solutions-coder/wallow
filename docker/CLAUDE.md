# docker — Compose Stacks Agent Guide

Three independent compose files, each a complete stack. Always run them from **this
directory** — the relative build contexts (`./garage`, `./images/`, `..`) depend on it.

| File | Stack |
|------|-------|
| `docker-compose.yml` | Dev infrastructure only — Postgres, Valkey, Mailpit, Garage (S3), Alloy + Grafana LGTM, the DocFX docs site; ClamAV behind `--profile clamav`. No app containers. |
| `docker-compose.test.yml` | Containerised E2E stack — the same infra plus `wallow-migrations` / `wallow-seeder` / `wallow-api` (prebuilt `*:test` images) and the `wallow-auth` / `wallow-web` / `bff-example` Node apps. Driven by `./scripts/e2e.sh`, not by hand. |
| `docker-compose.production.yml` | Full production topology — pulls `ghcr.io/bc-solutions-coder/*` images, adds a Postgres replica and hardened API settings. Its header comment documents path-based vs subdomain routing. |

```bash
cp .env.example .env        # required before the dev stack; GF_ADMIN_PASSWORD must be set
docker compose up -d        # or: pnpm backend:infra from the repo root
docker compose -f docker-compose.production.yml --env-file .env.production up --build
```

- **Image build contexts**: app Dockerfiles live with their apps (`apps/wallow-*/Dockerfile`)
  and build from the **repo root** context so `workspace:*` deps resolve. Only the docs site
  (`docs/Dockerfile`) and the Garage images are built from here.
- **Two Garage image dirs exist** — `garage/` (dev compose) and `images/garage/` (test compose).
  They are not interchangeable; check which compose you are editing.
- `images/api/entrypoint.sh` is referenced by **no** compose file — it is copied into the API
  container by `api/src/Wallow.Api/Wallow.Api.csproj` (`ContainerEntrypoint`).
- `.env`, `.env.example`, and `seed.json` are `merge=ours` in `.gitattributes` so fork values
  survive upstream merges. Never commit a real `.env` / `.env.production`.
