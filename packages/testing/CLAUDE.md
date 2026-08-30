# packages/testing — @bc-solutions-coder/testing Agent Guide

The shared **Vitest preset** (`createVitestProjects()`) and browser-mode test utilities.
(`packages/logger` deliberately spells its own project pair out, with a comment saying why.)

## Subpath-per-entry — the split is load-bearing

Every helper gets its OWN entry rather than riding the barrel, because the barrel is loaded
in a plain Node process at Vitest config time — one browser-only import on it breaks every
config in the workspace. Where an entry may be imported is the contract:

- **Config-time / node-only**: `.` (`createVitestProjects`, `mergeOptimizeDeps`),
  `./browser-deps` (node-project specs; spawns child processes), `./browser-styles-wiring`
  (node-project specs; reads config/setup/stylesheet off disk).
- **Browser-mode specs only**: `./render` and `./render-with-wallow` (`vitest-browser-react`
  evaluates `vitest/browser` at import and throws outside browser mode — keep it off the
  barrel), `./contrast`, `./locators` (the one way a spec reaches an element),
  `./catalog-select`, `./theme-wiring`, `./browser-mode-smoke`, `./form-submission` (captures and
  cancels a native form POST so a spec can read the body a full-page submit would carry).
- **A browser project's SETUP file** — the guard trio: `./navigation-escape`,
  `./console-guard` (wraps, never replaces, `console.error`/`warn` so React noise fails the
  test that produced it), `./network-escape` (unowned `fetch` answered with a 503 naming the
  request).
- **Any project**: `./sdk-harness` (imports no `vitest`), `./invalidation`, `./router-stub`
  (`beforeEach` guard for `vi.mock("@tanstack/react-router")` specs).
- **A browser project's `resolve.alias`**: `./node-async-hooks-browser-shim`.

## Preset facts

- **The two wiring guards are a PAIR.** `./theme-wiring` measures what the browser paints —
  the assertion that matters. `./browser-styles-wiring` names the pieces on disk, so a
  removed one fails saying WHICH, not as actionability timeouts or vacuous colours.
- **`./contrast` parses colours through a canvas, not a regex** — the fork palette is
  `oklch(...)`, Chromium preserves the authored colour space in computed values (an `rgb()`
  matcher silently fails), and painting the string then reading the pixel back normalises
  any syntax to sRGB.
- **The preset styles nothing.** A consumer needing real CSS passes `wallowStyles()` as
  `browserPlugins` and its setup file as `browserSetupFiles` — pure pass-throughs, keeping
  `styles` out of this package's deps. The Tailwind entry cannot be hoisted here — see
  `packages/styles/CLAUDE.md` on `@source`.
- **A render-nothing `*.test.tsx` spec is named `*.ssr.test.tsx`** — the preset's
  `ssrSpecGlob`: it renders through `react-dom/server` or asserts a `beforeLoad` redirect,
  never mounting a DOM. Passing `nodeTsxSpecs` explicitly REPLACES the convention rather
  than extending it.
- The browser project uses the Vitest 4 **factory** provider `playwright()`, not the v3
  `"playwright"` string (which throws).
- **An unresolvable `optimizeDeps.include` entry is a WARNING Vite ignores** — the list
  looks complete while pre-bundling nothing, and the dropped entry never reaches the
  dep-cache hash, turning the duplicate-React failure intermittent. `./browser-deps` is the
  guard; every consumer with a browser project calls it. Under pnpm the fix is almost always
  a `package.json` declaration. Base UI is named by the glob `@base-ui/react/*` (expanded
  against its `exports` keys) — do not list subpaths by hand.
- **An app's wiring guards live DIRECTLY under its `src/`** — each imports `../vite.config`
  or `../vitest.config`, which `wallow/zone-dag` tolerates only for single-segment `src/*`
  paths (`ROOT_ZONE`). The zoned apps spell this as exactly two files:
  `src/app-wiring.test.ts` and `src/app-wiring.browser.test.tsx` — two because the preset
  routes projects by EXTENSION; folded into the node file, the browser guards would evaluate
  the whole feature graph under `environment: "node"`.
- **Running a subset by hand needs `--configLoader runner`** (the apps' `test` scripts pass
  it). Without it vitest cannot resolve `packages/styles/src/assets` and dies with
  `ERR_MODULE_NOT_FOUND` before running a single test — looks like a broken spec, is the
  missing flag.
- **`packages/ui` runs a THIRD project, `storybook`** — see `packages/ui/CLAUDE.md`.

## Never mock `@bc-solutions-coder/ui`

The component library's own tests mock nothing (real Base UI parts, real Chromium, real
tokens; the only permitted double is a userland callback spy), and apps must not stub it out
either. A spec that finds a real component awkward to drive is telling you the component or
the spec is wrong. The one legitimate exception is the apps' `__root*.test.tsx`
SSR-isolation specs, which use `vi.mock(..., importOriginal)` to spread the real module and
override _only_ `FocusOnNavigate`/`DocumentStyles` as render-nothing sentinels
(`renderToString` has no router context) — a partial override, not a replacement.

## Test comments

A spec's header states **what the file covers**, plus any non-obvious reason a reader would
otherwise "fix" the test wrongly. **Maximum 8 lines. Present tense.** A statement earns its
place only if it is all three of: true now, non-obvious from the code, and able to prevent a
wrong edit. Inline comments only where a single assertion is genuinely surprising. Stale
prose harms the next reader — a comment naming a constraint that no longer exists makes them
route around a problem the code does not have.

