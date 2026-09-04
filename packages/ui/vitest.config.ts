import { fileURLToPath } from "node:url";

import { createVitestProjects } from "@bc-solutions-coder/testing";
import { storybookTest } from "@storybook/addon-vitest/vitest-plugin";
import { defineConfig } from "vitest/config";

/**
 * Vitest harness for @bc-solutions-coder/ui. This package's own specs render
 * real React components, so it adopts the shared two-project (node + headless
 * Chromium) split from `@bc-solutions-coder/testing`'s `createVitestProjects`
 * preset — exactly like apps/wallow-auth/vitest.config.ts — and adds a third
 * `storybook` project on top.
 *
 * There are no render-nothing `*.test.tsx` specs today, so the preset's
 * `*.ssr.test.tsx` convention matches nothing: every `*.test.ts` runs on node
 * and every `*.test.tsx` component spec runs in the browser project. This package also takes no `browserPlugins` —
 * the `browser` project deliberately loads no Tailwind (see CLAUDE.md), and the
 * `storybook` project below gets the real pipeline from Storybook itself. Its
 * `browserSetupFiles` therefore carries no styling either: ./vitest.setup.ts
 * exists only to install the navigation-escape guard. `storybookTest()` does not
 * read that option, so the storybook project installs the same guard through
 * .storybook/preview.tsx.
 */

/**
 * Every `@base-ui/react` subpath, for BOTH browser projects'
 * `optimizeDeps.include`. This is not an optimisation — it is required. Left to
 * on-the-fly discovery, Vite pre-bundles a Base UI subpath into a chunk carrying
 * its own copy of React, and the first spec that renders the part dies on
 * `Cannot read properties of null (reading 'useRef')`; in the storybook project
 * it instead triggers a mid-run reload and the story fails to fetch its shim.
 *
 * One glob, not the 39 hand-listed subpaths this used to be. Vite expands it
 * against the package's own `exports` keys (`expandGlobIds` →
 * `resolvePackageData`), so it covers every part Base UI publishes plus the
 * package root — a superset of any list kept by hand, with no "append your
 * subpath here" step for a new component to forget.
 */
const baseUi = ["@base-ui/react/*"];

/**
 * The recipe runtime every component pulls in through its `*.styles.ts` and
 * `src/core/cn.ts`. Same reason as the Base UI subpaths above: discovered on
 * the fly, they land mid-run ("dependencies optimized: class-variance-authority,
 * tailwind-merge" -> reload), which the storybook project cannot survive — its
 * stories then fail to fetch `@storybook_react-dom-shim.js`. Two real package
 * names rather than a glob: neither publishes subpaths worth pre-bundling.
 */
const recipeRuntime = ["class-variance-authority", "tailwind-merge"];

/**
 * The toast runtime `failure-toast` renders through. Same mid-run-discovery
 * hazard as the recipe runtime, and sonner ships React-bound code, so left to
 * discovery it would also be the duplicate-React case Base UI is listed for.
 */
const toastRuntime = ["sonner"];

const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: [...baseUi, ...recipeRuntime, ...toastRuntime],
  browserSetupFiles: ["./vitest.setup.ts"],
});

/**
 * The `storybook` project: `storybookTest` reads ./.storybook/main.ts, expands
 * its story glob and hands every story to Vitest as a test case rendered in a
 * browser. It is hand-assembled here rather than folded into
 * `createVitestProjects` because Storybook is a packages/ui concern — the shared
 * preset stays the two-project node/browser contract every app uses.
 *
 * The provider is the very descriptor the `browser` project runs on (it mints a
 * fresh provider per project), so stories and component specs execute in the
 * same headless Chromium with no second `@vitest/browser-playwright` copy to
 * keep in step.
 */
const storybook = {
  plugins: [storybookTest({ configDir: fileURLToPath(new URL(".storybook", import.meta.url)) })],
  // Storybook runs its own Vite server with its own dep cache, so the Base UI
  // and recipe-runtime pre-bundle lists have to be repeated here — sharing the
  // constants, not the whole browser-project list (this project renders through
  // Storybook's runtime, not `vitest-browser-react`).
  optimizeDeps: { include: [...baseUi, ...recipeRuntime, ...toastRuntime] },
  test: {
    name: "storybook",
    browser: {
      enabled: true,
      provider: browser.test.browser.provider,
      headless: true,
      instances: [{ browser: "chromium" as const }],
    },
  },
};

export default defineConfig({
  test: {
    projects: [node, browser, storybook],
  },
});
