**status: completed**

# Concurrent e2e.sh Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two `./scripts/e2e.sh` invocations running simultaneously both pass, with neither disturbing the other's containers, volumes, images, or the dev-infra stack (Wallow-joo0).

**Architecture:** Per-run Compose project name (`wallow-test-$E2E_STACK_ID`), kernel-allocated host ports exported into env-substituted `${VAR:-classic-default}` compose interpolations (ports *and* the OIDC URLs that embed them), per-run image tags when the run builds its own images, and a startup sweep that reclaims only dead `wallow-test*` stacks.

**Tech Stack:** bash + docker compose v2 interpolation, python3 (port allocation / JSON parsing), Playwright env knobs, .NET SDK container publish (`-p:ContainerImageTag`).

**Spec:** `docs/plans/2026-08-28/1441-e2e-concurrent-isolation-design.md` — read it first; it carries the collision inventory, the decisions, and the rejected alternatives.

## Global Constraints

- Running `docker compose -f docker/docker-compose.test.yml -p wallow-test up` by hand must keep today's exact behavior: every new `${VAR}` defaults to the classic fixed value (5050, 5051, 5053, 3003, 5442, 6389, 1035, 8035, 3910, 3913, tag `test`).
- Every `${VAR}` added to `docker/docker-compose.test.yml` needs a (commented) entry in `docker/.env.example` — `pnpm lint:env` fails otherwise (`scripts/check-env.sh` accepts `^#? *NAME=`).
- Keep every existing `127.0.0.1:` bind prefix exactly as it is; do not add or remove any.
- CI must keep consuming its cache-restored plain `*:test` images unchanged (`E2E_SKIP_IMAGE_BUILD=1` must resolve the image tag to `test`).
- Bash must stay portable to macOS's bash 3.2: no `mapfile`, no `${var,,}`, keep `set -euo pipefail`.
- Conventional commits, lowercase imperative, first line < 72 chars.
- The e2e suites themselves need no spec changes — `E2E_BASE_URL`, `WALLOW_API_INTERNAL_URL`, `E2E_MAILPIT_URL`, `E2E_AUTH_ORIGIN`, `E2E_BFF_EXAMPLE_URL`, `PORT` are all already read from env; this plan only changes who supplies them.

---

### Task 1: Parameterize docker-compose.test.yml + document the knobs

**Files:**
- Modify: `docker/docker-compose.test.yml`
- Modify: `docker/.env.example`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: the env-var names Task 2's script exports — `E2E_API_PORT`, `E2E_AUTH_PORT`, `E2E_WEB_PORT`, `E2E_BFF_PORT`, `E2E_POSTGRES_PORT`, `E2E_VALKEY_PORT`, `E2E_MAILPIT_SMTP_PORT`, `E2E_MAILPIT_HTTP_PORT`, `E2E_GARAGE_S3_PORT`, `E2E_GARAGE_ADMIN_PORT`, `E2E_IMAGE_TAG` — spelled exactly like this.

- [ ] **Step 1: Capture the baseline render**

Run: `docker compose -f docker/docker-compose.test.yml -p wallow-test config > /tmp/claude-1000/-home-bcordes-Wallow/e44c144b-2b07-42e0-9c5c-0e2a9dc1005e/scratchpad/compose-before.yml`
Expected: exits 0.

- [ ] **Step 2: Edit `docker/docker-compose.test.yml`**

Apply exactly these substitutions (values shown are the complete new lines):

*Image fields (7 services):*
```yaml
  garage:
    image: wallow-garage:${E2E_IMAGE_TAG:-test}
  wallow-migrations:
    image: wallow-migrations:${E2E_IMAGE_TAG:-test}
  wallow-seeder:
    image: wallow-seeder:${E2E_IMAGE_TAG:-test}
  wallow-api:
    image: wallow-api:${E2E_IMAGE_TAG:-test}
  wallow-auth:
    image: wallow-auth-react:${E2E_IMAGE_TAG:-test}
  wallow-web:
    image: wallow-web-react:${E2E_IMAGE_TAG:-test}
  bff-example:
    image: wallow-bff-example:${E2E_IMAGE_TAG:-test}
```

*Port mappings (keep each existing `127.0.0.1:` prefix — wallow-api and bff-example have none today and stay that way):*
```yaml
  postgres:      - "127.0.0.1:${E2E_POSTGRES_PORT:-5442}:5432"
  valkey:        - "127.0.0.1:${E2E_VALKEY_PORT:-6389}:6379"
  mailpit:       - "127.0.0.1:${E2E_MAILPIT_SMTP_PORT:-1035}:1025"
                 - "127.0.0.1:${E2E_MAILPIT_HTTP_PORT:-8035}:8025"
  garage:        - "127.0.0.1:${E2E_GARAGE_S3_PORT:-3910}:3900"
                 - "127.0.0.1:${E2E_GARAGE_ADMIN_PORT:-3913}:3903"
  wallow-api:    - "${E2E_API_PORT:-5050}:8080"
  wallow-auth:   - "127.0.0.1:${E2E_AUTH_PORT:-5051}:3002"
  wallow-web:    - "127.0.0.1:${E2E_WEB_PORT:-5053}:3000"
  bff-example:   - "${E2E_BFF_PORT:-3003}:3000"
```

