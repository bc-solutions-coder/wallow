## Testing Rules

- **Always use the test script:** `./scripts/run-tests.sh` (or `./scripts/run-tests.sh <module>` for one
  module — run without args or see the script for supported shorthands). It logs to TRX and reports
  structured per-assembly pass/fail counts with failed test names.
- **Never run bare `dotnet test`** — the script includes `--settings api/tests/coverage.runsettings`
  automatically (excludes generated code; running without it inflates uncovered lines). Coverage exclusions
  live only in that runsettings file — do not duplicate them.

### Frontend (JS/TS) tests — `pnpm test`

- **Component/DOM tests run in a REAL browser via Vitest browser mode.** Any spec that touches the DOM
  (renders a component, reads layout/focus/computed styles) runs in headless Chromium through the Vitest
  `playwright` provider (`@vitest/browser-playwright`), with `vitest-browser-react` supplying `render`
  and the locator API. **NEVER jsdom, NEVER happy-dom, NEVER jest** — those simulate the DOM and are
  banned in this repo. Do not add a `// @vitest-environment jsdom` pragma or a jsdom/happy-dom
  `devDependency`; that regresses the suite off real-browser fidelity.
- **Vitest runs a two-project split** (see each app's `vitest.config.ts`): a **node** project for pure-logic
  specs (`src/**/*.test.ts` plus the rare render-nothing `*.test.tsx`) and a **browser** project for every
  component spec (`src/**/*.test.tsx`, headless Chromium). `pnpm test` (= `pnpm -r test` = `vitest run` per
  package) drives both. Component assertions come from `@vitest/expect` locator matchers, not
  `@testing-library/jest-dom`.
- **`packages/ui` runs a THIRD vitest project, `storybook`.** `@storybook/addon-vitest` executes every
  `src/components/<name>/<name>.stories.tsx` as a test case in the same headless Chromium — with the real
  Tailwind pipeline and fork theme attached, which the plain `browser` project does not load. Stories are
  therefore the component library's render/interaction coverage; a co-located `*.test.tsx` there covers only
  behavioural edges a story cannot express (data-attribute state, className-override-wins, keyboard
  interaction). Do not duplicate story coverage into a test file. See `packages/ui/CLAUDE.md`.
- **Never mock `@bc-solutions-coder/ui`.** The component library's own tests mock nothing (real Base UI
  parts, real Chromium, real design tokens; the only permitted double is a userland callback spy such as
  `onClick={fn()}`), and apps must not stub it out either — no `vi.mock("@bc-solutions-coder/ui")` replacing
  components with hand-rolled placeholders. A spec that finds a real component awkward to drive is telling
  you the component or the spec is wrong, not that it needs a stub. The one legitimate exception already in
  the tree is the apps' `__root*.test.tsx` SSR-isolation specs, which use `vi.mock(..., importOriginal)` to
  spread the real module and override *only* `FocusOnNavigate`/`DocumentStyles` as render-nothing sentinels
  (`renderToString` has no router context) — a partial override for SSR isolation, not a replacement.
- **E2E tests are per-app Playwright suites** — `apps/wallow-auth/e2e/` and `apps/wallow-web/e2e/`. Run them
  with `pnpm --filter ./apps/wallow-auth test:e2e` (or `./apps/wallow-web`), or `./scripts/e2e.sh` for the
  full backend-dependent runner. Never skip E2E verification when modifying E2E test code.
  Backend-dependent specs need live infrastructure. See `E2E.md`.
