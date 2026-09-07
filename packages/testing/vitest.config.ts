import { defineConfig } from "vitest/config";

import { createVitestProjects } from "./src/vitest-projects";

/**
 * Run this package's logic tests in Node and component tests in Chromium.
 * Import the preset from source so configuration does not depend on a prior build.
 * Pre-bundle the linked query and SDK packages with the router to avoid mid-run reloads.
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

/** Vitest configuration for the package's Node and browser suites. */
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
