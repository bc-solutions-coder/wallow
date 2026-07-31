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
- **Specs are linted by `pnpm lint:tests`, not `pnpm lint`.** `pnpm lint` covers source only; the test
  and story files it excludes are linted by a second pass (`scripts/lint-tests.sh`) that additionally
  enables oxlint's **vitest plugin** — `no-focused-tests`, `no-disabled-tests`, `valid-title`,
  `no-standalone-expect` and the rest of its correctness set. `pnpm check` and CI run both passes; if
  you lint by hand after touching a spec, run `pnpm lint:tests`. Playwright `e2e/**/*.spec.ts` files are
  not `*.test.*`, so they stay on the **source** side and get no vitest rules.
- **E2E tests are per-app Playwright suites** — `apps/wallow-auth/e2e/` and `apps/wallow-web/e2e/`. Run them
  with `pnpm --filter ./apps/wallow-auth test:e2e` (or `./apps/wallow-web`), or `./scripts/e2e.sh` for the
  full backend-dependent runner. Never skip E2E verification when modifying E2E test code.
  Backend-dependent specs need live infrastructure. See `E2E.md`.
- **Running a subset by hand needs `--configLoader runner`.** The apps' own `test` scripts pass it; a bare
  `pnpm --filter ./apps/<app> exec vitest run <paths>` does not, and without it vitest cannot resolve
  `packages/styles/src/assets` and dies with `ERR_MODULE_NOT_FOUND` before running a single test — which
  looks like a broken spec but is the missing flag.

### Test comments

A spec's header states **what the file covers**, plus any non-obvious reason a reader would otherwise
"fix" the test wrongly. **Maximum 8 lines. Present tense.** A statement earns its place only if it is
all three of: true now, non-obvious from the code, and able to prevent a wrong edit. Inline comments
only where a single assertion is genuinely surprising — default to none.

Delete on sight, in any spec you touch:

| Category | Examples |
| --- | --- |
| Bead IDs / plan refs | `Wallow-vec7.3.11`, `(2.8a)`, `scout inventory on ...` |
| History verbs | `used to`, `no longer`, `replaces`, `was previously`, `this file used to carry` |
| Line citations into other files | `AccountController.cs:65-165`, `packages/sdk/src/auth-oidc.ts:42` |
| Scope disclaimers | `deliberately says NOTHING about`, `out of scope for this spec` |
| Restatement | any sentence paraphrasing the `it()` below it |
| Decorative rules | `── SECTION ─────`, ASCII banners, box drawing |

These are not style nits. Stale prose is load-bearing to whoever reads it next: a comment naming a
constraint that no longer exists makes the next reader route around a problem the code does not have.
A comment that cites a line number is wrong the moment either file moves.

**A spec asserting that a migration happened is finished work, not coverage.** "The restyle landed",
"this uses the catalog now" — delete it. So is any `it()` whose body only reads `element.classList`
(the `shared/testing/style-contract.ts` helpers): a component can render the right classes and be
broken, or restyle correctly with different classes and fail. Assert the computed value, never the
class string — `cn()` merges a caller's `className` over the recipe. Where a lint rule in
`tools/oxlint/wallow-lint-plugin.js` already says it, the spec is redundant by construction.

### Browser-mode facts that bite

Each of these cost a debugging session and is invisible from the code:

- **`location` is `[Unforgeable]` in real Chromium.** `vi.stubGlobal("location", …)` cannot shadow it,
  and a screen assigning `globalThis.location.href` navigates the iframe and tears the runner down.
  Observe a full-page hand-off at the Navigation API `navigate` event: read `event.destination.url`
  and `preventDefault()` so the runner stays put.
- **`createSdkHarness()` records a call BEFORE its responder runs.** The earliest non-racy point at
  which "the request is in flight" is a fact is `await vi.waitFor(() => expect(harness.calls).toHaveLength(n))`
  — not the click, and not the settle helper, which resolves only after the response is parsed. Every
  pending/disabled-state assertion depends on this.
- **Ask the generated factory for a query key; never spell one as a literal.** The key carries the
  client's `baseUrl`, so a drifted literal makes `getQueryData`/`getQueryState` return `undefined`
  rather than fail — a no-op assertion every implementation passes. Keys are flat with no prefix to
  sweep by, so assert an invalidation by *behaviour*: run the real predicate against the real
  `{op}QueryKey()`.
- **`getByText` matches by SUBSTRING.** Exact matching needs `{ exact: true }`. Two fixtures whose
  names overlap ("Globex" also matches "globex.io") make a spec pass for the wrong element.
- **A browser project with no stylesheet fails in two misleading ways.** No Tailwind: a catalog control
  has no box (`Checkbox.Root`'s `<span role="checkbox">` measures 0×0) and every click hangs to
  Playwright's ~15s actionability timeout. No fork theme: every colour token is a VALUELESS custom
  property, so `bg-card` paints `rgba(0, 0, 0, 0)` and colour assertions pass vacuously.
- **Catalog controls are not native elements.** `Select` renders `role="option"` divs portalled to
  `<body>` and only while open (drive it with `chooseOption`; `userEvent.selectOptions` only drives an
  `HTMLSelectElement`). `Checkbox` renders a `<span role="checkbox">` beside a hidden input — assert
  `role`/`aria-checked`, never `type="checkbox"`. `ListRow` derives its testid as `{name}-item` and
  that derivation **cannot** be overridden — the exact inverse of the form-field rule, where an
  explicit `testId` overrides both the field and its `-error` id.
- **Normalise colours through a canvas.** The fork palette is oklch and Chromium may serialize
  `oklch()`/`color()`; regex-parsing `getComputedStyle` output is unstable. Paint the string into a 2d
  context and read the sRGB bytes.
- **Pointer position persists across spec files**, and the browser re-evaluates `:hover` when new
  content mounts under it — park the pointer before mounting anything whose rest-state colour you measure.
- **An early `return` out of a `useAppForm` `onSubmit` RESOLVES the form's mutation**, so `onSuccess`
  fires and the user is navigated as though the write happened. A guard clause needs a test asserting
  the navigation did *not* happen, not merely that no request went out.
- **TanStack Router JSON-parses search values before `validateSearch`** — `?scope=123` arrives as a
  `number`. Route schemas must accept the parsed type.
- **A screen may not import `WallowError`** (SDK `./server` entry only), so error narrowing in app code
  is structural (`error.status === 400`), never `instanceof`.
- **Assert a feature seam by identity, not presence.** `expect(api.foo).toBe(sdkFoo)`, not
  `toBeDefined()` — a hand-written look-alike carries the same name, shape and type, passes every
  behavioural spec, and reaches an undocumented endpoint.
