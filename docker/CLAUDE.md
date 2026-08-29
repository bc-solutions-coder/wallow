# docker — Compose Stacks Agent Guide

Always run compose from **this directory** — the relative build contexts (`./images/`, `..`)
depend on it.

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Dev infrastructure only (Postgres, Valkey, Mailpit, Garage, observability, docs; ClamAV behind `--profile clamav`). No app containers. |
| `docker-compose.test.yml` | Containerised E2E stack — driven by `./scripts/e2e.sh`, not by hand. |
| `docker-compose.production.yml` | Full production topology (ghcr images, Postgres replica, profile-gated edges). Its header comment documents routing and the edge profiles. |
| `docker-compose.pangolin.yml` | Standalone newt tunnel stack — run INSTEAD of `--profile pangolin`, never alongside; details in its header comment. |

```bash
pnpm secrets:prod           # from the repo root — writes docker/.env.production
# exactly ONE edge profile: direct (Caddy) or pangolin (newt tunnel)
docker compose -f docker-compose.production.yml --env-file .env.production --profile direct up --build
```

- App Dockerfiles (`apps/wallow-*/Dockerfile`) build from the **repo root** context so
  `workspace:*` deps resolve; only `docker/docs/` and `docker/images/` build from here.
- Path-based deployments must `up --build` — `AUTH_BASE_PATH` is a **build** arg, not runtime
  env (see the production compose header comment).
- Adding a `${VAR}` to a compose file requires an entry in its paired `.env.example`;
  `pnpm lint:env` fails otherwise. Pairings and semantics live in `scripts/check-env.sh`'s
  comments and `pairs` list.
- `seed.production.json` IS committed and secret-less by design: secrets are `ClientSecrets__*`
  env vars, and it carries no admin block — first-run setup bootstraps the admin. Never "fix"
  the bare `${BCORDES_*}` vars with defaults (deliberate fail-closed), and never commit a real
  `.env` / `.env.production`.
- The API container entrypoint is `api/src/Wallow.Api/entrypoint.sh`, wired via
  `ContainerEntrypoint` — no compose file references it, so grepping `docker/` won't find it.
- A new secret in `.env.production.example` also needs a `secret_for` case in
  `scripts/prod-secrets.sh`.
