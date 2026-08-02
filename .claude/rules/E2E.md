## E2E Test Rules

E2E suites are **per-app `@playwright/test` suites** inside each app: `apps/wallow-auth/e2e/`,
`apps/wallow-web/e2e/`, and wallow-web's three-origin `apps/wallow-web/e2e-cross-app/`.
`./scripts/e2e.sh` is the one-command backend-dependent runner — it brings up the containerised
stack, runs all three suites, and tears down. **Named `e2e.sh`, NOT `run-e2e.sh`.** The .NET xUnit
suite `Wallow.E2E.Tests` and `scripts/run-e2e.sh` are deleted — do not recreate them.

Read the suite's own guide before editing a spec; selectors, readiness, configs and backend
dependence all live there — `apps/wallow-auth/e2e/CLAUDE.md` and `apps/wallow-web/e2e/CLAUDE.md`
(which covers `e2e-cross-app/` too).
