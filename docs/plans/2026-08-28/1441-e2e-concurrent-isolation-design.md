**status: completed**

# Design: Isolate concurrent `e2e.sh` runs (Wallow-joo0)

**Bead:** Wallow-joo0 · **Implementation plan:** `docs/plans/2026-08-28/1442-e2e-concurrent-isolation-plan.md`

## Problem

`Wallow-kd2e` scoped the e2e stack to Compose project `wallow-test` so teardown can no longer
remove the dev-infra stack. But two `./scripts/e2e.sh` invocations running **concurrently** still
destroy each other:

1. **Shared project name.** Both runs are `--project-name wallow-test`, so the second run's
   initial `down -v --remove-orphans` (scripts/e2e.sh:86) wipes the first run's containers and
   volumes mid-suite, and whichever teardown fires first kills the other run.
2. **Fixed host ports.** `docker/docker-compose.test.yml` publishes eleven fixed host ports; the
   second run's `up` fails to bind, or worse, its suites drive the first run's stack.
3. **Shared image tags** (not in the bead text, but the realistic trigger is two agents in two
   worktrees). All seven images are `*:test`. Run B's `dotnet publish`/`compose --build` can retag
   `wallow-web-react:test` between run A's build and A's container create, so A silently runs B's
   tree.

### Inventory of the fixed surface (verified against the tree)

| Collision | Where |
|---|---|
| Project name `wallow-test` | `scripts/e2e.sh:50` (`--project-name`), compose `name:` field |
| API `5050:8080` | compose `wallow-api.ports`; baked into `OpenIddict__Issuer`, `ServiceUrls__ApiUrl`, wallow-web/bff-example `OIDC_ISSUER` + `OIDC_METADATA_URL`; `API_URL` in e2e.sh |
| Auth `127.0.0.1:5051:3002` | compose `wallow-auth.ports`; baked into API `AuthUrl`/`ServiceUrls__AuthUrl`; `AUTH_ORIGIN` in e2e.sh; hardcoded as `E2E_BASE_URL` in ci.yml:458 |
| Web `127.0.0.1:5053:3000` | compose `wallow-web.ports`; baked into seeder `Clients__0__RedirectUris__0`/`PostLogoutRedirectUris__0` and wallow-web `OIDC_REDIRECT_URI`/`OIDC_POST_LOGOUT_REDIRECT_URI`; `WEB_URL` in e2e.sh |
| bff-example `3003:3000` | compose `bff-example.ports` + its `OIDC_REDIRECT_URI`/`OIDC_POST_LOGOUT_REDIRECT_URI`; **`api/seed.json` client index 2 (`bcordes-bff`) redirect/post-logout/frontchannel-logout URIs — no seeder override exists today**; spec default in `external-origin-login.spec.ts:15` |
| Postgres `127.0.0.1:5442`, Valkey `127.0.0.1:6389`, Mailpit `127.0.0.1:1035`/`8035`, Garage `127.0.0.1:3910`/`3913` | compose ports (debug conveniences; Mailpit `8035` also feeds `E2E_MAILPIT_URL`) |
| wallow-auth dev server `3002` (local mode only) | `wallowAppConfig` reads `process.env.PORT ?? 3002` (`packages/config/src/vite/app.ts:60`); `apps/wallow-auth/playwright.config.ts` reads `PORT` too, with `reuseExistingServer: true` — a second concurrent local run **adopts** the first run's dev server, whose proxy targets the first run's API |
| Image tags `*:test` | compose `image:` fields (7 services); `-p:ContainerImageTag=test` in e2e.sh publish loop; ci.yml builds/saves/loads the same tags |

Everything downstream already reads env: `E2E_BASE_URL`, `WALLOW_API_INTERNAL_URL`,
`E2E_MAILPIT_URL`, `E2E_AUTH_ORIGIN`, `E2E_BFF_EXAMPLE_URL`, `PORT`. The gaps are that e2e.sh
computes them from literals, never passes `E2E_BFF_EXAMPLE_URL` or `PORT`, and CI hardcodes
`E2E_BASE_URL`.

## Goals

- Two `./scripts/e2e.sh` invocations running simultaneously both pass; neither disturbs the
  other's containers, volumes, images, or the dev-infra stack (the bead's acceptance).
- Isolation is the **default** — no opt-in flag, or plain concurrent invocations still collide.
- Running `docker compose -f docker-compose.test.yml -p wallow-test up` **by hand** keeps
  today's exact behavior: every new `${VAR}` defaults to the classic fixed value.
- CI keeps working with its cache-restored `*:test` images, unchanged tags.

## Non-goals / accepted limitations