Delete on sight, in any spec you touch:

| Category                        | Examples                                                                        |
| ------------------------------- | ------------------------------------------------------------------------------- |
| Issue IDs / plan refs           | `#97`, `(2.8a)`, `scout inventory on ...`                                       |
| History verbs                   | `used to`, `no longer`, `replaces`, `was previously`, `this file used to carry` |
| Line citations into other files | `AccountController.cs:65-165`, `packages/sdk/src/auth-oidc.ts:42`               |
| Scope disclaimers               | `deliberately says NOTHING about`, `out of scope for this spec`                 |
| Restatement                     | any sentence paraphrasing the `it()` below it                                   |
| Decorative rules                | `── SECTION ─────`, ASCII banners, box drawing                                  |

**A spec asserting that a migration happened is finished work, not coverage.** So is any
`it()` that only reads `element.classList` — assert the computed value, never the class
string; `cn()` merges a caller's `className` over the recipe.

## Browser-mode facts that bite

- **`location` is `[Unforgeable]` in real Chromium.** `vi.stubGlobal("location", …)` cannot
  shadow it, and a screen assigning `globalThis.location.href` navigates the iframe and
  tears the runner down. `./navigation-escape` vetoes the hand-off at the Navigation API
  `navigate` event; a project installs it in its browser SETUP file — never per spec,
  because the file that leaks is not the file the runner blames. **Every browser project
  installs the guard TRIO** through its setup door (`packages/ui`'s `storybook` project takes
  a different door — see its CLAUDE.md); `browserSetupFiles` defaults to `[]`, so wire a
  setup file into a NEW browser project on day one. A spec asserting a DELIBERATE hand-off
  (wallow-auth's OIDC screens) consumes the record — `expectNavigationEscape()` awaits
  exactly one; `consumeNavigationEscapes()` drains all — an unconsumed escape still fails
  the test that leaked it.
- **Fill a field with `userEvent.fill`, not `userEvent.type`.** `type` costs one CDP round
  trip PER CHARACTER, amplifying into intermittent CI timeouts under a full `pnpm test`.
  Reach for `type` only for keyboard syntax or appending — `fill` REPLACES.
- **`createSdkHarness()` records a call BEFORE its responder runs.** The earliest non-racy
  point at which "the request is in flight" is a fact is
  `await vi.waitFor(() => expect(harness.calls).toHaveLength(n))` — not the click, and not
  the settle helper. Every pending/disabled-state assertion depends on this.
- **Ask the generated factory for a query key; never spell one as a literal.** The key
  carries the client's `baseUrl`, so a drifted literal makes `getQueryData` return
  `undefined` rather than fail — a no-op assertion every implementation passes. Assert an
  invalidation by _behaviour_: run the real predicate against the real `{op}QueryKey()`.
- **`getByText` matches by SUBSTRING.** Exact matching needs `{ exact: true }` —
  overlapping fixture names ("Globex" also matches "globex.io") pass for the wrong element.
- **A browser project with no stylesheet fails in two misleading ways.** No Tailwind: a
  catalog control measures 0×0 and every click hangs to Playwright's actionability timeout.
  No fork theme: every colour token is a VALUELESS custom property, so `bg-card` paints
  `rgba(0, 0, 0, 0)` and colour assertions pass vacuously.
- **Catalog controls are not native elements.** `Select` renders `role="option"` divs
  portalled to `<body>` only while open (drive with `chooseOption`;
  `userEvent.selectOptions` only drives an `HTMLSelectElement`). `Checkbox` renders a
  `<span role="checkbox">` beside a hidden input — assert `role`/`aria-checked`, never
  `type="checkbox"`. `ListRow` derives its testid as `{name}-item` and that derivation
  **cannot** be overridden — the exact inverse of the form-field rule, where an explicit
  `testId` overrides both the field and its `-error` id.
- **Pointer position persists across spec files**, and the browser re-evaluates `:hover`
  when new content mounts under it. A spec measuring a colour or asserting a hover-driven
  component closed must **name its pointer state at the assertion**:
  `await userEvent.unhover(el)` before the rest read, `hover` before the hover read — not in
  a `beforeEach`, never a "park" onto some other element (`unhover` moves the pointer to
  `<body>`, still inside the document, so actionability cannot stall). A rest read right
  after `unhover` can catch `motion-safe:transition-colors` mid-flight — poll the settled
  colour. A pointer parked over a fixed REGION (a toast viewport, whose hover pauses
  auto-dismiss timers) leaks into the next file — close it in the spec that hovered.
- **An early `return` out of a `useAppForm` `onSubmit` RESOLVES the form's mutation**, so
  `onSuccess` fires and the user is navigated as though the write happened. A guard clause
  needs a test asserting the navigation did _not_ happen, not merely that no request went out.
- **TanStack Router JSON-parses search values before `validateSearch`** — `?scope=123`
  arrives as a `number`. Route schemas must accept the parsed type.
- **A screen may not import `WallowError`** (SDK `./server` entry only), so error narrowing
  in app code is structural (`error.status === 400`), never `instanceof`.
- **Assert a feature seam by identity, not presence.** `expect(api.foo).toBe(sdkFoo)`, not
  `toBeDefined()` — a hand-written look-alike carries the same name, shape and type, passes
  every behavioural spec, and reaches an undocumented endpoint.
