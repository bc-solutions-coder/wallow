# packages/testing — @bc-solutions-coder/testing Agent Guide

The shared **Vitest preset** and browser-mode test utilities. Consumers get their two-project
node/browser split from `createVitestProjects()` rather than hand-rolling one
(`packages/logger` deliberately spells its own project pair out, with a comment saying why;
its devDependency here buys only the escape guards).

## Subpath-per-entry — the split is load-bearing

Every helper gets its OWN entry rather than riding the barrel, because the barrel is loaded in
a plain Node process at Vitest config time. One browser-only import on it breaks every config
in the workspace.

| Entry                             | Imported at                             | What it is                                                                                                                                                                                                               |
| --------------------------------- | --------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `.` (`src/index.ts`)              | Vitest **config load** (plain Node)     | `createVitestProjects()` → the `{ node, browser }` project pair, plus `browserOptimizeDepsBaseline` / `mergeOptimizeDeps`.                                                                                               |
| `./render`                        | Inside a **browser-mode spec**          | `render` from `vitest-browser-react` — the single seam where shared providers would be added.                                                                                                                            |
| `./render-with-wallow`            | Inside a **browser-mode spec**          | `render` wrapped in the router + query providers a screen needs.                                                                                                                                                         |
| `./contrast`                      | Inside a **browser-mode spec**          | Measured-colour helpers: `parseColor` / `computedColor` / `effectiveBackground` / `contrastRatio` / `textContrast`. Reads what a component PAINTS.                                                                       |
| `./locators`                      | Inside a **browser-mode spec**          | `byTestId` and friends — the one way a spec reaches an element.                                                                                                                                                          |
| `./catalog-select`                | Inside a **browser-mode spec**          | `chooseOption` / `expectCatalogSelect` — a catalog `Select` portals `role="option"` divs to `<body>` only while open, so `userEvent.selectOptions` cannot drive it.                                                      |
| `./theme-wiring`                  | Inside a **browser-mode spec**          | `assertThemeWiring({ tokens, probeClass })` — the consumer's whole spec file is one call.                                                                                                                                |
| `./sdk-harness`                   | **Any** project                         | `createSdkHarness` / `createPassthroughHarness`, plus `routeHarness` / `failsWith` / `neverSettles` re-exported. Imports no `vitest`.                                                                                    |
| `./invalidation`                  | Inside a **spec**                       | Runs a real `invalidations` predicate against a real generated query key.                                                                                                                                                |
| `./browser-deps`                  | Inside a **node-project spec**          | `describeBrowserPreBundleList()` — every `optimizeDeps.include` entry in a consumer's browser project actually resolves. Spawns child processes; keep it off the barrel. Also `browserPreBundleList()`.                  |
| `./browser-mode-smoke`            | Inside a **browser-mode spec**          | `assertBrowserModeSmoke(appName)` — the browser project really is headless Chromium (real userAgent, real layout engine).                                                                                                |
| `./browser-styles-wiring`         | Inside a **node-project spec**          | `assertBrowserStylesWiring({ appDir, extraSpecs })` — reads the consumer's config/setup/stylesheet off disk. Node-only.                                                                                                  |
| `./navigation-escape`             | A browser project's **setup file**      | `installNavigationEscapeGuard` + `assertNoNavigationEscape` — vetoes a cross-document hand-off at the Navigation API `navigate` event. `expectNavigationEscape` / `consumeNavigationEscapes` read back a deliberate one. |
| `./router-stub`                   | Inside a **spec** that mocks the router | `assertRouterStubApplied` — a `beforeEach` guard for `vi.mock("@tanstack/react-router")` specs; fails the file by NAME if the mocker serves the real module. The stub anchor carries `data-router-stub="true"`.          |
| `./console-guard`                 | A browser project's **setup file**      | `installConsoleGuard` + `assertNoConsoleNoise` — wraps (never replaces) `console.error`/`warn` so React noise fails the test that produced it. `consumeConsoleNoise` / `expectConsoleError` for deliberate error paths.  |
| `./network-escape`                | A browser project's **setup file**      | `installNetworkEscapeGuard` + `assertNoNetworkEscape` — traffic reaching `globalThis.fetch` that no harness owns is answered with a 503 naming the request. `consumeNetworkEscapes` for deliberate ones.                 |
| `./node-async-hooks-browser-shim` | A browser-project `resolve.alias`       | Real in-browser `AsyncLocalStorage` answering "no scope", for apps whose router pulls `node:async_hooks`.                                                                                                                |