- **Host-side build races in one worktree are out of scope.** Two concurrent runs in the *same*
  worktree share `bin/obj` (`dotnet build`) and package `dist/` (local-mode `pnpm build`) — those
  races predate this bead and are a caller concern. The supported same-worktree concurrency mode
  is `E2E_SKIP_IMAGE_BUILD=1` + container mode against prebuilt images; full isolation including
  builds is the two-worktree case, which this design fully supports.
- **SIGKILL leaks.** A run killed with SIGKILL (trap never fires) leaves a running
  `wallow-test-<id>` stack and a `test-<id>` image tag set. The stale-stack sweep (below) reclaims
  it on any later run once its containers have exited; a still-running orphan is indistinguishable
  from an active run and is left for manual `docker compose ls` cleanup. Stale per-run image tags
  cost only tag refs (layers are shared) and fall to `docker image prune`.
- No change to which interfaces ports bind (`127.0.0.1:` prefixes stay exactly as they are).

## Design

### 1. Per-run stack identity: `E2E_STACK_ID`

`E2E_STACK_ID` defaults to the script's PID (`$$`) — unique among concurrent runs on one machine,
short, and a valid Compose project-name fragment. The Compose project becomes
`wallow-test-$E2E_STACK_ID`. Overriding it must use lowercase alphanumerics/`-`/`_` (Compose
project-name rules). The compose file's `name: wallow-test` stays as the by-hand fallback.

Because every run now has fresh project-scoped volumes by construction, the old "clean any prior
e2e stack" step loses its original purpose (guaranteeing a fresh DB for the seeder) and is
repurposed as the stale-stack sweep (§5).

### 2. Host ports: kernel-allocated, env-substituted

Every host port in `docker-compose.test.yml` becomes `${VAR:-<classic default>}`:

| Var | Classic default | Service |
|---|---|---|
| `E2E_API_PORT` | 5050 | wallow-api |
| `E2E_AUTH_PORT` | 5051 | wallow-auth |
| `E2E_WEB_PORT` | 5053 | wallow-web |
| `E2E_BFF_PORT` | 3003 | bff-example |
| `E2E_POSTGRES_PORT` | 5442 | postgres |
| `E2E_VALKEY_PORT` | 6389 | valkey |
| `E2E_MAILPIT_SMTP_PORT` | 1035 | mailpit |
| `E2E_MAILPIT_HTTP_PORT` | 8035 | mailpit |
| `E2E_GARAGE_S3_PORT` | 3910 | garage |
| `E2E_GARAGE_ADMIN_PORT` | 3913 | garage |

e2e.sh allocates free ports for every var the caller left unset, in **one** `python3` pass that
holds all sockets open until every port is chosen (so the kernel cannot hand out a duplicate
within the run), then exports them for compose interpolation. The window between release and
compose's bind is accepted: kernel ephemeral assignment does not immediately reuse, and two
overlapping allocators hold their sockets concurrently so they get disjoint ports. Any var can be
pinned by exporting it (`E2E_API_PORT=5050 ./scripts/e2e.sh`). An eleventh allocated port serves
as the local-mode wallow-auth dev-server `PORT`.

`python3` is a new (soft) dependency of e2e.sh — present on dev machines and `ubuntu-latest`.

### 3. Port-coherent URLs inside the compose file

Every URL that embeds one of those ports is rewritten to interpolate the same var, so browser-
facing OIDC stays coherent per run:

- `wallow-api`: `OpenIddict__Issuer` + `ServiceUrls__ApiUrl` → `http://localhost:${E2E_API_PORT:-5050}`;
  `AuthUrl` + `ServiceUrls__AuthUrl` → `http://localhost:${E2E_AUTH_PORT:-5051}`.
- `wallow-seeder`: `Clients__0__*` → `${E2E_WEB_PORT:-5053}`; **new** `Clients__2__RedirectUris__0`,
  `Clients__2__PostLogoutRedirectUris__0`, `Clients__2__FrontchannelLogoutUri` → `${E2E_BFF_PORT:-3003}`,
  overriding `api/seed.json`'s hardcoded `bcordes-bff` URIs (index 2; the seeder's config-based
  positional override mechanism already exists — `api/src/Wallow.SeederService/Program.cs:19`).
- `wallow-web`: `OIDC_ISSUER`/`OIDC_METADATA_URL` → API port; `OIDC_REDIRECT_URI`/
  `OIDC_POST_LOGOUT_REDIRECT_URI` → web port.
- `bff-example`: `OIDC_ISSUER`/`OIDC_METADATA_URL` → API port; redirect URIs → bff port.

Per `docker/CLAUDE.md`, every new `${VAR}` gets a commented entry in `docker/.env.example`
(`# E2E_API_PORT=5050` — `scripts/check-env.sh` accepts `^#? *NAME=`), or `pnpm lint:env` fails.

