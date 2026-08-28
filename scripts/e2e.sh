#!/usr/bin/env bash
# One-command backend-dependent E2E runner for all three Playwright suites:
# apps/wallow-auth, apps/wallow-web, and the wallow-web cross-app login journey.
#
# Brings the docker/docker-compose.test.yml stack up (Postgres, Valkey, Mailpit,
# GarageHQ, migrations, seeder, Wallow.Api, wallow-auth, wallow-web), waits for
# the API + seeded admin, runs the suites, then tears the stack down.
#
# E2E_BASE_URL selects how the **wallow-auth** suite is served:
#
#   LOCAL (default): the app is served by Playwright's own `pnpm dev` webServer on
#     :3002; its passthrough proxy targets the containerised API (WALLOW_API_INTERNAL_URL).
#     The compose stack provides infra + API + seeder (service: wallow-api).
#
#   CONTAINER (E2E_BASE_URL set, e.g. CI): the app is served by the prebuilt
#     wallow-auth-react:test container on its allocated port (classic default
#     :5051, per-run below); Playwright drives it directly and boots no local
#     dev server. Bring up the `wallow-auth` service instead.
#
# The two **wallow-web** suites always run in container mode against this
# run's wallow-web port (classic default :5053), regardless of that choice —
# see the comment above their invocation below.
#
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
#
# Usage:
#   ./scripts/e2e.sh                 # local run: (re)builds images, up, test, down
#   E2E_SKIP_IMAGE_BUILD=1 ./scripts/e2e.sh   # reuse already-built :test images

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/docker/docker-compose.test.yml"
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

if [[ -z "${E2E_SKIP_IMAGE_BUILD:-}" ]]; then
  # The API/migration/seeder compose services have no build block — they consume
  # prebuilt :test images. Publish them as OCI images for the host's Docker arch.
  case "$(uname -m)" in
    arm64 | aarch64) RID="linux-arm64" ;;
    x86_64 | amd64) RID="linux-x64" ;;
    *)
      echo "ERROR: unsupported host arch $(uname -m) for container publish" >&2
      exit 1
      ;;
  esac

  # Mirror ci.yml: restore + build the solution without a RID, then publish
  # --no-build with an explicit ContainerRuntimeIdentifier. Publishing a RID
  # image in one RID-less invocation trips NETSDK1047 (assets file has no target
  # for the container RID); the build-then-package split avoids it.
  log "Restoring + building the solution (Release)"
  dotnet restore "$REPO_ROOT/api/Wallow.slnx"
  dotnet build "$REPO_ROOT/api/Wallow.slnx" --no-restore -c Release

  log "Publishing API / migration / seeder container images (:$E2E_IMAGE_TAG, $RID)"
  for proj in Wallow.Api Wallow.MigrationService Wallow.SeederService; do
    dotnet publish "$REPO_ROOT/api/src/$proj/$proj.csproj" \
      -c Release --no-build /t:PublishContainer \
      -p:ContainerImageTag="$E2E_IMAGE_TAG" -p:ContainerRuntimeIdentifier="$RID"
  done
fi

# `up --wait` brings up the target services and their transitive deps (Postgres,
# Valkey, Garage, Mailpit, migrations, seeder) and blocks until each is healthy /
# completed. Garage is auto-built from its build block if the image is absent.
#
# wallow-web is always in the set: the cross-app journey drives it directly and
# its own `depends_on` (wallow-api, wallow-auth, valkey) pulls up the other two
# origins that journey traverses. Like wallow-auth it carries a build block, so a
# cold run without a prebuilt wallow-web-react:test image builds one here.
#
# bff-example is also always in the set: it stands in for a genuinely separate
# "external origin" site the cross-app suite's external-origin-login.spec.ts
# drives directly (Wallow-yp3e.4). Nothing `depends_on` it -- wallow-web's own
# dependency chain doesn't reach it -- so unlike wallow-auth/valkey it must be
# named explicitly here or it never starts and that spec fails with
# ERR_CONNECTION_REFUSED against its allocated port (classic default :3003). It
# carries a build block and a healthcheck
# identical to wallow-web's (same image), so `--wait` blocks on it the same way.
#
# --build is why this is an array. Compose builds a service's image only when one
# is ABSENT, so without it any wallow-web-react:test / wallow-auth-react:test left
# over from an earlier run is reused verbatim, however far the tree has moved
# since -- a green E2E run that proves nothing about the code under test, and the
# way a broken image build once went unnoticed for two days (Wallow-gwy2). Layer
# caching makes the no-change rebuild cheap; a changed tree rebuilds, which is the
# point. This reaches every service in the set that HAS a build block, so garage
# and bff-example are covered by the same guarantee, not just the two app images.
UP_ARGS=(up -d --wait)
if [[ -z "${E2E_SKIP_IMAGE_BUILD:-}" ]]; then
  UP_ARGS+=(--build)
