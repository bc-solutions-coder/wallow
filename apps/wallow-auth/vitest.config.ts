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
 * wallow-auth needs no node-project overrides.
 */
const nodeTsxSpecs = ["src/routes/index.test.tsx"];

/*
 * Runtimes wallow-auth reaches beyond the preset baseline, pre-bundled so the
 * browser provider does not discover them mid-run and trigger a Vite reload
 * ("Vitest unexpectedly reloaded a test"), which flakes the whole file.
 *
 * `zod` arrives with @bc-solutions-coder/forms: a migrated screen imports it for
 * its schema, and it is the schema module — not the form package — that the
 * scanner misses on the first pass. Every form this app migrates needs it.
 */
const extraBrowserOptimizeDeps: string[] = ["zod"];

const { node, browser } = createVitestProjects({ nodeTsxSpecs, extraBrowserOptimizeDeps });

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
