import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-auth — the shared two-project (node + headless
 * Chromium) split now lives in `@bc-solutions-coder/testing`'s
 * `createVitestProjects` preset (Wallow-0q2s.1.3). This config only supplies the
 * app-specific knobs.
 *
 * The pure-logic/SSR `*.test.tsx` specs listed below render through
 * `react-dom/server` (or assert a route's `beforeLoad` redirect) and never mount
 * a live DOM, so they run on the node project rather than in Chromium — routing
 * them into a browser buys nothing and costs real per-test overhead. Every OTHER
 * `*.test.tsx` mounts a component and belongs in the browser project.
 * wallow-auth needs no extra browser `optimizeDeps` beyond the preset baseline
 * and no node-project overrides.
 */
const nodeTsxSpecs = ["src/routes/index.test.tsx"];

const { node, browser } = createVitestProjects({ nodeTsxSpecs });

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