- **The two wiring guards are a PAIR, and each fails differently.** `./theme-wiring` measures
  what the browser paints — the assertion that matters. `./browser-styles-wiring` names the
  pieces on disk, so a removed one fails saying WHICH, rather than as a pile of actionability
  timeouts (no utilities) or vacuously-passing transparent colours (no theme).
- **Keep `render` off the barrel.** `vitest-browser-react` evaluates `vitest/browser` at
  import and throws outside browser mode.
- **`./contrast` parses colours through a canvas, not a regex.** The fork palette is
  `oklch(...)` and Chromium preserves the authored colour space in computed values, so an
  `rgb()` matcher silently fails on the exact tokens this repo uses. Painting the string and
  reading the pixel back normalises any syntax to sRGB. Browser-only — off the barrel.
- **The preset styles nothing.** A consumer that needs real CSS passes `wallowStyles()` as
  `browserPlugins` and its setup file as `browserSetupFiles`, plus a root-level
  `vitest-styles.css` the setup file imports next to `virtual:wallow-theme.css` (see
  `apps/wallow-web`). Both options are pure pass-throughs — that keeps
  `@bc-solutions-coder/styles` out of this package's dependencies. The Tailwind entry cannot
  be hoisted here (Tailwind v4 resolves `@source` relative to the declaring stylesheet).
- **A render-nothing `*.test.tsx` spec is named `*.ssr.test.tsx`** — the preset's
  `ssrSpecGlob` and `nodeTsxSpecs`' default. A spec qualifies when it renders through
  `react-dom/server` or asserts a `beforeLoad` redirect, i.e. never mounts a DOM. Passing
  `nodeTsxSpecs` explicitly REPLACES the convention rather than extending it.
- The browser project uses the Vitest 4 **factory** provider `playwright()`, not the v3
  `"playwright"` string (which throws). Chromium only, headless.
- App-local knobs (`resolve.alias`, `server.deps.inline`) belong in the app's config, passed
  through `nodeProjectOverrides` / `nodeTsxSpecs` / `extraBrowserOptimizeDeps` /
  `browserPlugins` / `browserSetupFiles` — no app specifics hardcoded here. What a project
  shares with the ROOT config (`resolve.tsconfigPaths`, `ssr.noExternal`) is spelled once at
  the root and pulled in with `extends: true`; a project-level `resolve` MERGES into the
  inherited one, so adding an alias keeps the root's `tsconfigPaths`.
