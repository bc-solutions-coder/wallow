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
#   E2E_SKIP_IMAGE_BUILD=1  Reuse whatever `:test` images already exist instead of
#                           building any of them: skips both the `dotnet publish`
#                           of the API/migration/seeder images AND compose's
#                           `--build` of the ones with a build block (wallow-web,
#                           wallow-auth, bff-example, garage). CI preloads all but
#                           bff-example from cache, so it sets this; a local caller
#                           wanting the same reuse sets it too. Leaving it UNSET is
#                           what guarantees the run tests the current tree.
#   E2E_UP_SERVICE=<svc>    Extra compose service to `up --wait` (default:
#                           wallow-api; CI sets wallow-auth to serve that app from
#                           a container). `wallow-web` is always brought up too.
#   E2E_BASE_URL=<url>      Drive an already-running wallow-auth at <url>; skips
#                           `pnpm dev`. Does not affect the wallow-web suites.
#   E2E_KEEP_STACK=1        Leave the stack up after the run (for debugging).
#
# Usage:
#   ./scripts/e2e.sh                 # local run: (re)builds images, up, test, down
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
# Two more endpoints the backend-dependent wallow-auth specs need, and which only
# this script can know. Both branches below drive the CONTAINERISED backend, so
# both get these — E2E_BASE_URL picks the app's serving mode, not the backend's,
# and using it to infer either of these would read the local values off a run
# whose API is a container.
#   Mailpit HTTP: docker-compose.test.yml publishes 127.0.0.1:8035:8025, and the
#     API's Smtp__Host points at that same container.
#   Auth origin: the API's own configured AuthUrl in that stack, which
#     OpenIddictRedirectUriValidator allow-lists unconditionally.
MAILPIT_URL="http://127.0.0.1:8035"
AUTH_ORIGIN="http://localhost:5051"

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
#
# bff-example is also always in the set: it stands in for a genuinely separate
# "external origin" site the cross-app suite's external-origin-login.spec.ts
# drives directly (Wallow-yp3e.4). Nothing `depends_on` it -- wallow-web's own
# dependency chain doesn't reach it -- so unlike wallow-auth/valkey it must be
# named explicitly here or it never starts and that spec fails with
# ERR_CONNECTION_REFUSED against :3003. It carries a build block and a healthcheck
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
