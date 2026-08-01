import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for @bc-solutions-coder/navigation. The shell's specs render
 * real @bc-solutions-coder/ui components at real viewports and measure the
 * colours they paint, so this adopts the shared node + headless-Chromium split
 * from `createVitestProjects` — the same shape packages/forms and both apps use
 * — plus `wallowStyles()` and ./vitest.setup.ts for the CSS those measurements
 * need.
 *
 * `react-dom/server` is pre-bundled because `app-shell.ssr-flash.test.tsx`
 * renders the shell the way an SSR host does — through `react-dom/server` —
 * INSIDE Chromium, so the pre-hydration paint is measured against real CSS at a
 * real viewport. Its name deliberately does NOT end `.ssr.test.tsx`, which would
 * route it onto the node project and away from the viewport it measures.
 */

/**
 * Every `@base-ui/react` subpath reachable from the ui components the shell
 * composes, for the browser project's `optimizeDeps.include`. This is not an
 * optimisation — it is required. Left to on-the-fly discovery, Vite pre-bundles
 * a Base UI subpath into a chunk carrying its own copy of React and the first
 * spec that renders the part dies on `Cannot read properties of null (reading
 * 'useRef')` (see packages/ui/CLAUDE.md).
 *
 * One glob, which Vite expands against Base UI's own `exports` keys.
 */
const baseUi = ["@base-ui/react/*"];

/**
 * This package's own runtimes plus the recipe runtime every ui component pulls
 * in through its `*.styles.ts`. Same failure mode as the Base UI subpaths: left
 * undiscovered they land mid-run ("dependencies optimized: ..." -> reload),
 * which drops the test runner.
 */
const shellRuntime = [
  "react",
  "react/jsx-dev-runtime",
  "react/jsx-runtime",
  "react-dom",
  "react-dom/server",
  "@tanstack/react-router",
  "zustand",
  "lucide-react",
  "class-variance-authority",
  "tailwind-merge",
];

const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: [...baseUi, ...shellRuntime],
  browserPlugins: wallowStyles(),
  browserSetupFiles: ["./vitest.setup.ts"],
});

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
