#!/usr/bin/env bash
# Run E2E tests with full lifecycle management.
# Builds app images, starts the test stack, runs tests, and tears everything down.
#
# Usage:
#   ./scripts/run-e2e.sh              # Build, start, test, teardown
#   ./scripts/run-e2e.sh --no-build   # Skip image build (reuse existing images)
#   ./scripts/run-e2e.sh --keep       # Don't tear down after tests (for debugging)
#   ./scripts/run-e2e.sh --headed     # Run browser in headed mode
#   ./scripts/run-e2e.sh --video      # Record video of test runs
#   ./scripts/run-e2e.sh --tracing    # Enable Playwright tracing (saved on failure)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/docker/docker-compose.test.yml"
DOCKERFILE="$REPO_ROOT/Dockerfile"

# Defaults
SKIP_BUILD=false
KEEP_RUNNING=false
E2E_HEADED=""
E2E_VIDEO=""
E2E_TRACING=""

# Parse flags
for arg in "$@"; do
    case "$arg" in
        --no-build)   SKIP_BUILD=true ;;
        --keep)       KEEP_RUNNING=true ;;
        --headed)     E2E_HEADED=true ;;
        --video)      E2E_VIDEO=true ;;
        --tracing)    E2E_TRACING=true ;;
        --help|-h)
            echo "Usage: ./scripts/run-e2e.sh [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --no-build   Skip Docker image build (reuse existing)"
            echo "  --keep       Don't tear down containers after tests"
            echo "  --headed     Run browser in headed mode"
            echo "  --video      Record video of test runs"
            echo "  --tracing    Enable Playwright tracing"
            echo "  -h, --help   Show this help"
            exit 0
            ;;
        *)
            echo "Unknown option: $arg (use --help for usage)"
            exit 1
            ;;
    esac
done

# Cleanup function — tears down containers unless --keep was specified
cleanup() {
    if [[ "$KEEP_RUNNING" == "true" ]]; then
        echo ""
        echo "=== Containers left running (--keep) ==="
        echo "  API:     http://localhost:5050"
        echo "  Auth:    http://localhost:5051"
        echo "  Web:     http://localhost:5053"
        echo "  Mailpit: http://localhost:8035"
        echo ""
        echo "Tear down manually: docker compose -f docker/docker-compose.test.yml down -v"
    else
        echo ""
        echo "=== Tearing down test stack ==="
        docker compose -f "$COMPOSE_FILE" down -v 2>/dev/null || true
    fi
}
trap cleanup EXIT

# ============================================
# Step 1: Build Docker images
# ============================================
if [[ "$SKIP_BUILD" == "false" ]]; then
    echo "=== Building application images ==="

    docker build -t wallow-api:test \
        --build-arg BUILD_PROJECT=src/Wallow.Api/Wallow.Api.csproj \
        --build-arg ENTRYPOINT_DLL=Wallow.Api.dll \
        -f "$DOCKERFILE" "$REPO_ROOT" &
    PID_API=$!

    docker build -t wallow-auth:test \
        --build-arg BUILD_PROJECT=src/Wallow.Auth/Wallow.Auth.csproj \
        --build-arg ENTRYPOINT_DLL=Wallow.Auth.dll \
        -f "$DOCKERFILE" "$REPO_ROOT" &
    PID_AUTH=$!

    docker build -t wallow-web:test \
        --build-arg BUILD_PROJECT=src/Wallow.Web/Wallow.Web.csproj \
        --build-arg ENTRYPOINT_DLL=Wallow.Web.dll \
        -f "$DOCKERFILE" "$REPO_ROOT" &
    PID_WEB=$!

    # Wait for all builds — fail fast if any fail
    FAILED=false
    for pid in $PID_API $PID_AUTH $PID_WEB; do
        if ! wait "$pid"; then
            FAILED=true
        fi
    done

    if [[ "$FAILED" == "true" ]]; then
        echo "ERROR: One or more image builds failed."
        exit 1
    fi

    echo "=== All images built ==="
else
    echo "=== Skipping image build (--no-build) ==="
fi

# ============================================
# Step 2: Start the test stack
# ============================================
echo ""
echo "=== Starting test stack ==="
docker compose -f "$COMPOSE_FILE" up -d

# ============================================
# Step 3: Wait for services to be healthy
# ============================================
echo ""
echo "=== Waiting for services ==="

wait_for_health() {
    local url="$1"
    local name="$2"
    local max_wait=120
    local elapsed=0

    while [[ $elapsed -lt $max_wait ]]; do
        if curl -sf --max-time 3 "$url" > /dev/null 2>&1; then
            echo "  $name ready ($elapsed s)"
            return 0
        fi
        sleep 3
        elapsed=$((elapsed + 3))
    done

    echo "  ERROR: $name not healthy after ${max_wait}s ($url)"
    return 1
}

wait_for_health "http://localhost:5050/health/ready" "API"
wait_for_health "http://localhost:5051/health" "Auth"
wait_for_health "http://localhost:5053/health" "Web"

echo "=== All services healthy ==="

# ============================================
# Step 4: Run E2E tests
# ============================================
echo ""
echo "=== Running E2E tests ==="

# Export env vars for the test runner
export E2E_EXTERNAL_SERVICES=true
export E2E_BASE_URL=http://localhost:5050
export E2E_AUTH_URL=http://localhost:5051
export E2E_WEB_URL=http://localhost:5053
export E2E_MAILPIT_URL=http://localhost:8035

[[ -n "$E2E_HEADED" ]] && export E2E_HEADED
[[ -n "$E2E_VIDEO" ]] && export E2E_VIDEO
[[ -n "$E2E_TRACING" ]] && export E2E_TRACING

set +e
"$REPO_ROOT/scripts/run-tests.sh" e2e
TEST_EXIT=$?
set -e

exit $TEST_EXIT
