import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness — the shared two-project (node + headless Chromium) split lives
 * in `@bc-solutions-coder/testing`'s `createVitestProjects` preset. This config
 * only supplies the app-specific knobs.
 *
 * This app has no render-nothing `*.test.tsx` specs (every `*.test.tsx` mounts a
 * live DOM), so the preset's `*.ssr.test.tsx` convention matches nothing and
 * every option defaults.
 */
const { node, browser } = createVitestProjects();

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
