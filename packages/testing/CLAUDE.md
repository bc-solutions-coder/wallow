# packages/testing — @bc-solutions-coder/testing Agent Guide

The shared **Vitest preset** and browser-mode test utilities. Every package with component
specs (all three apps plus `packages/ui`) gets its two-project node/browser split from here
rather than hand-rolling one.

## Subpath-per-entry — the split is load-bearing

Every helper gets its OWN entry rather than riding the barrel, because the barrel is loaded in a
plain Node process at Vitest config time. One browser-only import on it breaks every config in the
workspace.

| Entry                                                                      | Imported at                         | What it is                                                                                                                                                                                  |
| -------------------------------------------------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`)                                                       | Vitest **config load** (plain Node) | `createVitestProjects()` → the `{ node, browser }` project pair for `defineConfig({ test: { projects } })`, plus `browserOptimizeDepsBaseline` / `mergeOptimizeDeps`.                       |
| `./render` (`src/render.tsx`)                                              | Inside a **browser-mode spec**      | `render`, re-exported from `vitest-browser-react` — the single seam where shared providers/wrappers would be added.                                                                         |
| `./render-with-wallow` (`src/render-with-wallow.tsx`)                      | Inside a **browser-mode spec**      | `render` wrapped in the router + query providers a screen needs.                                                                                                                            |
| `./contrast` (`src/contrast.ts`)                                           | Inside a **browser-mode spec**      | Measured-colour helpers: `parseColor` / `computedColor` / `effectiveBackground` / `contrastRatio` / `textContrast`. Reads what a component PAINTS, which a class-string assertion cannot.   |
| `./locators` (`src/locators.ts`)                                           | Inside a **browser-mode spec**      | `byTestId` and friends — the one way a spec reaches an element.                                                                                                                             |
| `./catalog-select` (`src/catalog-select.ts`)                               | Inside a **browser-mode spec**      | `chooseOption` / `expectCatalogSelect`. A catalog `Select` portals `role="option"` divs to `<body>` only while open, so `userEvent.selectOptions` cannot drive it.                          |
| `./theme-wiring` (`src/theme-wiring.tsx`)                                  | Inside a **browser-mode spec**      | `assertThemeWiring({ tokens, probeClass })` — the consumer's whole spec file is one call.                                                                                                   |
| `./sdk-harness` (`src/sdk-harness.ts`)                                     | **Any** project                     | `createSdkHarness` / `createPassthroughHarness`, plus the multi-route helpers (`routeHarness`, `failsWith`, `neverSettles`) re-exported so a spec needs one specifier. Imports no `vitest`. |
| `./invalidation` (`src/invalidation.ts`)                                   | Inside a **spec**                   | Runs a real `invalidations` predicate against a real generated query key.                                                                                                                   |
| `./browser-deps` (`src/browser-deps.ts`)                                   | Inside a **node-project spec**      | `describeBrowserPreBundleList()` — asserts every `optimizeDeps.include` entry in a consumer's browser project actually resolves. Spawns child processes; keep it off the barrel.            |
| `./browser-styles-wiring` (`src/browser-styles-wiring.ts`)                 | Inside a **node-project spec**      | `assertBrowserStylesWiring({ appDir, extraSpecs })` — reads the consumer's config/setup/stylesheet off disk. Node-only.                                                                     |
| `./node-async-hooks-browser-shim` (`src/node-async-hooks-browser-shim.ts`) | A browser-project `resolve.alias`   | Real in-browser `AsyncLocalStorage` answering "no scope", for apps whose router pulls `node:async_hooks`.                                                                                   |

- **The two wiring guards are a PAIR, and each fails differently.** `./theme-wiring` measures what
  the browser actually paints — that is the assertion that matters. `./browser-styles-wiring` names
  the pieces on disk, so a removed one fails saying WHICH rather than as a pile of 15s actionability
  timeouts (no utilities) or vacuously-passing transparent colours (no theme). A consumer's spec
  files are one call each; the app supplies only what it alone can answer — its `appDir`, its theme
  tokens, its probe class, and (wallow-auth only) the checkbox specs that must not regrow a
  focus+Space workaround.

- **Keep `render` off the barrel.** `vitest-browser-react` evaluates `vitest/browser` at import
  and throws outside browser mode; the barrel is loaded in a plain Node process at config time,
  so importing it there breaks every config in the workspace.
- **`./contrast` parses colours through a canvas, not a regex.** `packages/styles/branding.json`'s palette is
  `oklch(...)` and Chromium preserves the authored colour space in a computed value, so an
  `rgb()` matcher silently fails on the exact tokens this repo uses. Painting the string and
  reading the pixel back normalises any CSS colour syntax to sRGB. It is browser-only for the
  same reason as `./render` — keep it off the barrel.
- **The preset styles nothing.** A consumer that needs real CSS passes `wallowStyles()` as
  `browserPlugins` and its setup file as `browserSetupFiles`, alongside a root-level
  `vitest-styles.css` the setup file imports next to `virtual:wallow-theme.css` (see
  `apps/wallow-web`). Both options are pure pass-throughs onto the browser project — the preset
  calls neither, which is what keeps `@bc-solutions-coder/styles` out of this package's
  dependencies. The Tailwind entry cannot be hoisted here either (Tailwind v4 resolves `@source`
  relative to the declaring stylesheet), but the THEME half is shared, served by `wallowStyles()`
  from `@bc-solutions-coder/styles`.
- **A render-nothing `*.test.tsx` spec is named `*.ssr.test.tsx`** — the preset's `ssrSpecGlob`,
  and `nodeTsxSpecs`' default. A spec qualifies when it renders through `react-dom/server` or
  asserts a `beforeLoad` redirect, i.e. never mounts a DOM. The convention replaced a hand-listed
  inventory in each app's config, where a new SSR spec silently ran in Chromium until someone
  remembered to append it. Passing `nodeTsxSpecs` explicitly REPLACES the convention rather than
  extending it.
- The browser project uses the Vitest 4 **factory** provider `playwright()`, not the v3
  `"playwright"` string (which throws). Chromium only, headless.
- App-local knobs (`resolve.alias`, `server.deps.inline`) belong in the app's config and are
  passed through `nodeProjectOverrides` / `nodeTsxSpecs` / `extraBrowserOptimizeDeps` /
  `browserPlugins` / `browserSetupFiles` — do not hardcode app specifics in this package. What a
  project shares with the ROOT config (`resolve.tsconfigPaths`, `ssr.noExternal`) is spelled once
  at the root and pulled in per project with `extends: true`; a project-level `resolve` MERGES
  into the inherited one rather than replacing it, so a project adding an alias keeps the root's
  `tsconfigPaths`.
- **An unresolvable `optimizeDeps.include` entry is a WARNING Vite ignores**, so a list can look
  complete while pre-bundling nothing — and the dropped entry never reaches the dep-cache hash,
  which turns the resulting duplicate-React failure intermittent. `./browser-deps` is the guard;
  every consumer with a browser project calls it from a one-import `src/**/browser-deps.test.ts`.
  Under pnpm the cause is almost always non-declaration, so the fix is a `package.json` line.
  Base UI is named by the glob `@base-ui/react/*`, which Vite expands against that package's own
  `exports` keys — do not go back to listing subpaths by hand.
- Scripts: `pnpm --filter @bc-solutions-coder/testing build` (Vite lib mode +
  `tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`.

## How a consumer's suite runs

- **Vitest runs a two-project split** (see each app's `vitest.config.ts`): a **node** project for
  pure-logic specs (`src/**/*.test.ts` plus the rare render-nothing `*.test.tsx`) and a **browser**
  project for every component spec (`src/**/*.test.tsx`, headless Chromium). `pnpm test`
  (= `pnpm -r test` = `vitest run` per package) drives both. Component assertions come from
  `@vitest/expect` locator matchers, not `@testing-library/jest-dom`.
- **`packages/ui` runs a THIRD vitest project, `storybook`.** `@storybook/addon-vitest` executes every
  `src/components/<name>/<name>.stories.tsx` as a test case in the same headless Chromium — with the real
  Tailwind pipeline and fork theme attached, which the plain `browser` project does not load. Stories are
  therefore the component library's render/interaction coverage; a co-located `*.test.tsx` there covers only
  behavioural edges a story cannot express (data-attribute state, className-override-wins, keyboard
  interaction). Do not duplicate story coverage into a test file. See `packages/ui/CLAUDE.md`.
- **Running a subset by hand needs `--configLoader runner`.** The apps' own `test` scripts pass it; a bare
  `pnpm --filter ./apps/<app> exec vitest run <paths>` does not, and without it vitest cannot resolve
  `packages/styles/src/assets` and dies with `ERR_MODULE_NOT_FOUND` before running a single test — which
  looks like a broken spec but is the missing flag.

## Never mock `@bc-solutions-coder/ui`

The component library's own tests mock nothing (real Base UI parts, real Chromium, real design tokens;
the only permitted double is a userland callback spy such as `onClick={fn()}`), and apps must not stub
it out either — no `vi.mock("@bc-solutions-coder/ui")` replacing components with hand-rolled
placeholders. A spec that finds a real component awkward to drive is telling you the component or the
spec is wrong, not that it needs a stub.

The one legitimate exception in the tree is the apps' `__root*.test.tsx` SSR-isolation specs, which use
`vi.mock(..., importOriginal)` to spread the real module and override *only*
`FocusOnNavigate`/`DocumentStyles` as render-nothing sentinels (`renderToString` has no router context)
— a partial override for SSR isolation, not a replacement.

## Test comments

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
"this uses the catalog now" — delete it. So is any `it()` whose body only reads `element.classList`:
a component can render the right classes and be broken, or restyle correctly with different classes
and fail. Assert the computed value, never the class string — `cn()` merges a caller's `className`
over the recipe. Where one of the `wallow/*` lint rules already says it, the spec is redundant by
construction.

## Browser-mode facts that bite

Each of these cost a debugging session and is invisible from the code:

- **`location` is `[Unforgeable]` in real Chromium.** `vi.stubGlobal("location", …)` cannot shadow it,
  and a screen assigning `globalThis.location.href` navigates the iframe and tears the runner down.
  Observe a full-page hand-off at the Navigation API `navigate` event: read `event.destination.url`
  and `preventDefault()` so the runner stays put.
- **Fill a field with `userEvent.fill`, not `userEvent.type`.** `type` costs one CDP round trip PER
  CHARACTER, and under a full `pnpm test` — where every package's browser project drives its own
  Chromium at once — it is the round-trip COUNT that amplifies. A form helper typing 80 characters to
  set up one assertion failed 2 of 6 gate runs against the 15s browser `testTimeout` (worst 20647ms);
  the same specs on `fill` (80 round trips down to 5) stopped failing. Unloaded the difference is a
  forgettable ~24%, so this only ever shows up as an intermittent CI timeout. Reach for `type` only
  when the spec needs keyboard syntax (`{Shift}`, `{Backspace}`) or appends to an existing value —
  `fill` REPLACES, so converting a two-call sequence that builds one string silently changes it.
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
