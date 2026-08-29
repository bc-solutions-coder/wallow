# docker — Compose Stacks Agent Guide

Four independent compose files. Always run them from **this directory** — the relative build
contexts (`./images/`, `..`) depend on it.

| File | Stack |
|------|-------|
| `docker-compose.yml` | Dev infrastructure only — Postgres, Valkey, Mailpit, Garage (S3), Alloy + Grafana LGTM, the DocFX docs site; ClamAV behind `--profile clamav`. No app containers. |
| `docker-compose.test.yml` | Containerised E2E stack — the same infra plus `wallow-migrations` / `wallow-seeder` / `wallow-api` (prebuilt `*:test` images) and the `wallow-auth` / `wallow-web` / `bff-example` Node apps. Driven by `./scripts/e2e.sh`, not by hand. |
| `docker-compose.production.yml` | Full production topology — pulls `ghcr.io/bc-solutions-coder/*` images, adds a Postgres replica, hardened API settings and two profile-gated edges (Caddy via `--profile direct`, a Pangolin newt tunnel via `--profile pangolin`). Its header comment documents path-based vs subdomain routing. |
| `docker-compose.pangolin.yml` | The production `newt` tunnel client as its own stack, joined to the production stack's network via `external: true` (`WALLOW_NETWORK`). For stack managers (Dockhand, Portainer) where per-deploy `--profile` flags are awkward. Run it INSTEAD of `--profile pangolin`, never alongside — both copies name their container `wallow-newt` so doubling up fails fast. The `pangolin.*` labels stay on the production file's app services; newt reads them via the Docker socket regardless of which project it runs in. |

```bash
cp .env.example .env        # required before the dev stack; GF_ADMIN_PASSWORD must be set
docker compose up -d        # or: pnpm backend:infra from the repo root

pnpm secrets:prod           # from the repo root — writes docker/.env.production
# pick ONE edge profile: `direct` (Caddy on :80/:443) or `pangolin` (newt tunnel)
docker compose -f docker-compose.production.yml --env-file .env.production --profile direct up --build
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
- **Ingress (production stack only)** — two mutually exclusive edge profiles; a deployment
  enables exactly one, and with neither profile nothing external reaches the apps (they
  publish on `127.0.0.1` for debugging only). In **both** modes the `/api` and `/auth`
  prefixes are **kept**, never stripped — each app rebases itself — and `AUTH_BASE_PATH` is a
  **build** arg, not a runtime env, so path-based deployments must `up --build`; the published
  `wallow-auth` image is root-mounted.
  - `--profile direct`: the `caddy` service owns `:80`/`:443` and terminates TLS. Routing
    lives in `caddy/Caddyfile.example` (copy it and point `CADDYFILE_HOST_PATH` at your copy).
  - `--profile pangolin`: the `newt` tunnel client runs inside the stack on the `wallow`
    network (no host ports; a Pangolin instance terminates TLS) and reads the Docker socket.
    The whole Pangolin resource — `PANGOLIN_RESOURCE_DOMAIN` plus the `/api`, `/auth`, and
    catch-all targets — is declared as `pangolin.*` **blueprint labels** on the three app
    services, applied continuously: the compose file is the source of truth and dashboard
    edits are overwritten. Targets resolve by container DNS name, never bridge IPs (those are
    reassigned on every recreate), which is also why a host-level newt (systemd) cannot work
    and must be disabled — two newts on one site steal the tunnel from each other.
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
