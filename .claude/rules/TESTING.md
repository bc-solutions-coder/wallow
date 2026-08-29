## Testing Rules

- **Backend: always use `./scripts/run-tests.sh`** (or `./scripts/run-tests.sh <module>` for one
  module). **Never run bare `dotnet test`** — the script supplies
  `--settings api/tests/coverage.runsettings`, which excludes generated code; without it uncovered
  lines are inflated.
- **A bare backend run does NOT cover integration tests.** Everything tagged
  `Category=Integration` (Wolverine handler-codegen guards, Testcontainers-backed suites) runs
  only via `./scripts/run-tests.sh integration` (only those) or `./scripts/run-tests.sh all`
  (both); both need Docker and select by category across the whole solution. As a **second**
  argument, `integration` narrows to the first argument's target
  (`./scripts/run-tests.sh api integration`). A run that excludes them says so beside its totals —
  **do not report a backend change green off a bare run alone.**
- **Frontend: `pnpm test`** (vitest run per package).
- **DOM tests run in a REAL browser via Vitest browser mode** (headless Chromium).
  **NEVER jsdom, NEVER happy-dom, NEVER jest** — no `// @vitest-environment jsdom` pragma, no
  jsdom/happy-dom `devDependency`.
- **Specs are linted by `pnpm lint:tests`, not `pnpm lint`** (`pnpm check` runs both). Playwright
  `e2e/**/*.spec.ts` files are not `*.test.*`, so they lint on the source side.
- **Spec helpers live in `@bc-solutions-coder/testing`, never in an app.**
- **No source tests.** A test never reads application source, README prose, or directory layout
  off disk — no `readFileSync` over `src/`, no assertions about file anatomy, imports, or class
  strings. Constraining how code is *written* is a **linter's** job (a `wallow/*` AST rule —
  `packages/lint/CLAUDE.md`); a test calls a function or renders a component and asserts what
  happens. Most structural sweeps should not become lint rules either — **prefer deleting the
  constraint to relocating it.** `wallow/no-source-tests` enforces this repo-wide by banning
  `node:fs` in specs. A small fixed set of specs is deliberately exempt — the list is a single
  override block near the end of the root `.oxlintrc.json`, in three sanctioned classes only:
  **tool-output guardrails** (run the real oxlint binary over fixtures), **artifact readers**
  (parse a committed data contract like `packages/sdk/openapi/v1.json` against its generated
  runtime — an artifact is not source), and **runtime/compile-time identity** (a facade's exports
  vs. the package it re-exports). Do not grow the list outside those classes.

Before writing or editing a frontend spec, read **`packages/testing/CLAUDE.md`** — project split,
the no-mocking rule, test-comment standards, and browser-mode gotchas live there. E2E rules:
**`.claude/rules/E2E.md`**.
