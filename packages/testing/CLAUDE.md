# packages/testing — @bc-solutions-coder/testing Agent Guide

The shared **Vitest preset** and browser-mode test utilities. Every package with component
specs (all three apps plus `packages/ui`) gets its two-project node/browser split from here
rather than hand-rolling one.

## Three entries — the split is load-bearing

| Entry                            | Imported at                         | What it is                                                                                                                                                                                |
| -------------------------------- | ----------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`)             | Vitest **config load** (plain Node) | `createVitestProjects()` → the `{ node, browser }` project pair for `defineConfig({ test: { projects } })`, plus `browserOptimizeDepsBaseline` / `mergeOptimizeDeps`.                     |
| `./render` (`src/render.tsx`)    | Inside a **browser-mode spec**      | `render`, re-exported from `vitest-browser-react` — the single seam where shared providers/wrappers would be added.                                                                       |
| `./contrast` (`src/contrast.ts`) | Inside a **browser-mode spec**      | Measured-colour helpers: `parseColor` / `computedColor` / `effectiveBackground` / `contrastRatio` / `textContrast`. Reads what a component PAINTS, which a class-string assertion cannot. |

- **Keep `render` off the barrel.** `vitest-browser-react` evaluates `vitest/browser` at import
  and throws outside browser mode; the barrel is loaded in a plain Node process at config time,
  so importing it there breaks every config in the workspace.
- **`./contrast` parses colours through a canvas, not a regex.** `api/branding.json`'s palette is
  `oklch(...)` and Chromium preserves the authored colour space in a computed value, so an
  `rgb()` matcher silently fails on the exact tokens this repo uses. Painting the string and
  reading the pixel back normalises any CSS colour syntax to sRGB. It is browser-only for the
  same reason as `./render` — keep it off the barrel.
- **The preset styles nothing.** A consumer that needs real CSS adds `wallowStyles()` plus a
  root-level `vitest-styles.css` and a setup file importing it and `virtual:wallow-theme.css`
  (see `apps/wallow-web`). The Tailwind entry cannot be hoisted here — Tailwind v4 resolves
  `@source` relative to the declaring stylesheet — but the THEME half is shared, served by
  `wallowStyles()` from `@bc-solutions-coder/styles`.
- The browser project uses the Vitest 4 **factory** provider `playwright()`, not the v3
  `"playwright"` string (which throws). Chromium only, headless.
- App-local knobs (`resolve.alias`, `server.deps.inline`) belong in the app's config and are
  passed through `nodeProjectOverrides` / `nodeTsxSpecs` / `extraBrowserOptimizeDeps` — do not
  hardcode app specifics in this package.
- Scripts: `pnpm --filter @bc-solutions-coder/testing build` (Vite lib mode +
  `tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`. Consumers' rules live in
  `.claude/rules/TESTING.md`.
