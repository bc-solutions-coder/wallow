import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness — the shared two-project (node + headless Chromium) split lives
 * in `@bc-solutions-coder/testing`'s `createVitestProjects` preset. This config
 * only supplies the app-specific knobs.
 *
 * This app has no pure-logic `*.test.tsx` specs (every `*.test.tsx` mounts a live
 * DOM), so `nodeTsxSpecs` is empty and everything defaults from the preset.
 */
const { node, browser } = createVitestProjects({ nodeTsxSpecs: [] });

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
