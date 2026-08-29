## Testing rules

- **Backend: always `./scripts/run-tests.sh`** (`<module>` to narrow). **Never bare
  `dotnet test`** — the script supplies `api/tests/coverage.runsettings`, which excludes
  generated code from coverage.
- **A bare backend run does NOT cover integration tests.** `Category=Integration` runs only via
  `./scripts/run-tests.sh integration` or `all` (both need Docker); as a second argument,
  `integration` narrows the first argument's target (`./scripts/run-tests.sh api integration`).
  **Do not report a backend change green off a bare run alone.**
- **DOM tests run in a REAL browser via Vitest browser mode** (headless Chromium). **NEVER
  jsdom, happy-dom, or jest** — no `// @vitest-environment jsdom` pragma, no jsdom/happy-dom
  `devDependency`.
- **Specs lint via `pnpm lint:tests`, not `pnpm lint`.** Playwright `e2e/**/*.spec.ts` files
  are not `*.test.*`, so they lint on the source side.
- **Spec helpers live in `@bc-solutions-coder/testing`, never in an app.**
- **No source tests.** A spec never reads application source, README prose, or directory
  layout off disk — constraining how code is written is a linter's job (`wallow/*` rule;
  prefer deleting the constraint to relocating it). `wallow/no-source-tests` enforces this by
  banning `node:fs` in specs. Exemptions are one override block near the end of the root
  `.oxlintrc.json`, in three classes only: tool-output guardrails, artifact readers,
  runtime/compile-time identity. Do not grow the list outside those classes.

Read `packages/testing/CLAUDE.md` before writing or editing a frontend spec. E2E rules:
`.claude/rules/E2E.md`.