*URL-valued environment (the ports above are baked into OIDC URLs; each must interpolate the same var):*

`wallow-seeder.environment` — replace the two `Clients__0__*` values and add three `Clients__2__*` keys (bcordes-bff is index 2 in `api/seed.json`'s clients array; its seeded URIs hardcode `localhost:3003` and the seeder's positional env override — `api/src/Wallow.SeederService/Program.cs` — is the existing mechanism):
```yaml
      Clients__0__RedirectUris__0: "http://localhost:${E2E_WEB_PORT:-5053}/bff/callback"
      Clients__0__PostLogoutRedirectUris__0: "http://localhost:${E2E_WEB_PORT:-5053}/"
      # bcordes-bff (seed.json clients index 2) hardcodes localhost:3003; rebase it
      # onto this run's bff-example host port so the external-origin consent spec
      # still round-trips when scripts/e2e.sh randomizes ports (Wallow-joo0).
      Clients__2__RedirectUris__0: "http://localhost:${E2E_BFF_PORT:-3003}/bff/callback"
      Clients__2__PostLogoutRedirectUris__0: "http://localhost:${E2E_BFF_PORT:-3003}/"
      Clients__2__FrontchannelLogoutUri: "http://localhost:${E2E_BFF_PORT:-3003}/bff/frontchannel-logout"
```

`wallow-api.environment`:
```yaml
      AuthUrl: "http://localhost:${E2E_AUTH_PORT:-5051}"
      ServiceUrls__AuthUrl: "http://localhost:${E2E_AUTH_PORT:-5051}"
      ServiceUrls__ApiUrl: "http://localhost:${E2E_API_PORT:-5050}"
      OpenIddict__Issuer: "http://localhost:${E2E_API_PORT:-5050}"
```

`wallow-web.environment`:
```yaml
      OIDC_ISSUER: "http://localhost:${E2E_API_PORT:-5050}"
      OIDC_METADATA_URL: "http://host.docker.internal:${E2E_API_PORT:-5050}/.well-known/openid-configuration"
      OIDC_REDIRECT_URI: "http://localhost:${E2E_WEB_PORT:-5053}/bff/callback"
      OIDC_POST_LOGOUT_REDIRECT_URI: "http://localhost:${E2E_WEB_PORT:-5053}/"
```

`bff-example.environment`:
```yaml
      OIDC_ISSUER: "http://localhost:${E2E_API_PORT:-5050}"
      OIDC_METADATA_URL: "http://host.docker.internal:${E2E_API_PORT:-5050}/.well-known/openid-configuration"
      OIDC_REDIRECT_URI: "http://localhost:${E2E_BFF_PORT:-3003}/bff/callback"
      OIDC_POST_LOGOUT_REDIRECT_URI: "http://localhost:${E2E_BFF_PORT:-3003}/"
```

Also extend the file's header comment (the `name: wallow-test` note): after the existing text, add:
```yaml
# Every host port and the URLs that embed one interpolate ${E2E_*_PORT:-<classic
# default>}, and every image tag interpolates ${E2E_IMAGE_TAG:-test} — run by
# hand you get exactly the classic values; scripts/e2e.sh exports per-run values
# so concurrent runs cannot collide (Wallow-joo0). The knob list lives in
# .env.example.
```

- [ ] **Step 3: Add the knobs to `docker/.env.example`**

Append at the end of the file:
```bash

# Containerised E2E stack (docker-compose.test.yml — driven by scripts/e2e.sh).
# Every knob defaults to the classic fixed value when the compose file is run by
# hand; scripts/e2e.sh exports per-run values (free ports, a per-run image tag)
# so concurrent runs stay isolated (Wallow-joo0). Export one before running the
# script to pin it.
# E2E_IMAGE_TAG=test
# E2E_API_PORT=5050
# E2E_AUTH_PORT=5051
# E2E_WEB_PORT=5053
# E2E_BFF_PORT=3003
# E2E_POSTGRES_PORT=5442
# E2E_VALKEY_PORT=6389
# E2E_MAILPIT_SMTP_PORT=1035
# E2E_MAILPIT_HTTP_PORT=8035
# E2E_GARAGE_S3_PORT=3910
# E2E_GARAGE_ADMIN_PORT=3913
```

- [ ] **Step 4: Verify the default render is unchanged (plus only the three new seeder keys)**

