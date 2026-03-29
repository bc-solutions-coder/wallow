#!/usr/bin/env bash
# Run E2E tests with full lifecycle management.
# Builds once on the host, publishes container images, starts the test stack,
# runs tests, and tears everything down.
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
    echo "=== Building solution (Release) ==="
    dotnet build --configuration Release "$REPO_ROOT/Wallow.slnx"

    echo ""
    echo "=== Publishing container images ==="
    dotnet publish "$REPO_ROOT/src/Wallow.Api/Wallow.Api.csproj" \
        -c Release /t:PublishContainer -p:ContainerImageTag=test --no-build &
    PID_API=$!

    dotnet publish "$REPO_ROOT/src/Wallow.Auth/Wallow.Auth.csproj" \
        -c Release /t:PublishContainer -p:ContainerImageTag=test --no-build &
    PID_AUTH=$!

    dotnet publish "$REPO_ROOT/src/Wallow.Web/Wallow.Web.csproj" \
        -c Release /t:PublishContainer -p:ContainerImageTag=test --no-build &
    PID_WEB=$!

    FAILED=false
    for pid in $PID_API $PID_AUTH $PID_WEB; do
        if ! wait "$pid"; then
            FAILED=true
        fi
    done

    if [[ "$FAILED" == "true" ]]; then
        echo "ERROR: One or more container publishes failed."
        exit 1
    fi
    echo "=== Container images published ==="

    echo ""
    echo "=== Building migration bundles ==="
    dotnet tool restore

    # Detect target runtime for migration bundles.
    # On non-Linux hosts (e.g. macOS), bundles must target the container's Linux arch.
    # CI runs on Linux so --no-build works; locally we cross-compile with a two-step
    # approach: build for the target runtime first, then create bundles with --no-build.
    # This avoids EF trying to boot the host during bundle creation (which fails without Redis).
    BUNDLE_EXTRA_ARGS=()
    if [[ "$(uname -s)" != "Linux" ]]; then
        case "$(uname -m)" in
            arm64|aarch64) BUNDLE_RID="linux-arm64" ;;
            *)             BUNDLE_RID="linux-x64" ;;
        esac
        echo "  Cross-compiling for $BUNDLE_RID (non-Linux host detected)"
        echo "  Building startup project for target runtime..."
        dotnet build "$REPO_ROOT/src/Wallow.Api/Wallow.Api.csproj" \
            --configuration Release --runtime "$BUNDLE_RID" --no-self-contained
        BUNDLE_EXTRA_ARGS=("--target-runtime" "$BUNDLE_RID")
    fi

    MODULES=(
        "Identity:IdentityDbContext"
        "Billing:BillingDbContext"
        "Storage:StorageDbContext"
        "Notifications:NotificationsDbContext"
        "Messaging:MessagingDbContext"
        "Announcements:AnnouncementsDbContext"
        "ApiKeys:ApiKeysDbContext"
        "Branding:BrandingDbContext"
        "Inquiries:InquiriesDbContext"
    )

    BUNDLE_DIR="$REPO_ROOT/bundles"
    rm -rf "$BUNDLE_DIR"
    mkdir -p "$BUNDLE_DIR"

    for entry in "${MODULES[@]}"; do
        module="${entry%%:*}"
        context="${entry##*:}"
        dotnet ef migrations bundle \
            --project "$REPO_ROOT/src/Modules/${module}/Wallow.${module}.Infrastructure/Wallow.${module}.Infrastructure.csproj" \
            --startup-project "$REPO_ROOT/src/Wallow.Api/Wallow.Api.csproj" \
            --context "$context" \
            --output "$BUNDLE_DIR/efbundle-$(echo "$module" | tr '[:upper:]' '[:lower:]')" \
            --configuration Release --force --no-build "${BUNDLE_EXTRA_ARGS[@]}"
    done

    dotnet ef migrations bundle \
        --project "$REPO_ROOT/src/Shared/Wallow.Shared.Infrastructure.Core/Wallow.Shared.Infrastructure.Core.csproj" \
        --startup-project "$REPO_ROOT/src/Wallow.Api/Wallow.Api.csproj" \
        --context AuditDbContext \
        --output "$BUNDLE_DIR/efbundle-audit" \
        --configuration Release --force --no-build "${BUNDLE_EXTRA_ARGS[@]}"

    echo "=== Migration bundles built ==="

    echo ""
    echo "=== Building migrations container image ==="
    docker build -f - -t wallow-migrations:test "$REPO_ROOT" <<'DOCKERFILE'
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY bundles/ bundles/
COPY scripts/apply-migrations.sh .
RUN chmod +x apply-migrations.sh
ENTRYPOINT ["./apply-migrations.sh"]
DOCKERFILE

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
dotnet test "$REPO_ROOT/tests/Wallow.E2E.Tests" \
    --configuration Release --no-build \
    --settings "$REPO_ROOT/tests/coverage.runsettings" \
    --verbosity normal --blame-hang-timeout 5m
TEST_EXIT=$?
set -e

exit $TEST_EXIT
