import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-auth — the shared two-project (node + headless
 * Chromium) split now lives in `@bc-solutions-coder/testing`'s
 * `createVitestProjects` preset (Wallow-0q2s.1.3). This config only supplies the
 * app-specific knobs.
 *
 * The one pure-logic `*.test.tsx` (`src/routes/index.test.tsx`) asserts a route's
 * `beforeLoad` redirect and renders no DOM, so it runs on the node project rather
 * than in the browser. wallow-auth needs no extra browser `optimizeDeps` beyond
 * the preset baseline and no node-project overrides.
 */
const nodeTsxSpecs = ["src/routes/index.test.tsx"];

const { node, browser } = createVitestProjects({ nodeTsxSpecs });

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
