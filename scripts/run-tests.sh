#!/usr/bin/env bash
# Run Wallow tests with structured output for easy parsing.
# Usage:
#   ./scripts/run-tests.sh                    # Fast suites only (Category=Integration EXCLUDED)
#   ./scripts/run-tests.sh integration        # Every Category=Integration test, solution-wide (Docker)
#   ./scripts/run-tests.sh all                # Fast suites + integration, one run (Docker)
#   ./scripts/run-tests.sh identity           # Run Identity module tests only
#   ./scripts/run-tests.sh <project-path>     # Run a specific test project
#   ./scripts/run-tests.sh api integration    # One target's integration tests (2nd arg: integration|all)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RUNSETTINGS="$REPO_ROOT/api/tests/coverage.runsettings"
MODULE_FILTER="${1:-}"
TRX_DIR=$(mktemp -d)

# Shorthands are case-insensitive; an arbitrary project path is matched verbatim, because paths are
# case-sensitive. A first argument that is neither is a hard error -- see resolve_filter.
MODE=$(printf '%s' "$MODULE_FILTER" | tr '[:upper:]' '[:lower:]')
TIER=$(printf '%s' "${2:-}" | tr '[:upper:]' '[:lower:]')
# Echo back what was actually typed, before TIER is inferred from the first argument below.
SELECTOR_LABEL="${MODULE_FILTER:-(default)}${2:+ $2}"

# Map shorthand module names to test project paths
resolve_filter() {
    local lower="$1"
    case "$lower" in
        identity)       echo "$REPO_ROOT/api/tests/Modules/Identity/Wallow.Identity.Tests" ;;
        storage)        echo "$REPO_ROOT/api/tests/Modules/Storage/Wallow.Storage.Tests" ;;
        notifications)  echo "$REPO_ROOT/api/tests/Modules/Notifications/Wallow.Notifications.Tests" ;;
        announcements)  echo "$REPO_ROOT/api/tests/Modules/Announcements/Wallow.Announcements.Tests" ;;
        inquiries)      echo "$REPO_ROOT/api/tests/Modules/Inquiries/Wallow.Inquiries.Tests" ;;
        branding)       echo "$REPO_ROOT/api/tests/Modules/Branding/Wallow.Branding.Tests" ;;
        apikeys)        echo "$REPO_ROOT/api/tests/Modules/ApiKeys/Wallow.ApiKeys.Tests" ;;
        api)             echo "$REPO_ROOT/api/tests/Wallow.Api.Tests" ;;
        arch|architecture) echo "$REPO_ROOT/api/tests/Wallow.Architecture.Tests" ;;
        seeder)          echo "$REPO_ROOT/api/tests/Wallow.SeederService.Tests" ;;
        migrations)      echo "$REPO_ROOT/api/tests/Wallow.MigrationService.Tests" ;;
        shared)          echo "$REPO_ROOT/api/tests/Wallow.Shared.Infrastructure.Tests" ;;
        kernel)          echo "$REPO_ROOT/api/tests/Wallow.Shared.Kernel.Tests" ;;
        # Integration tests are spread over seven assemblies, not one: Wallow.Api.Tests carries the
        # Wolverine handler-codegen guards, and Storage/Announcements/Inquiries/Identity/Shared each
        # carry Testcontainers-backed suites. Only the category selects them, so run the solution.
        integration|all) echo "$REPO_ROOT/api/Wallow.slnx" ;;
        "")              echo "$REPO_ROOT/api/Wallow.slnx" ;;
        # A path that exists is the documented `<project-path>` form. Anything else is a
        # misspelled shorthand: echo nothing so the caller below can fail loudly. Passing it
        # through used to hand it to `dotnet test`, which resolved nothing, ran nothing, and
        # exited 0 -- a typo reported green.
        *)               if [[ -e "$MODULE_FILTER" ]]; then echo "$MODULE_FILTER"; fi ;;
    esac
}

PROJECT_PATH=$(resolve_filter "$MODE")

if [[ -z "$PROJECT_PATH" ]]; then
    echo "Unknown target '${MODULE_FILTER}'. It is neither a shorthand nor a path that exists." >&2
    echo "Shorthands: identity, storage, notifications, announcements, inquiries, branding," >&2
    echo "            apikeys, api, arch|architecture, seeder, migrations, shared, kernel," >&2
    echo "            integration, all." >&2
    echo "Or give a test project path, relative to the current directory." >&2
    rm -rf "$TRX_DIR"
    exit 2
fi

# Build test command
CMD=(dotnet test --settings "$RUNSETTINGS" --logger "trx;LogFilePrefix=results" --results-directory "$TRX_DIR" --no-restore -v quiet "$PROJECT_PATH")

# `integration` and `all` are whole-solution tiers; as a SECOND argument they narrow the same tier to
# whatever the first argument selected, so `run-tests.sh api integration` iterates on
# HandlerCodegenTests without running the other six.
case "$MODE" in
    integration|all) TIER="$MODE" ;;
esac

# Integration tests need Docker (Testcontainers), so they are opt-in. Whenever they are opted out
# of, the run says so twice -- here and beside the totals -- because a silent exclusion is what let
# agents report green locally without ever reaching HandlerCodegenTests. CI was never blind to it:
# ci.yml runs --filter "Category=Integration" over the whole solution in its own job. The gap was
# local-only, which is worse than it sounds -- every CLAUDE.md points agents at this script.
INTEGRATION_EXCLUDED=1
case "$TIER" in
    integration) CMD+=(--filter "Category=Integration"); INTEGRATION_EXCLUDED=0 ;;
    all)         INTEGRATION_EXCLUDED=0 ;;
    "")          CMD+=(--filter "Category!=E2E&Category!=Integration") ;;
    *)
        echo "Unknown tier '${2}'. The second argument, when given, must be 'integration' or 'all'." >&2
        rm -rf "$TRX_DIR"
        exit 2
        ;;
