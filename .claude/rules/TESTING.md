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
  spec, and reaches **every spec in the repo**: the root `.oxlintrc.json` both registers the plugin
  and turns this one rule on repo-wide, so a package with no nested config of its own is covered
  too. **Seven specs** are deliberately outside it and stay that way — the exemption list is a single
  override block near the end of the root `.oxlintrc.json`, in three classes:
  - **Tool-output guardrails** — `packages/lint/src/fixtures.test.ts` and
    `packages/sdk/src/oxlint-guardrails.test.ts` run the real oxlint binary over fixture files, so
    they assert a **tool's** behaviour, not source text.
  - **Artifact readers** — `packages/sdk/src/generated-query-surface.test.ts`,
    `packages/sdk/src/openapi-regen.test.ts`, `packages/styles/src/assets.test.ts` and
    `packages/styles/src/theme-css.test.ts`. Parsing a committed data contract
    (`packages/sdk/openapi/v1.json`, `packages/styles/styles.css`) and checking it against the
    runtime modules generated from it is fine: an artifact is not source.
  - **Runtime/compile-time identity** — `packages/query/src/index.test.ts` checks the facade's
    exported surface against the real package it re-exports.

  `@bc-solutions-coder/testing`'s `./browser-styles-wiring` is **not** on that list and is not a
  spec: it is an exported helper module that reads a consumer's build config to prove its browser
  project has a stylesheet attached.

Before writing or editing a frontend spec, read **`packages/testing/CLAUDE.md`** — project split,
the no-mocking rule, test-comment standards, and the browser-mode facts that bite all live there.
E2E rules live in **`.claude/rules/E2E.md`**.
