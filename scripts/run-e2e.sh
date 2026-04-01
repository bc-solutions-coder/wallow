#!/usr/bin/env bash
# Run E2E tests with full lifecycle management.
# Builds once on the host, publishes container images via dotnet publish /t:PublishContainer,
# starts the test stack, runs tests, and tears everything down.
#
# Usage:
#   ./scripts/run-e2e.sh              # Build, publish images, test, teardown
#   ./scripts/run-e2e.sh --no-build   # Skip build (reuse existing images)
#   ./scripts/run-e2e.sh --keep       # Don't tear down after tests (for debugging)
#   ./scripts/run-e2e.sh --headed     # Run browser in headed mode
#   ./scripts/run-e2e.sh --video      # Record video of test runs
#   ./scripts/run-e2e.sh --tracing    # Enable Playwright tracing (saved on failure)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$REPO_ROOT/docker/docker-compose.test.yml"
COMPOSE_CMD="docker compose -p wallow-test -f $COMPOSE_FILE"

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
            echo "  --no-build   Skip build and image publish (reuse existing)"
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
        echo "Tear down manually: docker compose -p wallow-test -f docker/docker-compose.test.yml down -v"
    else
        echo ""
        echo "=== Tearing down test stack ==="
        $COMPOSE_CMD down -v 2>/dev/null || true
    fi
}
trap cleanup EXIT

# ============================================
# Step 1: Build and publish container images
# ============================================
if [[ "$SKIP_BUILD" == "false" ]]; then
    echo "=== Building solution ==="
    dotnet build "$REPO_ROOT/Wallow.slnx" -c Release

    # Detect host arch for single-arch local publish (faster than multi-arch)
    ARCH=$(uname -m)
    case "$ARCH" in
        arm64|aarch64) RID="linux-arm64" ;;
        *)             RID="linux-x64" ;;
    esac

    echo ""
    echo "=== Publishing container images (${RID}) ==="

    echo "  Publishing wallow-api:test..."
    dotnet publish "$REPO_ROOT/src/Wallow.Api/Wallow.Api.csproj" \
        -c Release --no-build /t:PublishContainer \
        -p:ContainerImageTag=test \
        -p:ContainerRuntimeIdentifier="$RID"

    echo "  Publishing wallow-auth:test..."
    dotnet publish "$REPO_ROOT/src/Wallow.Auth/Wallow.Auth.csproj" \
        -c Release --no-build /t:PublishContainer \
        -p:ContainerImageTag=test \
        -p:ContainerRuntimeIdentifier="$RID"

    echo "  Publishing wallow-web:test..."
    dotnet publish "$REPO_ROOT/src/Wallow.Web/Wallow.Web.csproj" \
        -c Release --no-build /t:PublishContainer \
        -p:ContainerImageTag=test \
        -p:ContainerRuntimeIdentifier="$RID"

    echo "  Publishing wallow-migrations:test..."
    dotnet publish "$REPO_ROOT/src/Wallow.MigrationService/Wallow.MigrationService.csproj" \
        -c Release --no-build /t:PublishContainer \
        -p:ContainerImageTag=test \
        -p:ContainerRuntimeIdentifier="$RID"

    echo ""
    echo "=== Building infrastructure images ==="
    $COMPOSE_CMD build garage

    echo "=== All images ready ==="
else
    echo "=== Skipping build (--no-build) ==="
fi

# ============================================
# Step 2: Start the test stack
# ============================================
echo ""
echo "=== Starting test stack ==="
$COMPOSE_CMD up -d

# ============================================
# Step 3: Wait for migrations to complete
# ============================================
echo ""
echo "=== Waiting for migrations to complete ==="
MIGRATION_TIMEOUT=120
MIGRATION_ELAPSED=0
while [ $MIGRATION_ELAPSED -lt $MIGRATION_TIMEOUT ]; do
    STATUS=$($COMPOSE_CMD ps -a wallow-migrations --format '{{.State}}' 2>/dev/null)
    if [ "$STATUS" = "exited" ]; then
        EXIT_CODE=$($COMPOSE_CMD ps -a wallow-migrations --format '{{.ExitCode}}' 2>/dev/null)
        if [ "$EXIT_CODE" = "0" ]; then
            echo "  Migrations completed successfully (${MIGRATION_ELAPSED}s)"
            break
        else
            echo "  ERROR: Migration service failed with exit code $EXIT_CODE"
            $COMPOSE_CMD logs wallow-migrations
            exit 1
        fi
    fi
    sleep 2
    MIGRATION_ELAPSED=$((MIGRATION_ELAPSED + 2))
done
if [ $MIGRATION_ELAPSED -ge $MIGRATION_TIMEOUT ]; then
    echo "  ERROR: Timeout waiting for migrations after ${MIGRATION_TIMEOUT}s"
    $COMPOSE_CMD logs wallow-migrations
    exit 1
fi

# ============================================
# Step 4: Wait for services to be healthy
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
# Step 5: Run E2E tests
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
dotnet test "$REPO_ROOT/tests/Wallow.E2E.Tests" \
    --configuration Release --no-build \
    --settings "$REPO_ROOT/tests/coverage.runsettings" \
    --verbosity normal --blame-hang-timeout 5m
TEST_EXIT=$?
set -e

exit $TEST_EXIT
