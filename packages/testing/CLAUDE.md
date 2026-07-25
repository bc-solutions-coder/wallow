# packages/testing — @bc-solutions-coder/testing Agent Guide

The shared **Vitest preset** and browser-mode test utilities. Every package with component
specs (all three apps plus `packages/ui`) gets its two-project node/browser split from here
rather than hand-rolling one.

## Two entries — the split is load-bearing

| Entry                         | Imported at                         | What it is                                                                                                                                                            |
| ----------------------------- | ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`)          | Vitest **config load** (plain Node) | `createVitestProjects()` → the `{ node, browser }` project pair for `defineConfig({ test: { projects } })`, plus `browserOptimizeDepsBaseline` / `mergeOptimizeDeps`. |
| `./render` (`src/render.tsx`) | Inside a **browser-mode spec**      | `render`, re-exported from `vitest-browser-react` — the single seam where shared providers/wrappers would be added.                                                   |

- **Keep `render` off the barrel.** `vitest-browser-react` evaluates `vitest/browser` at import
  and throws outside browser mode; the barrel is loaded in a plain Node process at config time,
  so importing it there breaks every config in the workspace.
- The browser project uses the Vitest 4 **factory** provider `playwright()`, not the v3
  `"playwright"` string (which throws). Chromium only, headless.
- App-local knobs (`resolve.alias`, `server.deps.inline`) belong in the app's config and are
  passed through `nodeProjectOverrides` / `nodeTsxSpecs` / `extraBrowserOptimizeDeps` — do not
  hardcode app specifics in this package.
- Scripts: `pnpm --filter @bc-solutions-coder/testing build` (Vite lib mode +
  `tsc -p tsconfig.build.json`), `test`, `test:watch`, `typecheck`. Consumers' rules live in
  `.claude/rules/TESTING.md`.