Run:
```bash
docker compose -f docker/docker-compose.test.yml -p wallow-test config > /tmp/claude-1000/-home-bcordes-Wallow/e44c144b-2b07-42e0-9c5c-0e2a9dc1005e/scratchpad/compose-after.yml
diff /tmp/claude-1000/-home-bcordes-Wallow/e44c144b-2b07-42e0-9c5c-0e2a9dc1005e/scratchpad/compose-before.yml /tmp/claude-1000/-home-bcordes-Wallow/e44c144b-2b07-42e0-9c5c-0e2a9dc1005e/scratchpad/compose-after.yml
```
Expected: the ONLY diff hunks are the three added `Clients__2__*` lines under wallow-seeder. Any other diff means a default was mistyped — fix before proceeding.

- [ ] **Step 5: Verify an overridden render lands everywhere**

Run (override values deliberately share no substring with any classic value, so a plain grep for the classics is conclusive):
```bash
E2E_API_PORT=9101 E2E_AUTH_PORT=9102 E2E_WEB_PORT=9103 E2E_BFF_PORT=9104 \
E2E_POSTGRES_PORT=9105 E2E_VALKEY_PORT=9106 E2E_MAILPIT_SMTP_PORT=9107 \
E2E_MAILPIT_HTTP_PORT=9108 E2E_GARAGE_S3_PORT=9109 E2E_GARAGE_ADMIN_PORT=9110 \
E2E_IMAGE_TAG=test-xyz \
docker compose -f docker/docker-compose.test.yml -p wallow-test-x config \
  | grep -nE "5050|5051|5053|3003|5442|6389|1035|8035|3910|3913|wallow-[a-z-]+:test\"?$"
```
Expected: no output, grep exits 1 (no classic port and no un-suffixed `:test` image survives an override).

- [ ] **Step 6: Run the env-doc lint**

Run: `pnpm lint:env`
Expected: `docker-compose.test.yml -> .env.example: ok` (all pairs ok, exit 0).

- [ ] **Step 7: Commit**

```bash
git add docker/docker-compose.test.yml docker/.env.example
git commit -m "feat(e2e): parameterize test-stack host ports, image tags and OIDC urls (Wallow-joo0)"
```

---

### Task 2: Pass the dev-server port through wallow-auth's Playwright webServer

**Files:**
- Modify: `apps/wallow-auth/playwright.config.ts`

**Interfaces:**
- Consumes: nothing new — `port` is already `Number(process.env.PORT ?? 3002)` in this file, and `wallowAppConfig` (`packages/config/src/vite/app.ts:60`) already reads `process.env.PORT` for vite's `server.port`.
- Produces: the contract Task 3 relies on — `PORT=<n>` in the suite's environment makes both Playwright's wait-port **and** the spawned `pnpm dev` bind `<n>`.

- [ ] **Step 1: Edit the webServer block**

Replace the current block (lines ~6–28):
```ts
// When E2E_BASE_URL points at an already-running app — the wallow-auth container
// the compose stack serves in CI (scripts/e2e.sh) — Playwright drives that URL
// directly and must NOT boot a local dev server. Left unset (the local runner's
// default) it falls back to a `pnpm dev` (`vite dev`) webServer on `port`, whose
// passthrough server routes target WALLOW_API_INTERNAL_URL.
//
// `port` and vite's `server.port` (wallowAppConfig) both read process.env.PORT,
// and the env block below passes it to the child explicitly, so the port
// Playwright waits on is always the one the dev server claims — including the
// per-run port scripts/e2e.sh allocates to keep concurrent runs apart
// (Wallow-joo0).
const externalBaseURL = process.env.E2E_BASE_URL;

const webServer: PlaywrightTestConfig["webServer"] = externalBaseURL
  ? undefined
  : {
      command: "pnpm dev",
      port,
      reuseExistingServer: true,
      env: {
        PORT: String(port),
        // Outside Aspire the proxy's default target (http://wallow-api) does not
        // resolve; point it at the locally-run API unless the caller overrides.
        WALLOW_API_INTERNAL_URL: process.env.WALLOW_API_INTERNAL_URL ?? "http://localhost:5001",
      },
    };
```
(The only functional change is `PORT: String(port)` in `env`; the comment is rewritten because its old claim — "passes no PORT to the child" — becomes false.)

- [ ] **Step 2: Verify the config still parses and the suite still lists**

Run: `pnpm --filter ./apps/wallow-auth exec playwright test --list | head -5`
Expected: spec titles print, exit 0 (listing does not boot the webServer).

- [ ] **Step 3: Verify a non-default port is honored end-to-end**

