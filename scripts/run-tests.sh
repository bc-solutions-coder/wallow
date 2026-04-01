#!/usr/bin/env bash
# Run Wallow tests with structured output for easy parsing.
# Usage:
#   ./scripts/run-tests.sh                    # Run all tests
#   ./scripts/run-tests.sh identity           # Run Identity module tests only
#   ./scripts/run-tests.sh <project-path>     # Run a specific test project

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RUNSETTINGS="$REPO_ROOT/tests/coverage.runsettings"
MODULE_FILTER="${1:-}"
TRX_DIR=$(mktemp -d)

# Map shorthand module names to test project paths
resolve_filter() {
    local filter="$1"
    local lower
    lower=$(echo "$filter" | tr '[:upper:]' '[:lower:]')
    case "$lower" in
        identity)       echo "$REPO_ROOT/tests/Modules/Identity/Wallow.Identity.Tests" ;;
        storage)        echo "$REPO_ROOT/tests/Modules/Storage/Wallow.Storage.Tests" ;;
        notifications)  echo "$REPO_ROOT/tests/Modules/Notifications/Wallow.Notifications.Tests" ;;
announcements)  echo "$REPO_ROOT/tests/Modules/Announcements/Wallow.Announcements.Tests" ;;
        inquiries)      echo "$REPO_ROOT/tests/Modules/Inquiries/Wallow.Inquiries.Tests" ;;
        branding)       echo "$REPO_ROOT/tests/Modules/Branding/Wallow.Branding.Tests" ;;
        apikeys)        echo "$REPO_ROOT/tests/Modules/ApiKeys/Wallow.ApiKeys.Tests" ;;
        auth)            echo "$REPO_ROOT/tests/Wallow.Auth.Tests" ;;
        auth-components) echo "$REPO_ROOT/tests/Wallow.Auth.Component.Tests" ;;
        web)             echo "$REPO_ROOT/tests/Wallow.Web.Tests" ;;
        web-components)  echo "$REPO_ROOT/tests/Wallow.Web.Component.Tests" ;;
        e2e)
            echo "ERROR: E2E tests must be run via ./scripts/run-e2e.sh (requires live infrastructure)." >&2
            exit 1
            ;;
        api)             echo "$REPO_ROOT/tests/Wallow.Api.Tests" ;;
        arch|architecture) echo "$REPO_ROOT/tests/Wallow.Architecture.Tests" ;;
        shared)          echo "$REPO_ROOT/tests/Wallow.Shared.Infrastructure.Tests" ;;
        kernel)          echo "$REPO_ROOT/tests/Wallow.Shared.Kernel.Tests" ;;
        integration)     echo "$REPO_ROOT/tests/Modules/Identity/Wallow.Identity.IntegrationTests" ;;
        "")              echo "" ;;
        *)               echo "$filter" ;;
    esac
}

PROJECT_PATH=$(resolve_filter "$MODULE_FILTER")

# Build test command
CMD=(dotnet test --settings "$RUNSETTINGS" --logger "trx;LogFilePrefix=results" --results-directory "$TRX_DIR" --no-restore -v quiet)
if [[ -n "$PROJECT_PATH" ]]; then
    CMD+=("$PROJECT_PATH")
fi
# Exclude E2E tests from standard runs; only include when explicitly requested via the e2e shorthand
if [[ -z "$MODULE_FILTER" ]]; then
    CMD+=(--filter "Category!=E2E")
fi

echo "=== WALLOW TEST RUN ==="
echo "Filter: ${MODULE_FILTER:-all}"
echo "Running: ${CMD[*]}"
echo ""

# Run tests, capture exit code
set +e
"${CMD[@]}" 2>&1
TEST_EXIT=$?
set -e

echo ""
echo "========================"
echo "=== TEST RESULTS ==="
echo "========================"
echo ""

# Parse TRX files for structured output
TOTAL_PASSED=0
TOTAL_FAILED=0
TOTAL_SKIPPED=0
FAILED_TESTS=""

for trx in "$TRX_DIR"/*.trx; do
    [[ -f "$trx" ]] || continue

    # Extract assembly name from the trx filename or content
    ASSEMBLY=$(basename "$trx" | sed 's/^results_//' | sed 's/_[0-9T].*\.trx$//')

    # Parse counters from the Counters element
    COUNTERS=$(grep -o '<Counters[^/]*/>' "$trx" 2>/dev/null || echo "")
    if [[ -z "$COUNTERS" ]]; then
        continue
    fi

    PASSED=$(echo "$COUNTERS" | grep -o 'passed="[0-9]*"' | grep -o '[0-9]*')
    FAILED=$(echo "$COUNTERS" | grep -o 'failed="[0-9]*"' | grep -o '[0-9]*')
    SKIPPED=$(echo "$COUNTERS" | grep -o 'notExecuted="[0-9]*"' | grep -o '[0-9]*' || echo "0")

    PASSED=${PASSED:-0}
    FAILED=${FAILED:-0}
    SKIPPED=${SKIPPED:-0}

    TOTAL_PASSED=$((TOTAL_PASSED + PASSED))
    TOTAL_FAILED=$((TOTAL_FAILED + FAILED))
    TOTAL_SKIPPED=$((TOTAL_SKIPPED + SKIPPED))

    # Status indicator
    if [[ "$FAILED" -gt 0 ]]; then
        STATUS="FAIL"
    else
        STATUS="PASS"
    fi

    printf "%-55s %s  (passed: %d, failed: %d, skipped: %d)\n" "$ASSEMBLY" "$STATUS" "$PASSED" "$FAILED" "$SKIPPED"

    # Collect failed test names
    if [[ "$FAILED" -gt 0 ]]; then
        FAILS=$(grep -o 'testName="[^"]*"' "$trx" | while read -r line; do
            TEST_NAME=$(echo "$line" | sed 's/testName="//;s/"$//')
            # Check if this test failed
            if grep -q "testName=\"$TEST_NAME\".*outcome=\"Failed\"" "$trx" 2>/dev/null; then
                echo "  - $TEST_NAME"
            fi
        done)
        if [[ -n "$FAILS" ]]; then
            FAILED_TESTS="${FAILED_TESTS}
${ASSEMBLY}:
${FAILS}"
        fi
    fi
done

TOTAL=$((TOTAL_PASSED + TOTAL_FAILED + TOTAL_SKIPPED))

echo ""
echo "========================"
printf "TOTAL: %d tests | PASSED: %d | FAILED: %d | SKIPPED: %d\n" "$TOTAL" "$TOTAL_PASSED" "$TOTAL_FAILED" "$TOTAL_SKIPPED"

if [[ "$TOTAL_FAILED" -gt 0 ]]; then
    echo ""
    echo "=== FAILED TESTS ==="
    echo "$FAILED_TESTS"
    echo ""
    echo "RESULT: FAIL"
else
    echo ""
    echo "RESULT: PASS"
fi

# Cleanup
rm -rf "$TRX_DIR"

exit $TEST_EXIT
