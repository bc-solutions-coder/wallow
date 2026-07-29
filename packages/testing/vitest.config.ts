import { defineConfig } from "vitest/config";

import { createVitestProjects } from "./src/vitest-projects";

/**
 * This package dogfoods the two-project split it exports (Wallow-pu6a.5.1).
 *
 * It used to be a single `environment: "node"` config, which was honest while
 * everything here was config helpers and thin re-exports. `renderWithWallow`
 * changed that: it mounts a real component tree, so its spec must run in the
 * same headless Chromium every other `*.test.tsx` in the repo runs in
 * (`.claude/rules/TESTING.md` — never jsdom). The node project still carries the
 * pure-logic specs (`src/**\/*.test.ts`).
 *
 * The preset is imported from `./src`, not from this package's own dist: a build
 * has to be able to run AFTER a green test run, not before it.
 *
 * `@bc-solutions-coder/sdk` and the two `@tanstack` packages join the browser
 * `optimizeDeps` baseline because `render-with-wallow.tsx` imports them; leaving
 * them out lets Vite discover them mid-run and reload, which drops the test
 * runner ("Vitest failed to find the runner").
 */
const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: [
    "@bc-solutions-coder/sdk",
    "@tanstack/react-query",
    "@tanstack/react-router",
  ],
});

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
