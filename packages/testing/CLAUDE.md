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
  `tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`. Consumers' rules live in
  `.claude/rules/TESTING.md`.