Run (needs workspace deps installed; Ctrl-C equivalent via timeout is fine — the point is the bind):
```bash
cd apps/wallow-auth && PORT=13202 timeout 25 pnpm dev & sleep 20; curl -fsS -o /dev/null -w "%{http_code}\n" http://localhost:13202/login; wait || true
```
Expected: an HTTP status (200) from :13202, proving vite honors `PORT`. (This checks the `wallowAppConfig` half; the Playwright half is exercised by Task 6's full run.)

- [ ] **Step 4: Commit**

```bash
git add apps/wallow-auth/playwright.config.ts
git commit -m "fix(e2e): pass the dev-server port through wallow-auth's playwright webserver"
```

---

### Task 3: Rewrite scripts/e2e.sh for per-run isolation

**Files:**
- Modify: `scripts/e2e.sh`

**Interfaces:**
- Consumes: Task 1's env-var names (exact spellings above); Task 2's `PORT` contract.
- Produces: the runner CI and humans call. New/changed knobs: `E2E_STACK_ID` (default `$$`), `E2E_IMAGE_TAG` (default `test` when `E2E_SKIP_IMAGE_BUILD` set, else `test-$E2E_STACK_ID`), the ten `E2E_*_PORT` vars (default: allocated free ports), and `E2E_BASE_URL` now **inferred** as `http://localhost:$E2E_AUTH_PORT` when unset and `E2E_UP_SERVICE=wallow-auth` (Task 4 depends on exactly this inference).

- [ ] **Step 1: Replace the identity/URL section (current lines 44–68)**

After `COMPOSE_FILE=...`, replace everything down to (and including) the `AUTH_ORIGIN=` line with:

```bash
# --- Per-run stack identity (Wallow-joo0) -----------------------------------
# Every invocation gets its own Compose project, host ports and (when it builds
# its own images) image tags, so two concurrent runs cannot see each other's
# stack — and, as before (Wallow-kd2e), can never touch the dev-infra stack.
# E2E_STACK_ID defaults to this shell's PID: unique per concurrent run on one
# machine and a valid Compose project-name fragment (an override must stick to
# lowercase alphanumerics, '-' and '_').
E2E_STACK_ID="${E2E_STACK_ID:-$$}"
PROJECT_NAME="wallow-test-${E2E_STACK_ID}"
# --project-name pins the Compose project even if the caller's environment (or
# docker/.env, which COMPOSE_PROJECT_NAME-scopes the dev-infra stack) would
# override the compose file's top-level `name:`.
COMPOSE=(docker compose --project-name "$PROJECT_NAME" -f "$COMPOSE_FILE")

UP_SERVICE="${E2E_UP_SERVICE:-wallow-api}"

# --- Host ports ---------------------------------------------------------------
# docker-compose.test.yml publishes every host port as ${E2E_*_PORT:-classic
# default}. Allocate a free port for each var the caller left unset, in ONE
# python pass that holds all sockets open until every port is chosen, so the
# kernel cannot hand out a duplicate within this run. The window between release
# and compose's bind is accepted: overlapping runs hold their sockets
# concurrently, so they receive disjoint ports.
alloc_ports() {
  python3 - "$1" <<'PY'
import socket
import sys

count = int(sys.argv[1])
socks = [socket.socket() for _ in range(count)]
for s in socks:
    s.bind(("127.0.0.1", 0))
for s in socks:
    print(s.getsockname()[1])
    s.close()
PY
}

FREE_PORTS=()
while IFS= read -r free_port; do
  FREE_PORTS+=("$free_port")
done < <(alloc_ports 11)

E2E_API_PORT="${E2E_API_PORT:-${FREE_PORTS[0]}}"
E2E_AUTH_PORT="${E2E_AUTH_PORT:-${FREE_PORTS[1]}}"
E2E_WEB_PORT="${E2E_WEB_PORT:-${FREE_PORTS[2]}}"
E2E_BFF_PORT="${E2E_BFF_PORT:-${FREE_PORTS[3]}}"
E2E_POSTGRES_PORT="${E2E_POSTGRES_PORT:-${FREE_PORTS[4]}}"
E2E_VALKEY_PORT="${E2E_VALKEY_PORT:-${FREE_PORTS[5]}}"
E2E_MAILPIT_SMTP_PORT="${E2E_MAILPIT_SMTP_PORT:-${FREE_PORTS[6]}}"
E2E_MAILPIT_HTTP_PORT="${E2E_MAILPIT_HTTP_PORT:-${FREE_PORTS[7]}}"
E2E_GARAGE_S3_PORT="${E2E_GARAGE_S3_PORT:-${FREE_PORTS[8]}}"
E2E_GARAGE_ADMIN_PORT="${E2E_GARAGE_ADMIN_PORT:-${FREE_PORTS[9]}}"
# Local mode only: the port the wallow-auth `pnpm dev` webServer binds.
AUTH_DEV_PORT="${FREE_PORTS[10]}"
export E2E_API_PORT E2E_AUTH_PORT E2E_WEB_PORT E2E_BFF_PORT E2E_POSTGRES_PORT \
  E2E_VALKEY_PORT E2E_MAILPIT_SMTP_PORT E2E_MAILPIT_HTTP_PORT \
  E2E_GARAGE_S3_PORT E2E_GARAGE_ADMIN_PORT

# --- Image tag ----------------------------------------------------------------
# The compose file's image fields interpolate ${E2E_IMAGE_TAG:-test}. A run that
# builds its own images tags them per-run (test-$E2E_STACK_ID) so a concurrent
# run in another worktree can't retag the images under it mid-run; teardown
# untags them. A run that REUSES images (E2E_SKIP_IMAGE_BUILD=1 — CI, or a local
# caller after a prior build) resolves to the plain `test` tags those builds
# produce. An explicit E2E_IMAGE_TAG wins over both.
IMAGE_TAG_GENERATED=""
if [[ -z "${E2E_IMAGE_TAG:-}" ]]; then
  if [[ -n "${E2E_SKIP_IMAGE_BUILD:-}" ]]; then
    E2E_IMAGE_TAG="test"
  else
    E2E_IMAGE_TAG="test-${E2E_STACK_ID}"
    IMAGE_TAG_GENERATED=1
  fi
fi
export E2E_IMAGE_TAG

# --- URLs derived from this run's ports --------------------------------------
API_URL="http://localhost:${E2E_API_PORT}"
DISCOVERY_URL="$API_URL/.well-known/openid-configuration"
WEB_URL="http://localhost:${E2E_WEB_PORT}"
BFF_EXAMPLE_URL="http://localhost:${E2E_BFF_PORT}"
# Two more endpoints the backend-dependent wallow-auth specs need, and which only
# this script can know. Both serving modes below drive the CONTAINERISED backend,
# so both get these — E2E_BASE_URL picks the app's serving mode, not the
# backend's.
#   Mailpit HTTP: the API's Smtp__Host points at the same mailpit container this
#     port publishes.
#   Auth origin: the API's own configured AuthUrl in that stack, which
#     OpenIddictRedirectUriValidator allow-lists unconditionally.
MAILPIT_URL="http://127.0.0.1:${E2E_MAILPIT_HTTP_PORT}"
AUTH_ORIGIN="http://localhost:${E2E_AUTH_PORT}"

# Container mode for the wallow-auth suite is implied by bringing that service
# up; the auth port is per-run, so the caller can no longer be expected to spell
# the URL (ci.yml used to hardcode :5051). An explicit E2E_BASE_URL still wins —
# that is the knob for driving a genuinely external, already-running app.
if [[ -z "${E2E_BASE_URL:-}" && "$UP_SERVICE" == "wallow-auth" ]]; then
  E2E_BASE_URL="$AUTH_ORIGIN"
fi
```

- [ ] **Step 2: Replace teardown and the cleanup step (current lines 70–86)**

```bash
log() { printf '\n=== %s ===\n' "$1"; }

PER_RUN_IMAGES=(
  "wallow-api" "wallow-migrations" "wallow-seeder" "wallow-auth-react"
  "wallow-web-react" "wallow-bff-example" "wallow-garage"
)

teardown() {
  if [[ -n "${E2E_KEEP_STACK:-}" ]]; then
    log "E2E_KEEP_STACK set — leaving the stack up (project $PROJECT_NAME)"
    echo "  api $API_URL · auth $AUTH_ORIGIN · web $WEB_URL · bff-example $BFF_EXAMPLE_URL"
    echo "  mailpit $MAILPIT_URL"
    echo "  teardown: docker compose -p $PROJECT_NAME -f $COMPOSE_FILE down -v --remove-orphans"
    return
  fi
  log "Tearing down the e2e stack ($PROJECT_NAME)"
  "${COMPOSE[@]}" down -v --remove-orphans || true
  if [[ -n "$IMAGE_TAG_GENERATED" ]]; then
    # Per-run tags are refs onto layers the next build reuses — removing them
    # reclaims nothing but the names, which is the point: they must not
    # accumulate. Best-effort; a shared layer is never deleted.
    for image in "${PER_RUN_IMAGES[@]}"; do
      docker image rm "$image:$E2E_IMAGE_TAG" > /dev/null 2>&1 || true
    done
  fi
}
trap teardown EXIT

# Fresh volumes are a per-project guarantee now — a new project name has no
# volumes to inherit, so the seeder always bootstraps admin@wallow.dev
# (Wallow-wd6n). The `down` here only matters when E2E_STACK_ID is pinned to a
# reused value. The sweep after it reclaims DEAD stacks a killed run left
# behind: a concurrent healthy run always has containers in one of the four
# live states (and a run in its pre-up gap has no containers, so compose ls
# does not list it), so only genuinely dead stacks — including a legacy plain
# `wallow-test` one — are removed.
log "Cleaning this run's project and sweeping dead e2e stacks"
"${COMPOSE[@]}" down -v --remove-orphans || true
while IFS= read -r stale_project; do
  [[ "$stale_project" == "$PROJECT_NAME" ]] && continue
  if [[ -z "$(docker ps -q \
    --filter "label=com.docker.compose.project=$stale_project" \
    --filter status=running --filter status=created \
    --filter status=restarting --filter status=paused)" ]]; then
    echo "removing dead e2e stack: $stale_project"
    docker compose --project-name "$stale_project" -f "$COMPOSE_FILE" \
      down -v --remove-orphans || true
  fi
done < <(docker compose ls -a --format json | python3 -c '
import json
import sys

# Compose versions differ on whether --format json emits one array or NDJSON
# lines; accept both.
raw = sys.stdin.read().strip()
if not raw:
    sys.exit(0)
if raw.startswith("["):
    projects = json.loads(raw)
else:
    projects = [json.loads(line) for line in raw.splitlines() if line.strip()]
for project in projects:
    name = project.get("Name", "")
    if name == "wallow-test" or name.startswith("wallow-test-"):
        print(name)
')
```

- [ ] **Step 3: Thread the tag through the publish loop**

In the `dotnet publish` loop, change the tag argument to:
```bash
      -p:ContainerImageTag="$E2E_IMAGE_TAG" -p:ContainerRuntimeIdentifier="$RID"
```
and update the log line to `log "Publishing API / migration / seeder container images (:$E2E_IMAGE_TAG, $RID)"`.

- [ ] **Step 4: Thread ports/URLs into the suite invocations**

In the local-mode branch, change the last line of the branch to also pass the dev-server port:
```bash
  E2E_ENV+=("WALLOW_API_INTERNAL_URL=$API_URL" "PORT=$AUTH_DEV_PORT")
```
and in the branch's comment block, note beside the existing `reuseExistingServer` caveat:
```bash
  # PORT is this run's allocated dev-server port: playwright.config.ts waits on
  # it and passes it to the `pnpm dev` child, so two concurrent local-mode runs
  # cannot adopt each other's dev server via reuseExistingServer.
```

Change the cross-app invocation to also name this run's bff-example origin:
```bash
log "Running the wallow-web cross-app login journey suite"
env "E2E_BASE_URL=$WEB_URL" "E2E_BFF_EXAMPLE_URL=$BFF_EXAMPLE_URL" \
  pnpm --filter ./apps/wallow-web test:e2e:cross-app
```

(The wallow-web suite line `env "E2E_BASE_URL=$WEB_URL" ...` is already correct — `WEB_URL` is now per-run.)

- [ ] **Step 5: Update the header comment**

Rewrite the header's env-knob section to document the new contract (keep the serving-mode explanation, adjust ports wording):

```bash
# Env knobs:
#   E2E_STACK_ID=<id>       Per-run stack identity (default: this shell's PID).
#                           Compose project = wallow-test-<id>; lowercase
#                           alphanumerics/'-'/'_' only. Concurrent runs isolate
#                           on this plus per-run host ports (Wallow-joo0).
#   E2E_*_PORT=<n>          Pin any host port (API/AUTH/WEB/BFF/POSTGRES/VALKEY/
#                           MAILPIT_SMTP/MAILPIT_HTTP/GARAGE_S3/GARAGE_ADMIN —
#                           full list in docker/.env.example). Unset ports get a
#                           free port from the kernel each run.
#   E2E_IMAGE_TAG=<tag>     Pin the image tag. Default: `test` when
#                           E2E_SKIP_IMAGE_BUILD=1 (reuse), else test-<stack id>
#                           (built per-run, untagged at teardown).
#   E2E_SKIP_IMAGE_BUILD=1  Reuse the existing plain :test images instead of
#                           building any (dotnet publish AND compose --build).
#                           CI sets this after its image jobs; never set it just
#                           to make a local run faster.
#   E2E_UP_SERVICE=<svc>    Extra compose service to `up --wait` (default:
#                           wallow-api; CI sets wallow-auth to serve that app
#                           from a container — which also makes the script point
#                           the wallow-auth suite at that container's per-run
#                           port unless E2E_BASE_URL is set).
#   E2E_BASE_URL=<url>      Drive an already-running wallow-auth at <url>; skips
#                           `pnpm dev`. Does not affect the wallow-web suites.
#   E2E_KEEP_STACK=1        Leave the stack up after the run (for debugging —
#                           the run prints its project name, URLs and the manual
#                           teardown command).
#
# Same-worktree concurrency caveat: the compose stacks are fully isolated, but
# the HOST-side build phases (dotnet build/publish, pnpm install, workspace
# builds) share bin/obj and dist/ — run concurrent same-worktree invocations in
# container mode against prebuilt images (E2E_SKIP_IMAGE_BUILD=1
# E2E_UP_SERVICE=wallow-auth). Two worktrees need no such care.
#
# python3 is required (free-port allocation, compose-project listing).
```

- [ ] **Step 6: Syntax-check and shell-lint**

Run: `bash -n scripts/e2e.sh && (command -v shellcheck > /dev/null && shellcheck scripts/e2e.sh || echo "no shellcheck")`
Expected: exit 0; no shellcheck errors (info/style notes acceptable if pre-existing).

- [ ] **Step 7: Dry-run the pre-stack half**

Run: `E2E_SKIP_IMAGE_BUILD=1 E2E_UP_SERVICE=wallow-auth timeout 120 ./scripts/e2e.sh || true` — with images NOT yet built this fails at `up` (missing image), which is fine; the point is to observe before the failure:
Expected in output: a project name `wallow-test-<pid>`, the sweep log line, no attempt to build, and teardown naming that same project. Verify `docker compose ls -a` shows no leftover `wallow-test-*` project afterwards.

- [ ] **Step 8: Commit**

```bash
git add scripts/e2e.sh
git commit -m "feat(e2e): per-run compose project, host ports and image tags (Wallow-joo0)"
```

---

### Task 4: Drop CI's hardcoded E2E_BASE_URL

**Files:**
- Modify: `.github/workflows/ci.yml` (the `Run E2E suites` step, ~line 450)

**Interfaces:**
- Consumes: Task 3's inference — `E2E_UP_SERVICE=wallow-auth` with `E2E_BASE_URL` unset makes e2e.sh drive the auth container at its per-run port.

- [ ] **Step 1: Edit the step**

Remove the line `E2E_BASE_URL: http://localhost:5051` from the step's `env`, leaving:
```yaml
      # Serve wallow-auth from its prebuilt container and drive it directly — no
      # local dev server: e2e.sh infers the container's per-run URL from
      # E2E_UP_SERVICE=wallow-auth (Wallow-joo0). The runner brings up the
      # compose stack (including wallow-web), waits for the API + seeded admin,
      # then runs all three suites — wallow-auth, wallow-web, and the cross-app
      # login journey, which gates the full login + authenticated mutation +
      # logout loop — and tears the stack down.
      - name: Run E2E suites
        env:
          E2E_SKIP_IMAGE_BUILD: "1"
          E2E_UP_SERVICE: wallow-auth
        run: ./scripts/e2e.sh
```

- [ ] **Step 2: Validate workflow syntax**

Run: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('ok')"` (or `actionlint` if installed).
Expected: `ok`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci(e2e): let e2e.sh derive the auth suite url from e2e_up_service"
```

---

### Task 5: Update prose that states the fixed ports/project as facts

**Files:**
- Modify: `apps/wallow-web/e2e/CLAUDE.md` (`:5053` / `:3003` statements, ~lines 62–92)
- Modify: `apps/wallow-web/e2e-cross-app/external-origin-login.spec.ts` (header comment, lines ~10–23: "fixed at 3003")
- Modify: `apps/wallow-web/playwright.cross-app.config.ts` (header comment, lines ~14–16)
- Modify: `apps/wallow-auth/e2e/logout.spec.ts` (comment ~lines 19–28: ":5051 under docker-compose.test.yml")
- Modify: `apps/wallow-auth/e2e/mailpit.ts` (comment ~lines 11–31: ":8035")
- Modify: `docs/development/testing.md` (test-stack tables ~lines 289–314; CI table ~line 337 mentions `E2E_BASE_URL=http://localhost:5051` — now dropped)
- Modify: `docs/development/testing-e2e.md` (~lines 67–138: `:3003`, `:5053`, `:5050`, `:5051`)
- Modify: `docs/integrations/bff-pattern.md` (~line 113) and `docs/integrations/typescript-sdk.md` (~lines 613, 642–654) — light touch: these describe the stack's defaults

**Interfaces:** none — prose only; no code or behavior changes in this task.

- [ ] **Step 1: Reword each site**

The pattern for every edit: a port stated as a fixed fact becomes "the classic default; `scripts/e2e.sh` substitutes a free per-run port and threads it through `E2E_BASE_URL` / `E2E_BFF_EXAMPLE_URL` / `E2E_MAILPIT_URL` / `E2E_AUTH_ORIGIN`". Concretely, e.g. `external-origin-login.spec.ts`:
```ts
 * built on the same SDK, per design doc Sec 14. Its host port defaults to 3003
 * (`ports: ["${E2E_BFF_PORT:-3003}:3000"]`); `scripts/e2e.sh` allocates a per-run
 * port and passes it as `E2E_BFF_EXAMPLE_URL` (Wallow-joo0). There is no
 * equivalent under `pnpm backend` (Aspire has no bff-example service).
```
and `logout.spec.ts` / `mailpit.ts` swap "…:5051/:8035 under docker-compose.test.yml" for "…the auth/Mailpit port scripts/e2e.sh chose for this run (classic defaults 5051/8035)". In `docs/development/testing.md`, retitle the port column "Default host port" and add one sentence under the table: "`scripts/e2e.sh` overrides every host port (and the image tag) per run so concurrent runs stay isolated — see `docker/.env.example` for the `E2E_*` knobs (Wallow-joo0)." Update its CI row to drop `E2E_BASE_URL` from the listed env. Same sentence pattern for `testing-e2e.md`; in the two integration docs just annotate the shown values as the by-hand defaults.

- [ ] **Step 2: Sweep for leftovers**

Run: `grep -rnE "localhost:(5050|5051|5053|3003)|:5051\b|:5053\b" apps/*/e2e* docs/development docs/integrations scripts/e2e.sh .github/workflows/ci.yml | grep -v "plans/" | grep -vE "E2E_[A-Z_]*PORT:-|classic default|default"`
Expected: no hit that states a port as a *current fixed fact* (hits inside `${VAR:-default}` interpolations and "default"-annotated prose are correct and remain).

- [ ] **Step 3: Commit**

```bash
git add apps/wallow-web/e2e/CLAUDE.md apps/wallow-web/e2e-cross-app/external-origin-login.spec.ts \
  apps/wallow-web/playwright.cross-app.config.ts apps/wallow-auth/e2e/logout.spec.ts \
  apps/wallow-auth/e2e/mailpit.ts docs/development/testing.md docs/development/testing-e2e.md \
  docs/integrations/bff-pattern.md docs/integrations/typescript-sdk.md
git commit -m "docs(e2e): describe per-run e2e stack isolation (Wallow-joo0)"
```

---

### Task 6: Verification — single run, then the acceptance pair

**Files:** none (verification only). Docker required; expect ~10 min for the build run and ~5 min per concurrent run.

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: One full default run (local mode, builds images)**

Run: `./scripts/e2e.sh`
Expected: all three suites pass; output shows a `wallow-test-<pid>` project, per-run ports in every printed URL, publish log `(:test-<pid>, …)`.

- [ ] **Step 2: Confirm the run left nothing behind**

Run: `docker compose ls -a | grep wallow-test; docker images | grep -E "wallow-.*test-"`
Expected: no output from either (project gone, per-run tags untagged).

- [ ] **Step 3: Build the plain `:test` images once for the concurrent pair**

Run: `E2E_IMAGE_TAG=test E2E_UP_SERVICE=wallow-auth ./scripts/e2e.sh`
Expected: passes (cheap — Step 1's layer cache is reused, only the tags differ); because `E2E_IMAGE_TAG` was explicit, the `:test` tags now exist and survive teardown (only auto-generated tags are removed).

- [ ] **Step 4: The bead's acceptance — two simultaneous runs**

Run:
```bash
(cd /home/bcordes/Wallow && E2E_SKIP_IMAGE_BUILD=1 E2E_UP_SERVICE=wallow-auth ./scripts/e2e.sh) &
FIRST=$!
sleep 5
(cd /home/bcordes/Wallow && E2E_SKIP_IMAGE_BUILD=1 E2E_UP_SERVICE=wallow-auth ./scripts/e2e.sh)
SECOND=$?
wait "$FIRST"; FIRST_RC=$?
echo "first=$FIRST_RC second=$SECOND"
```
Expected: `first=0 second=0`. (Container mode on both sides the host-side build races the design scopes out.)

- [ ] **Step 5: Confirm mutual and dev-infra non-interference**

Run: `docker compose ls -a | grep -E "wallow($|-test)"`
Expected: only the dev-infra `wallow` project (if it was up) — no `wallow-test*` residue.

- [ ] **Step 6: Frontend gate**

Run: `pnpm check`
Expected: green — this catches `lint:env`, formatting of the touched TS config, and everything else the gate owns. (No backend code changed, so `./scripts/run-tests.sh` is not required, but running it is harmless.)

---

### Task 7: Close out

**Files:**
- Modify: `docs/plans/2026-08-28/1441-e2e-concurrent-isolation-design.md` (status line)
- Modify: `docs/plans/2026-08-28/1442-e2e-concurrent-isolation-plan.md` (status line)

- [ ] **Step 1: Flip both plan docs' first line** to `**status: completed**`.

- [ ] **Step 2: Record and close the bead**

```bash
bd note Wallow-joo0 "Implemented per docs/plans/2026-08-28/1441-e2e-concurrent-isolation-design.md + 1442 plan: per-run project wallow-test-\$E2E_STACK_ID, kernel-allocated E2E_*_PORT vars interpolated through docker-compose.test.yml (ports + OIDC URLs + new Clients__2 bcordes-bff overrides), per-run image tags when building, dead-stack sweep, E2E_BFF_EXAMPLE_URL/PORT threading, CI E2E_BASE_URL inference. Acceptance verified: two simultaneous container-mode runs green, dev-infra untouched."
bd close Wallow-joo0
```

- [ ] **Step 3: Commit, sync, push**

```bash
git add docs/plans/2026-08-28
git commit -m "docs(plans): complete e2e concurrent-isolation design + plan (Wallow-joo0)"
git pull --rebase && bd dolt push && git push
git status   # must be "up to date with origin"
git ls-remote origin refs/dolt/data   # hash must have changed
```