esac

echo "=== WALLOW TEST RUN ==="
echo "Filter: $SELECTOR_LABEL"
if [[ "$INTEGRATION_EXCLUDED" -eq 1 ]]; then
    echo "Integration tests: EXCLUDED (Category=Integration) -- ./scripts/run-tests.sh integration"
else
    echo "Integration tests: INCLUDED (Docker required; Testcontainers starts Postgres/Valkey)"
fi
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
TRX_COUNT=0
EMPTY_ASSEMBLIES=0
FAILED_TESTS=""

for trx in "$TRX_DIR"/*.trx; do
    [[ -f "$trx" ]] || continue

    TRX_COUNT=$((TRX_COUNT + 1))

    # Name the assembly from the TRX's own codeBase. The filename cannot: LogFilePrefix=results
    # yields results_<tfm>_<timestamp>.trx, so every project in a solution-wide run would print as
    # "net10.0" and the reader could not tell whether Wallow.Api.Tests was among them.
    # grep -m1 rather than `| head -1`: under `set -o pipefail`, head closing the pipe SIGPIPEs grep
    # and takes the whole script down with exit 141.
    ASSEMBLY=$(grep -o -m1 'codeBase="[^"]*"' "$trx" 2>/dev/null |
        sed 's/^codeBase="//;s/"$//;s#.*[/\\]##;s/\.dll$//' || true)
    if [[ -z "$ASSEMBLY" ]]; then
        ASSEMBLY=$(basename "$trx" | sed 's/^results_//' | sed 's/_[0-9T].*\.trx$//')
    fi

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

    # A category selector runs against every project in the solution, and most of them contain no
    # test in that category. Those are counted, not listed, so the assemblies that did run stay
    # readable.
    if [[ $((PASSED + FAILED + SKIPPED)) -eq 0 ]]; then
        EMPTY_ASSEMBLIES=$((EMPTY_ASSEMBLIES + 1))
        continue
    fi

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

if [[ "$EMPTY_ASSEMBLIES" -gt 0 ]]; then
    NOUN="assemblies"
    VERB="are"
    if [[ "$EMPTY_ASSEMBLIES" -eq 1 ]]; then
        NOUN="assembly"
        VERB="is"
    fi
    printf "(%d %s matched no test with this selector and %s omitted)\n" \
        "$EMPTY_ASSEMBLIES" "$NOUN" "$VERB"
fi

echo ""
echo "========================"
printf "TOTAL: %d tests | PASSED: %d | FAILED: %d | SKIPPED: %d\n" "$TOTAL" "$TOTAL_PASSED" "$TOTAL_FAILED" "$TOTAL_SKIPPED"

# The scope line rides immediately under the totals so a pasted count cannot be read as coverage it
# does not have.
if [[ "$INTEGRATION_EXCLUDED" -eq 1 ]]; then
    echo "SCOPE: fast suites only -- 0 integration tests ran (Category=Integration was filtered out)"
else
    echo "SCOPE: includes Category=Integration"
fi

EXIT_CODE=$TEST_EXIT

# A nonzero dotnet test exit means FAIL even when no test failures were recorded:
# a project that fails to COMPILE runs no tests and writes no TRX at all. So does a run that
# executed nothing: a selector that matches no test must never report PASS.
if [[ "$TOTAL_FAILED" -gt 0 || "$TEST_EXIT" -ne 0 || "$TOTAL" -eq 0 ]]; then
    if [[ "$TOTAL_FAILED" -gt 0 ]]; then
        echo ""
        echo "=== FAILED TESTS ==="
        echo "$FAILED_TESTS"
    elif [[ "$TRX_COUNT" -eq 0 ]]; then
        echo ""
        echo "No TRX result files were generated (found: $TRX_COUNT). The test project likely failed"
        echo "to build/compile - see the dotnet test output above."
    elif [[ "$TOTAL" -eq 0 ]]; then
        echo ""
        echo "No tests ran (TOTAL: 0). A run that exercises nothing is not a pass - the selector"
        echo "'$SELECTOR_LABEL' matched no test, or discovery failed. See the output above."
    else
        echo ""
        echo "No test failures were recorded, but dotnet test exited with code $TEST_EXIT."
        echo "See the dotnet test output above."
    fi
    echo ""
    echo "RESULT: FAIL"
    if [[ "$EXIT_CODE" -eq 0 ]]; then
        EXIT_CODE=1
    fi
else
    echo ""
    echo "RESULT: PASS"
fi

if [[ "$INTEGRATION_EXCLUDED" -eq 1 ]]; then
    cat <<'EOF'

========================
!! INTEGRATION TESTS DID NOT RUN !!
This invocation filtered out every test tagged Category=Integration, including the Wolverine
handler-codegen guards in Wallow.Api.Tests (which are the only check that every discovered handler
COMPILES) and every Testcontainers-backed suite. The result above says nothing about them.

  ./scripts/run-tests.sh integration    # only those suites, solution-wide (needs Docker)
  ./scripts/run-tests.sh all            # both, in one run (needs Docker)
========================
EOF
fi

# Cleanup
rm -rf "$TRX_DIR"

exit $EXIT_CODE
