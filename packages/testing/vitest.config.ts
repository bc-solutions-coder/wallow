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
 *
 * `@bc-solutions-coder/query` joined the list with Wallow-x4qn.4, when
 * `render-with-wallow.tsx` stopped importing react-query directly and started
 * taking `QueryClient`/`QueryClientProvider` from the facade. It has to be named
 * explicitly because a LINKED workspace package is not pre-bundled by default;
 * with it listed, the pre-bundled `@bc-solutions-coder_query.js` chunk carries
 * react-query's runtime inlined, so there is exactly ONE react-query copy (and
 * therefore one `QueryClientProvider` context) in the browser graph.
 *
 * `@tanstack/react-query` stays on the list as policy — it is still the module
 * actually being pre-bundled, one facade hop away, and this package must not read
 * as if the facade replaced it. It no longer resolves from THIS package's root
 * (the manifest dropped it), so Vite logs one "Failed to resolve dependency"
 * line per run; the pre-bundling it names is done by the facade entry above.
 */
const { node, browser } = createVitestProjects({
  browserSetupFiles: ["./vitest.setup.ts"],
  extraBrowserOptimizeDeps: [
    "@bc-solutions-coder/query",
    "@bc-solutions-coder/sdk",
    "@tanstack/react-query",
    "@tanstack/react-router",
  ],
});

export default defineConfig({
  // The query facade is a LINKED workspace package, which Vite neither
  // pre-bundles nor inlines by default, so it is named on both sides
  // explicitly: `optimizeDeps.include` above keeps the browser project from
  // discovering it (and its react-query re-export) mid-run and reloading, and
  // `ssr.noExternal` keeps the node project transforming its source instead of
  // externalizing it to a bare Node import.
  ssr: {
    noExternal: ["@bc-solutions-coder/query"],
  },
  test: {
    projects: [node, browser],
  },
});
