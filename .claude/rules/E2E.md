## E2E Test Rules

E2E suites are **per-app `@playwright/test` suites** inside each app: `apps/wallow-auth/e2e/`,
`apps/wallow-web/e2e/`, and wallow-web's three-origin `apps/wallow-web/e2e-cross-app/`.
`./scripts/e2e.sh` is the one-command backend-dependent runner — it brings up the containerised
stack, runs all three suites, and tears down. **Named `e2e.sh`, NOT `run-e2e.sh`.** The .NET xUnit
suite `Wallow.E2E.Tests` and `scripts/run-e2e.sh` are deleted — do not recreate them.

**`./scripts/e2e.sh` rebuilds the app images on every run.** Compose builds a service's image
only when one is ABSENT, so before this it reused any leftover `wallow-web-react:test` /
`wallow-auth-react:test` however far the tree had moved — green E2E proving nothing about the code
under test (Wallow-gwy2). The runner now passes `--build`, and **`E2E_SKIP_IMAGE_BUILD=1` is the
single opt-in to reuse**: it suppresses both the `dotnet publish` of the API/migration/seeder
images and compose's `--build` of the ones with a build block. Set it only when something else has
already built the images for the current tree — CI does, from a cache-restored prior job. Never set
it to make a local run faster; layer caching already makes an unchanged rebuild cheap, and the
whole point of the flag being unset is that the run tests the tree you are sitting on.

Read the suite's own guide before editing a spec; selectors, readiness, configs and backend
dependence all live there — `apps/wallow-auth/e2e/CLAUDE.md` and `apps/wallow-web/e2e/CLAUDE.md`
(which covers `e2e-cross-app/` too).
