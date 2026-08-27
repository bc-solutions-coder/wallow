import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for @bc-solutions-coder/forms. This package's specs render real
 * @bc-solutions-coder/ui components driven by TanStack Form state, so it adopts
 * the shared node + headless-Chromium split from `createVitestProjects` — the
 * same shape apps/wallow-auth and packages/ui use.
 *
 * There are no render-nothing `*.test.tsx` specs here, so the preset's
 * `*.ssr.test.tsx` convention matches nothing: every `*.test.ts` runs on node and
 * every `*.test.tsx` catalog spec runs in the browser project.
 *
 * `wallowStyles()` + ./vitest.setup.ts are not cosmetic. A ui control gets its
 * BOX from a Tailwind utility in its recipe, so with no stylesheet
 * `Checkbox.Root`'s `<span role="checkbox">` measures 0x0 and a spec that clicks
 * it hangs until Playwright's actionability timeout. Every catalog spec drives
 * real controls, so they all need real CSS.
 */

/**
 * Every `@base-ui/react` subpath reachable from the ui components this package's
 * fields wrap, for the browser project's `optimizeDeps.include`. This is not an
 * optimisation — it is required. Left to on-the-fly discovery, Vite pre-bundles a
 * Base UI subpath into a chunk carrying its own copy of React and the first spec
 * that renders the part dies on `Cannot read properties of null (reading
 * 'useRef')` (see packages/ui/CLAUDE.md).
 *
 * One glob, which Vite expands against Base UI's own `exports` keys — the same
 * line packages/ui/vitest.config.ts carries. It replaces the five subpaths kept
 * by hand here, and with them the rule that every new catalog field had to append
 * the subpaths of the ui component it wraps.
 */
const baseUi = ["@base-ui/react/*"];

/**
 * This package's own runtimes plus the recipe runtime every ui component pulls
 * in through its `*.styles.ts`. Same failure mode as the Base UI subpaths: left
 * undiscovered they land mid-run ("dependencies optimized: ..." -> reload),
 * which drops the test runner.
 *
 * TanStack Query appears here under its facade name, `@bc-solutions-coder/query`,
 * and never under the react-query specifier the facade re-exports. An entry Vite
 * cannot resolve is only a warning, after which it pre-bundles nothing (see
 * `src/core/browser-deps.test.ts`), and under pnpm's strict `node_modules` this
 * package resolves only what it declares — which is the facade. The facade
 * carries the same module through the one package that does declare react-query.
 */
const formRuntime = [
  "@tanstack/react-form",
  "@bc-solutions-coder/query",
  "zod",
  "class-variance-authority",
  "tailwind-merge",
];

const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: [...baseUi, ...formRuntime],
  browserPlugins: wallowStyles(),
  browserSetupFiles: ["./vitest.setup.ts"],
});

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
