## E2E Test Rules

E2E suites are **per-app `@playwright/test` suites**: `apps/wallow-auth/e2e/`,
`apps/wallow-web/e2e/`, and wallow-web's three-origin `apps/wallow-web/e2e-cross-app/`.
`./scripts/e2e.sh` is the one-command backend-dependent runner — it brings up the containerised
stack, runs all three suites, and tears down. **Named `e2e.sh`, NOT `run-e2e.sh`**; there is no
.NET E2E suite — do not create one.

**`./scripts/e2e.sh` rebuilds the app images on every run** (compose `--build`), so the run
always tests the tree you are sitting on. **`E2E_SKIP_IMAGE_BUILD=1` is the single opt-in to
reuse existing images**: it suppresses both the `dotnet publish` of the API/migration/seeder
images and compose's `--build`. Set it only when something else already built the images for the
current tree (CI does, from a cache-restored prior job) — never just to make a local run faster;
layer caching already makes an unchanged rebuild cheap.

Read the suite's own guide before editing a spec — selectors, readiness, configs and backend
dependence live in `apps/wallow-auth/e2e/CLAUDE.md` and `apps/wallow-web/e2e/CLAUDE.md`
(which covers `e2e-cross-app/` too).
