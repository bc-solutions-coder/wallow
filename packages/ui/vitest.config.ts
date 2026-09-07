import { fileURLToPath } from "node:url";

import { createVitestProjects } from "@bc-solutions-coder/testing";
import { storybookTest } from "@storybook/addon-vitest/vitest-plugin";
import { defineConfig } from "vitest/config";

/** Runs node, browser, and Storybook projects. The browser project has no Tailwind; Storybook supplies the real styles pipeline. vitest.setup.ts installs the browser guards, and .storybook/preview.tsx installs them for stories. */

/** Pre-bundle Base UI subpaths in both browser projects to avoid dependency-discovery reloads and duplicate React instances during tests. */
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