### 4. Image tags: `E2E_IMAGE_TAG`

All seven `image:` fields become `wallow-*:${E2E_IMAGE_TAG:-test}`. e2e.sh resolves the tag:

- caller-set `E2E_IMAGE_TAG` → used verbatim;
- else `E2E_SKIP_IMAGE_BUILD=1` → `test` (CI and local reuse keep consuming the plain tags);
- else → `test-$E2E_STACK_ID`, and teardown untags those seven refs after `down` (`docker image rm`,
  best-effort; layer cache makes the next run's rebuild cheap, and shared layers mean untagging
  costs nothing).

The publish loop passes `-p:ContainerImageTag=$E2E_IMAGE_TAG`. ci.yml's image jobs are untouched:
they build/save/load plain `:test`, and the e2e job's `E2E_SKIP_IMAGE_BUILD=1` resolves to `test`.

### 5. Stale-stack sweep (replaces the "clean any prior stack" step)

At startup, e2e.sh:

1. `down -v --remove-orphans` on **its own** project (idempotence when `E2E_STACK_ID` is pinned);
2. lists Compose projects (`docker compose ls -a --format json`) whose name is `wallow-test` or
   starts with `wallow-test-`, skips its own, and for each with **no** container in
   `running`/`created`/`restarting`/`paused` state runs `down -v --remove-orphans` on it.

A concurrent healthy run always has containers in those states (and a run in its pre-`up` gap has
no containers at all, so it isn't listed) — the sweep can only reclaim genuinely dead stacks,
including a legacy plain `wallow-test` one.

### 6. URL threading out of e2e.sh

All script-level URLs derive from the chosen ports: `API_URL`, `WEB_URL`, `MAILPIT_URL`
(`127.0.0.1:$E2E_MAILPIT_HTTP_PORT`), `AUTH_ORIGIN` (`localhost:$E2E_AUTH_PORT`), and new
`BFF_EXAMPLE_URL` (`localhost:$E2E_BFF_PORT`). Two threading gaps close:

- the cross-app suite invocation gains `E2E_BFF_EXAMPLE_URL=$BFF_EXAMPLE_URL` (the spec's
  existing env override, today never set by the runner);
- the local-mode wallow-auth invocation gains `PORT=$AUTH_DEV_PORT` (the allocated 11th port), and
  `apps/wallow-auth/playwright.config.ts` passes `PORT` through to its `webServer` child
  explicitly so the spawned `pnpm dev` claims the same port Playwright waits on.

`E2E_KEEP_STACK=1` prints the project name, the four app URLs, and the exact manual teardown
command, since none of them are guessable any more.

### 7. Container-mode inference (CI contract change)

Today CI sets `E2E_BASE_URL: http://localhost:5051`, duplicating knowledge of the auth port. New
rule in e2e.sh: if `E2E_BASE_URL` is unset **and** `E2E_UP_SERVICE=wallow-auth`, the script sets
`E2E_BASE_URL=http://localhost:$E2E_AUTH_PORT` itself. ci.yml drops its `E2E_BASE_URL` line. A
caller-supplied `E2E_BASE_URL` still wins (drive a genuinely external app).

## Alternatives considered

- **Opt-in isolation flag, defaults unchanged** — rejected: fails the acceptance criterion (two
  *plain* invocations must pass concurrently).
- **Deterministic port base derived from the stack ID** (e.g. `20000 + hash(id) * 25`) —
  rejected: collisions with unrelated services need detect-and-retry anyway; kernel allocation is
  simpler and race-free within a run.
- **Compose ephemeral ports (`"127.0.0.1::8080"`) read back via `docker compose port`** —
  rejected: the browser-facing issuer and redirect URIs must be known *before* `up` (they are
  baked into the API/seeder env), which forces choose-then-up.
- **Per-run image tags always, CI included** — rejected: CI's cache-restored `:test` images would
  never match; gating the auto-suffix on "actually building" keeps CI byte-identical.

## Acceptance verification

With images prebuilt once (`./scripts/e2e.sh` normally, or `E2E_SKIP_IMAGE_BUILD=1` after a
build), run two overlapping invocations from one worktree:

```bash
E2E_SKIP_IMAGE_BUILD=1 E2E_UP_SERVICE=wallow-auth ./scripts/e2e.sh &
sleep 5
E2E_SKIP_IMAGE_BUILD=1 E2E_UP_SERVICE=wallow-auth ./scripts/e2e.sh
wait
```

Both must pass; afterwards `docker compose ls` must show the dev-infra stack (if it was up)
untouched and no `wallow-test*` projects remaining. Container mode is used for both so the run
exercises stack isolation without the out-of-scope host-side build races.
