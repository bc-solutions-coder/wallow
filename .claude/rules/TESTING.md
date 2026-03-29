## Testing Rules

- **Always use the test script:** `./scripts/run-tests.sh` for all tests, `./scripts/run-tests.sh <module>` for a specific module
  - The script outputs structured per-assembly pass/fail counts and lists individual failed test names
  - Supported module shorthands: `identity`, `billing`, `storage`, `notifications`, `messaging`, `announcements`, `inquiries`, `branding`, `apikeys`, `auth`, `auth-components`, `web`, `web-components`, `e2e`, `api`, `arch` (or `architecture`), `shared`, `kernel`, `integration`
  - You can also pass a full project path: `./scripts/run-tests.sh tests/Modules/Billing/Wallow.Billing.Tests`
- Never run bare `dotnet test` — always use the script which includes `--settings tests/coverage.runsettings` automatically
- Never run `dotnet test --collect:"XPlat Code Coverage"` without `--settings tests/coverage.runsettings` — it includes generated code and inflates uncovered lines
- Coverage exclusions are defined in `tests/coverage.runsettings` — do not duplicate them elsewhere
- **E2E tests require live infrastructure.** When working on E2E tests, always run them with `./scripts/run-e2e.sh` which handles the full lifecycle (build images, start stack, run tests, teardown). See `.claude/rules/E2E.md` for details. Never skip E2E verification when modifying E2E test code.
