## E2E test rules

- Per-app `@playwright/test` suites: `apps/wallow-auth/e2e/`, `apps/wallow-web/e2e/`, and
  wallow-web's three-origin `apps/wallow-web/e2e-cross-app/`. No .NET E2E suite — do not
  create one.
- **`./scripts/e2e.sh`** (not `run-e2e.sh`) is the one-command backend-dependent runner:
  brings up the containerised stack, runs all three suites, tears down.
- **`E2E_SKIP_IMAGE_BUILD=1` is the single opt-in to reuse existing images.** Set it only when
  something else already built the images for the current tree (CI does) — never just to make
  a local run faster.
- Read the suite's own guide before editing a spec: `apps/wallow-auth/e2e/CLAUDE.md` and
  `apps/wallow-web/e2e/CLAUDE.md` (covers `e2e-cross-app/` too).
