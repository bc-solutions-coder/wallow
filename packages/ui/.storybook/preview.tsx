import "./preview.css";

import { forkResolvedBranding, renderThemeStyle } from "@bc-solutions-coder/styles";
import {
  assertNoNavigationEscape,
  installNavigationEscapeGuard,
} from "@bc-solutions-coder/testing/navigation-escape";
import type { Decorator, Preview } from "@storybook/react-vite";

/**
 * Storybook preview annotations for @bc-solutions-coder/ui.
 *
 * Every story renders inside the fork's real theme: `renderThemeStyle` is fed
 * the same `forkResolvedBranding` an app's root route feeds it, so the custom
 * properties in Storybook are byte-for-byte the ones packages/styles/branding.json produces
 * for wallow-web. A hand-written palette here would let a component look right
 * in Storybook and wrong in the app.
 *
 * The `<style>` element mirrors what the `DocumentStyles` component does for an
 * app; it is inlined rather than imported from the library so the preview stays
 * independent of the components it is documenting.
 */
const themeDecorator: Decorator = (Story) => (
  <>
    <style>{renderThemeStyle(forkResolvedBranding)}</style>
    <Story />
  </>
);

export const decorators: Decorator[] = [themeDecorator];

/**
 * The navigation-escape guard, wired the only way the `storybook` Vitest project
 * can take it. `storybookTest()` builds that project itself and never reads
 * `browserSetupFiles`, so ../vitest.setup.ts — which carries the same two calls
 * for the plain `browser` project — does not reach a story. These preview-level
 * hooks do: Storybook runs `beforeEach` before, and `afterEach` after, EVERY
 * story, play function or not.
 *
 * Without them a story that lets a cross-document hand-off reach the iframe
 * drops the runner mid-file, and Vitest blames whichever file it was loading
 * next. `installNavigationEscapeGuard` is idempotent per browser context, so
 * calling it per story costs one comparison rather than a second listener.
 */
export const beforeEach: Preview["beforeEach"] = () => {
  installNavigationEscapeGuard();
};

export const afterEach: Preview["afterEach"] = () => {
  assertNoNavigationEscape();
};

export const parameters: Preview["parameters"] = {
  controls: { matchers: { color: /(?<colorProp>background|color)$/iu, date: /Date$/u } },
};