fi
UP_ARGS+=("$UP_SERVICE" wallow-web bff-example)

log "Bringing up compose stack (services: $UP_SERVICE, wallow-web, bff-example)"
"${COMPOSE[@]}" "${UP_ARGS[@]}"

# `--wait` returns once wallow-api is *running*, not necessarily once Kestrel is
# listening. Poll OIDC discovery so the login spec never races the boot.
log "Waiting for the API at $DISCOVERY_URL"
for attempt in $(seq 1 60); do
  if curl -fsS -o /dev/null "$DISCOVERY_URL" 2>/dev/null; then
    echo "API ready after ${attempt}s"
    break
  fi
  if [[ "$attempt" -eq 60 ]]; then
    echo "ERROR: API did not become ready in 60s" >&2
    "${COMPOSE[@]}" logs --tail 40 wallow-api >&2 || true
    exit 1
  fi
  sleep 1
done

E2E_ENV=("E2E_MAILPIT_URL=$MAILPIT_URL" "E2E_AUTH_ORIGIN=$AUTH_ORIGIN")
if [[ -z "${E2E_BASE_URL:-}" ]]; then
  # Local mode: Playwright's `pnpm dev` webServer serves the app and proxies to
  # the containerised API. From a cold checkout the workspace deps, the Chromium
  # browser, and the @bc-solutions-coder/sdk dist/ the dev server resolves against
  # may all be missing — provision them. (CI does its own install + browser step
  # and serves the app from a container, so this branch is skipped there.)
  # One `playwright install` covers both apps: they pin the same @playwright/test
  # version, and the browser binaries live in a shared per-version cache.
  log "Installing workspace deps + Playwright Chromium"
  pnpm install --frozen-lockfile
  pnpm --filter ./apps/wallow-auth exec playwright install chromium
  # Build every workspace package the dev server resolves against (sdk, forms,
  # ui, styles, query, auth, ...): their exports all point at dist/, so any
  # unbuilt dependency surfaces as a Vite import-analysis error overlay that
  # blocks every click in the suite. The ^... filter selects the app's
  # dependency closure without rebuilding the app itself, so new workspace
  # packages are covered automatically.
  log "Building wallow-auth's workspace dependencies for the dev server"
  pnpm --filter "@bc-solutions-coder/wallow-auth^..." build
  # This only reaches a dev server Playwright actually STARTS. Its config sets
  # `reuseExistingServer`, so an unrelated `pnpm dev` already on the port is
  # adopted with its own upstream and quietly serves the suite against the dev
  # API. apps/wallow-auth/e2e/global-setup.ts compares OIDC discovery issuers to
  # catch exactly that and aborts the run with the offending URLs.
  # PORT is this run's allocated dev-server port: playwright.config.ts waits on
  # it and passes it to the `pnpm dev` child, so two concurrent local-mode runs
  # cannot adopt each other's dev server via reuseExistingServer.
  E2E_ENV+=("WALLOW_API_INTERNAL_URL=$API_URL" "PORT=$AUTH_DEV_PORT")
else
  E2E_ENV+=("E2E_BASE_URL=$E2E_BASE_URL")
fi

log "Running the wallow-auth Playwright suite"
env "${E2E_ENV[@]}" pnpm --filter ./apps/wallow-auth test:e2e

# Both wallow-web suites always drive the containerised app on this run's
# wallow-web port (classic default :5053), whichever mode the wallow-auth
# suite ran in. The cross-app journey needs three
# cooperating origins the compose stack alone cross-wires — wallow-web (where the
# journey starts and ends), the API's OIDC issuer, and the wallow-auth login UI —
# and playwright.cross-app.config.ts boots no server of its own. Passing
# E2E_BASE_URL also stops apps/wallow-web/playwright.config.ts from starting a
# `pnpm dev` webServer, so the reachability gate hits that same container.
log "Running the wallow-web Playwright suite"
env "E2E_BASE_URL=$WEB_URL" pnpm --filter ./apps/wallow-web test:e2e

log "Running the wallow-web cross-app login journey suite"
env "E2E_BASE_URL=$WEB_URL" "E2E_BFF_EXAMPLE_URL=$BFF_EXAMPLE_URL" \
  pnpm --filter ./apps/wallow-web test:e2e:cross-app
