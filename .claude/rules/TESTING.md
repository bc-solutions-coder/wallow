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
- **No source tests.** A test never reads application source, README prose, or directory layout off
  disk — no `readFileSync` over `src/`, no comment stripping, no assertions about file anatomy,
  imports, or class strings. Constraining how code is *written* is a **linter's** job: a `wallow/*`
  AST rule fires in the editor, names the offending line, and cannot be defeated by formatting
  (`packages/lint/CLAUDE.md`). A test calls a function or renders a component and asserts what
  happens. Most structural sweeps should not become rules either — a spec pinning file counts or
  README wording makes the codebase rigid without making it correct; **prefer deleting the
  constraint to relocating it.** `wallow/no-source-tests` enforces this by banning `node:fs` in a
  spec, and reaches every spec under the five trees that register the plugin (both apps,
  `packages/ui`, `packages/forms`, `packages/navigation`). Elsewhere the doctrine still holds; it is
  just unenforced. Three specs are deliberately outside it and stay that way — `packages/lint`'s and
  `packages/sdk`'s guardrail specs run the real oxlint binary over fixture files, and
  `@bc-solutions-coder/testing`'s `./browser-styles-wiring` reads a consumer's build config to prove
  its browser project has a stylesheet attached. All three assert a **tool's** behaviour, not source
  text. Parsing a committed data contract (`packages/sdk/openapi/v1.json`,
  `packages/styles/styles.css`) and checking it against the runtime modules generated from it is
  also fine: an artifact is not source.

Before writing or editing a frontend spec, read **`packages/testing/CLAUDE.md`** — project split,
the no-mocking rule, test-comment standards, and the browser-mode facts that bite all live there.
E2E rules live in **`.claude/rules/E2E.md`**.
