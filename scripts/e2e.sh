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
#     wallow-auth-react:test container on :5051; Playwright drives it directly and
#     boots no local dev server. Bring up the `wallow-auth` service instead.
#
# The two **wallow-web** suites always run in container mode against :5053,
# regardless of that choice — see the comment above their invocation below.
#
# Env knobs:
#   E2E_SKIP_IMAGE_BUILD=1  Skip `dotnet publish` of the API/migration/seeder
#                           images (CI preloads them from cache; set this there).
#   E2E_UP_SERVICE=<svc>    Extra compose service to `up --wait` (default:
#                           wallow-api; CI sets wallow-auth to serve that app from
#                           a container). `wallow-web` is always brought up too.
#   E2E_BASE_URL=<url>      Drive an already-running wallow-auth at <url>; skips
#                           `pnpm dev`. Does not affect the wallow-web suites.
#   E2E_KEEP_STACK=1        Leave the stack up after the run (for debugging).
#
# Usage:
#   ./scripts/e2e.sh                 # cold local run: builds images, up, test, down
#   E2E_SKIP_IMAGE_BUILD=1 ./scripts/e2e.sh   # reuse already-built :test images

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/docker/docker-compose.test.yml"
# --project-name pins the Compose project even if the caller's environment (or
# docker/.env, which COMPOSE_PROJECT_NAME-scopes the dev-infra stack) would
# override the compose file's top-level `name:`. Without this, teardown's
# `down --remove-orphans` removes the running dev-infra containers (Wallow-kd2e).
COMPOSE=(docker compose --project-name wallow-test -f "$COMPOSE_FILE")

UP_SERVICE="${E2E_UP_SERVICE:-wallow-api}"
# Host-published API port from docker-compose.test.yml (wallow-api: 5050:8080).
API_URL="http://localhost:5050"
DISCOVERY_URL="$API_URL/.well-known/openid-configuration"
# Host-published wallow-web port from docker-compose.test.yml (5053:3000).
WEB_URL="http://localhost:5053"

log() { printf '\n=== %s ===\n' "$1"; }

teardown() {
  if [[ -n "${E2E_KEEP_STACK:-}" ]]; then
    log "E2E_KEEP_STACK set — leaving the stack up"
    return
  fi
  log "Tearing down the e2e stack"
  "${COMPOSE[@]}" down -v --remove-orphans || true
}
trap teardown EXIT

# Fresh volumes every run so the seeder always bootstraps admin@wallow.dev.
# (Seeder skips admin bootstrap if ANY user already exists — Wallow-wd6n — so a
# reused DB would silently lack the seed admin the login spec signs in as.)
log "Cleaning any prior e2e stack"
"${COMPOSE[@]}" down -v --remove-orphans || true

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

  log "Publishing API / migration / seeder container images (:test, $RID)"
  for proj in Wallow.Api Wallow.MigrationService Wallow.SeederService; do
    dotnet publish "$REPO_ROOT/api/src/$proj/$proj.csproj" \
      -c Release --no-build /t:PublishContainer \
      -p:ContainerImageTag=test -p:ContainerRuntimeIdentifier="$RID"
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
log "Bringing up compose stack (services: $UP_SERVICE, wallow-web)"
"${COMPOSE[@]}" up -d --wait "$UP_SERVICE" wallow-web

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

E2E_ENV=()
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
  # ui, styles, web-shell, ...): their exports all point at dist/, so any
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
  E2E_ENV+=("WALLOW_API_INTERNAL_URL=$API_URL")
else
  E2E_ENV+=("E2E_BASE_URL=$E2E_BASE_URL")
fi

log "Running the wallow-auth Playwright suite"
env "${E2E_ENV[@]}" pnpm --filter ./apps/wallow-auth test:e2e

# Both wallow-web suites always drive the containerised app on :5053, whichever
# mode the wallow-auth suite ran in. The cross-app journey needs three
# cooperating origins the compose stack alone cross-wires — wallow-web (where the
# journey starts and ends), the API's OIDC issuer, and the wallow-auth login UI —
# and playwright.cross-app.config.ts boots no server of its own. Passing
# E2E_BASE_URL also stops apps/wallow-web/playwright.config.ts from starting a
# `pnpm dev` webServer, so the reachability gate hits that same container.
log "Running the wallow-web Playwright suite"
env "E2E_BASE_URL=$WEB_URL" pnpm --filter ./apps/wallow-web test:e2e

log "Running the wallow-web cross-app login journey suite"
env "E2E_BASE_URL=$WEB_URL" pnpm --filter ./apps/wallow-web test:e2e:cross-app
