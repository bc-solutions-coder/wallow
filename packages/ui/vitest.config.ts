import { createVitestProjects } from "@bc-solutions-coder/testing";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for @bc-solutions-coder/ui. This package's own specs render
 * real React components, so it adopts the shared two-project (node + headless
 * Chromium) split from `@bc-solutions-coder/testing`'s `createVitestProjects`
 * preset — exactly like apps/wallow-auth/vitest.config.ts.
 *
 * There are no pure-logic/SSR `*.test.tsx` specs today, so `nodeTsxSpecs` is
 * empty: every `*.test.ts` (e.g. the on-disk scaffold guard) runs on node and
 * every `*.test.tsx` component spec runs in the browser project.
 */
const { node, browser } = createVitestProjects({});

export default defineConfig({
  test: {
    projects: [node, browser],
  },
});
