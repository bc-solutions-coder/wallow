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
 * There are no pure-logic/SSR `*.test.tsx` specs today, so `nodeTsxSpecs` is
 * empty: every `*.test.ts` (e.g. the on-disk scaffold guard) runs on node and
 * every `*.test.tsx` component spec runs in the browser project.
 */

/**
 * Every `@base-ui/react/*` subpath this package's components import, listed for
 * BOTH browser projects' `optimizeDeps.include`. This is not an optimisation —
 * it is required. Left to on-the-fly discovery, Vite pre-bundles a Base UI
 * subpath into a chunk carrying its own copy of React, and the first spec that
 * renders the part dies on `Cannot read properties of null (reading 'useRef')`;
 * in the storybook project it instead triggers a mid-run reload and the story
 * fails to fetch its shim.
 *
 * EVERY component task in the Base UI rebuild must append its own subpath here
 * as it lands (Wallow-m5aq.2.1 established this).
 */
const baseUiSubpaths = [
  "@base-ui/react/accordion",
  "@base-ui/react/alert-dialog",
  "@base-ui/react/autocomplete",
  "@base-ui/react/avatar",
  "@base-ui/react/button",
  "@base-ui/react/checkbox",
  "@base-ui/react/checkbox-group",
  "@base-ui/react/collapsible",
  "@base-ui/react/combobox",
  "@base-ui/react/context-menu",
  "@base-ui/react/dialog",
  "@base-ui/react/drawer",
  "@base-ui/react/field",
  "@base-ui/react/fieldset",
  "@base-ui/react/form",
  "@base-ui/react/input",
  "@base-ui/react/menu",
  "@base-ui/react/menubar",
  "@base-ui/react/meter",
  "@base-ui/react/navigation-menu",
  "@base-ui/react/number-field",
  "@base-ui/react/otp-field",
  "@base-ui/react/popover",
  "@base-ui/react/preview-card",
  "@base-ui/react/progress",
  "@base-ui/react/radio",
  "@base-ui/react/radio-group",
  "@base-ui/react/scroll-area",
  "@base-ui/react/select",
  "@base-ui/react/separator",
  "@base-ui/react/slider",
  "@base-ui/react/switch",
  "@base-ui/react/tabs",
  "@base-ui/react/toast",
  "@base-ui/react/toggle",
  "@base-ui/react/toggle-group",
  "@base-ui/react/toolbar",
  "@base-ui/react/tooltip",
  // Not a part: the `useRender` hook, which `ListRow` uses to get Base UI's
  // `render` contract on a plain `<li>` that wraps no headless part.
  "@base-ui/react/use-render",
];

/**
 * The recipe runtime every component pulls in through its `*.styles.ts` and
 * `src/core/cn.ts`. Same reason as the Base UI subpaths above: discovered on
 * the fly, they land mid-run ("dependencies optimized: class-variance-authority,
 * tailwind-merge" -> reload), which the storybook project cannot survive — its
 * stories then fail to fetch `@storybook_react-dom-shim.js`. Package-wide, so
 * unlike `baseUiSubpaths` this list does not grow per component.
 */
const recipeRuntime = ["class-variance-authority", "tailwind-merge"];

const { node, browser } = createVitestProjects({
  extraBrowserOptimizeDeps: [...baseUiSubpaths, ...recipeRuntime],
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
  optimizeDeps: { include: [...baseUiSubpaths, ...recipeRuntime] },
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
