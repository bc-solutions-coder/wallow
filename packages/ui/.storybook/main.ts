import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import type { StorybookConfig } from "@storybook/react-vite";

/**
 * Storybook 10 configuration for @bc-solutions-coder/ui.
 *
 * Storybook is not a side-car explorer here: `@storybook/addon-vitest` turns
 * every story into a Vitest test case, so this file is also the manifest the
 * `storybook` Vitest project in ../vitest.config.ts collects from. Stories are
 * co-located with their component under `src/components/<name>/`, exactly like
 * the `*.test.tsx` specs.
 *
 * `viteFinal` appends `wallowStyles()` — the same `@tailwindcss/vite` +
 * brand-assets plugin pair both apps use — so a story renders against the real
 * Tailwind pipeline rather than a Storybook-only approximation. The utilities
 * it emits are scoped by the `@source` rules in ./preview.css.
 */
const config: StorybookConfig = {
  framework: "@storybook/react-vite",
  stories: ["../src/components/**/*.stories.tsx"],
  addons: ["@storybook/addon-vitest"],
  // Stories run as part of `pnpm test`, so Storybook must stay offline and
  // deterministic here rather than phoning home on every suite run.
  core: { disableTelemetry: true },
  viteFinal: (viteConfig) => ({
    ...viteConfig,
    plugins: [...(viteConfig.plugins ?? []), ...wallowStyles()],
  }),
};

export default config;
