## Testing Rules

- **Backend: always use `./scripts/run-tests.sh`** (or `./scripts/run-tests.sh <module>` for one
  module). **Never run bare `dotnet test`** — the script supplies
  `--settings api/tests/coverage.runsettings`, which excludes generated code; without it uncovered
  lines are inflated.
- **Frontend: `pnpm test`** (= `pnpm -r test` = `vitest run` per package).
- **DOM tests run in a REAL browser via Vitest browser mode.** Any spec that touches the DOM
  (renders a component, reads layout/focus/computed styles) runs in headless Chromium.
  **NEVER jsdom, NEVER happy-dom, NEVER jest** — those simulate the DOM and are banned in this
  repo. Do not add a `// @vitest-environment jsdom` pragma or a jsdom/happy-dom `devDependency`.
- **Specs are linted by `pnpm lint:tests`, not `pnpm lint`.** `pnpm lint` covers source only;
  `scripts/lint-tests.sh` is the second pass and enables oxlint's vitest plugin. `pnpm check` runs
  both. Playwright `e2e/**/*.spec.ts` files are not `*.test.*`, so they lint on the source side.
- **Spec helpers live in `@bc-solutions-coder/testing`, never in an app.** A helper under an app's
  `src/shared/testing/` is a helper the other app cannot use.

Before writing or editing a frontend spec, read **`packages/testing/CLAUDE.md`** — project split,
the no-mocking rule, test-comment standards, and the browser-mode facts that bite all live there.
E2E rules live in **`.claude/rules/E2E.md`**.