- **An unresolvable `optimizeDeps.include` entry is a WARNING Vite ignores**, so a list can
  look complete while pre-bundling nothing — and the dropped entry never reaches the dep-cache
  hash, which turns the resulting duplicate-React failure intermittent. `./browser-deps` is
  the guard; every consumer with a browser project calls it. Under pnpm the cause is almost
  always non-declaration; the fix is a `package.json` line. Base UI is named by the glob
  `@base-ui/react/*` (expanded against that package's `exports` keys) — do not list subpaths
  by hand.
- **An app's wiring guards live DIRECTLY under its `src/`** — each imports `../vite.config` or
  `../vitest.config`, which `wallow/zone-dag` tolerates only for single-segment `src/*` paths
  (`ROOT_ZONE`). The apps spell this as exactly two files: `src/app-wiring.test.ts` (node) and
  `src/app-wiring.browser.test.tsx` (browser). They stay two because the preset routes
  projects by EXTENSION — folded into the node file, the browser guards would evaluate the
  whole feature graph under `environment: "node"`.
- Scripts: `pnpm --filter @bc-solutions-coder/testing build` (Vite lib mode +
  `tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`.

## How a consumer's suite runs

- **Vitest runs a two-project split** (see each app's `vitest.config.ts`): a **node** project
  for pure-logic specs (`src/**/*.test.ts` plus render-nothing `*.ssr.test.tsx`) and a
  **browser** project for every component spec (`src/**/*.test.tsx`, headless Chromium).
  Component assertions come from `@vitest/expect` locator matchers, not
  `@testing-library/jest-dom`.
- **`packages/ui` runs a THIRD project, `storybook`** — stories are that package's
  render/interaction coverage; see `packages/ui/CLAUDE.md`.
- **Running a subset by hand needs `--configLoader runner`.** The apps' own `test` scripts
  pass it; a bare `pnpm --filter ./apps/<app> exec vitest run <paths>` does not, and without
  it vitest cannot resolve `packages/styles/src/assets` and dies with `ERR_MODULE_NOT_FOUND`
  before running a single test — which looks like a broken spec but is the missing flag.

## Never mock `@bc-solutions-coder/ui`

The component library's own tests mock nothing (real Base UI parts, real Chromium, real
tokens; the only permitted double is a userland callback spy), and apps must not stub it out
either — no `vi.mock("@bc-solutions-coder/ui")` with hand-rolled placeholders. A spec that
finds a real component awkward to drive is telling you the component or the spec is wrong.

The one legitimate exception is the apps' `__root*.test.tsx` SSR-isolation specs, which use
`vi.mock(..., importOriginal)` to spread the real module and override _only_
`FocusOnNavigate`/`DocumentStyles` as render-nothing sentinels (`renderToString` has no router
context) — a partial override, not a replacement.

## Test comments

A spec's header states **what the file covers**, plus any non-obvious reason a reader would
otherwise "fix" the test wrongly. **Maximum 8 lines. Present tense.** A statement earns its
place only if it is all three of: true now, non-obvious from the code, and able to prevent a
wrong edit. Inline comments only where a single assertion is genuinely surprising.

Delete on sight, in any spec you touch:

| Category                        | Examples                                                                        |
| ------------------------------- | ------------------------------------------------------------------------------- |
| Issue IDs / plan refs           | `#97`, `(2.8a)`, `scout inventory on ...`                                       |
| History verbs                   | `used to`, `no longer`, `replaces`, `was previously`, `this file used to carry` |
| Line citations into other files | `AccountController.cs:65-165`, `packages/sdk/src/auth-oidc.ts:42`               |
| Scope disclaimers               | `deliberately says NOTHING about`, `out of scope for this spec`                 |
| Restatement                     | any sentence paraphrasing the `it()` below it                                   |
| Decorative rules                | `── SECTION ─────`, ASCII banners, box drawing                                  |

Stale prose is load-bearing to whoever reads it next: a comment naming a constraint that no
longer exists makes the next reader route around a problem the code does not have.

**A spec asserting that a migration happened is finished work, not coverage.** So is any
`it()` whose body only reads `element.classList`: a component can render the right classes and
be broken, or restyle correctly with different classes and fail. Assert the computed value,
never the class string — `cn()` merges a caller's `className` over the recipe. Where a
`wallow/*` lint rule already says it, the spec is redundant by construction.

## Browser-mode facts that bite

Each of these is invisible from the code:

- **`location` is `[Unforgeable]` in real Chromium.** `vi.stubGlobal("location", …)` cannot
  shadow it, and a screen assigning `globalThis.location.href` navigates the iframe and tears
  the runner down. Observe a full-page hand-off at the Navigation API `navigate` event
  (`event.destination.url` + `preventDefault()`). `./navigation-escape` is that guard, and a
  project installs it in its browser SETUP file — never per spec, because the file that leaks
  is not the file the runner blames. **Every browser project installs the guard TRIO**
  (navigation, console, network) through its setup door; `packages/ui`'s `storybook` project
  takes them through `.storybook/preview.tsx`'s `beforeEach`/`afterEach` exports because
  `storybookTest()` never reads `browserSetupFiles`. `createVitestProjects()`'s
  `browserSetupFiles` defaults to `[]`, so a NEW browser project has no guard until its config
  passes a setup file — wire one on day one. A spec asserting a DELIBERATE hand-off consumes
  the guard's record (`expectNavigationEscape()` awaits exactly one and clears what it read;
  `consumeNavigationEscapes()` drains all) rather than registering a listener beside it — an
  unconsumed escape still fails the test that leaked it. wallow-auth rides on this: its
  screens hand the browser off for real to finish OIDC flows, and their specs assert those
  hand-offs through these helpers.
- **Fill a field with `userEvent.fill`, not `userEvent.type`.** `type` costs one CDP round
  trip PER CHARACTER, and under a full `pnpm test` — every package's browser project driving
  its own Chromium at once — the round-trip count amplifies into intermittent CI timeouts.
  Reach for `type` only when the spec needs keyboard syntax (`{Shift}`, `{Backspace}`) or
  appends to an existing value — `fill` REPLACES, so converting a sequence that builds one
  string silently changes it.
- **`createSdkHarness()` records a call BEFORE its responder runs.** The earliest non-racy
  point at which "the request is in flight" is a fact is
  `await vi.waitFor(() => expect(harness.calls).toHaveLength(n))` — not the click, and not the
  settle helper, which resolves only after the response is parsed. Every
  pending/disabled-state assertion depends on this.
- **Ask the generated factory for a query key; never spell one as a literal.** The key carries
  the client's `baseUrl`, so a drifted literal makes `getQueryData`/`getQueryState` return
  `undefined` rather than fail — a no-op assertion every implementation passes. Keys are flat
  with no prefix to sweep by, so assert an invalidation by _behaviour_: run the real predicate
  against the real `{op}QueryKey()`.
- **`getByText` matches by SUBSTRING.** Exact matching needs `{ exact: true }` — two fixtures
  whose names overlap ("Globex" also matches "globex.io") make a spec pass for the wrong
  element.
- **A browser project with no stylesheet fails in two misleading ways.** No Tailwind: a
  catalog control has no box (`Checkbox.Root`'s `<span role="checkbox">` measures 0×0) and
  every click hangs to Playwright's actionability timeout. No fork theme: every colour token
  is a VALUELESS custom property, so `bg-card` paints `rgba(0, 0, 0, 0)` and colour assertions
  pass vacuously.
- **Catalog controls are not native elements.** `Select` renders `role="option"` divs
  portalled to `<body>` only while open (drive with `chooseOption`; `userEvent.selectOptions`
  only drives an `HTMLSelectElement`). `Checkbox` renders a `<span role="checkbox">` beside a
  hidden input — assert `role`/`aria-checked`, never `type="checkbox"`. `ListRow` derives its
  testid as `{name}-item` and that derivation **cannot** be overridden — the exact inverse of
  the form-field rule, where an explicit `testId` overrides both the field and its `-error`
  id.
- **Normalise colours through a canvas** (see `./contrast`); regex-parsing
  `getComputedStyle` output is unstable under the oklch palette.
- **Pointer position persists across spec files**, and the browser re-evaluates `:hover` when
  new content mounts under it. A spec that measures a colour, or asserts a hover-driven
  component is closed, must **name its pointer state at the assertion**:
  `await userEvent.unhover(el)` before the rest read, `await userEvent.hover(el)` before the
  hover read — not in a `beforeEach`, and never a "park" onto some other element. `unhover`
  moves the pointer to `<body>`, still inside the document, so actionability cannot stall. Two
  consequences: a rest read taken immediately after `unhover` can catch
  `motion-safe:transition-colors` mid-flight, so poll the settled colour; and a pointer parked
  over a fixed REGION (a toast viewport, whose hover pauses auto-dismiss timers) leaks into
  the next file — close that one in the spec that hovered.
- **An early `return` out of a `useAppForm` `onSubmit` RESOLVES the form's mutation**, so
  `onSuccess` fires and the user is navigated as though the write happened. A guard clause
  needs a test asserting the navigation did _not_ happen, not merely that no request went out.
- **TanStack Router JSON-parses search values before `validateSearch`** — `?scope=123`
  arrives as a `number`. Route schemas must accept the parsed type.
- **A screen may not import `WallowError`** (SDK `./server` entry only), so error narrowing in
  app code is structural (`error.status === 400`), never `instanceof`.
- **Assert a feature seam by identity, not presence.** `expect(api.foo).toBe(sdkFoo)`, not
  `toBeDefined()` — a hand-written look-alike carries the same name, shape and type, passes
  every behavioural spec, and reaches an undocumented endpoint.
