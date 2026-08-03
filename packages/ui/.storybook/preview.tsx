import "./preview.css";

import { forkResolvedBranding, renderThemeStyle } from "@bc-solutions-coder/styles";
import {
  assertNoConsoleNoise,
  installConsoleGuard,
} from "@bc-solutions-coder/testing/console-guard";
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
 * The navigation-escape and console guards, wired the only way the `storybook`
 * Vitest project can take them. `storybookTest()` builds that project itself and
 * never reads `browserSetupFiles`, so ../vitest.setup.ts — which carries the same
 * calls for the plain `browser` project — does not reach a story. These
 * preview-level hooks do: Storybook runs `beforeEach` before, and `afterEach`
 * after, EVERY story, play function or not.
 *
 * Without them a story that lets a cross-document hand-off reach the iframe drops
 * the runner mid-file and Vitest blames whichever file it was loading next, and a
 * story whose component logs a React defect through `console.error` renders it
 * into a green run. Both installers are idempotent per browser context, so
 * calling them per story costs a comparison rather than a second listener.
 */
export const beforeEach: Preview["beforeEach"] = () => {
  installNavigationEscapeGuard();
  installConsoleGuard();
};

/**
 * A preview exports ONE `afterEach`, where the setup files register a hook per
 * guard — so both assertions have to be driven by hand. Each must RUN even when
 * the one before it threw, because a guard clears its own record only as it
 * fails: a skipped assertion leaves its entries in place and fails the NEXT
 * story instead.
 */
export const afterEach: Preview["afterEach"] = () => {
  const failures: unknown[] = [];

  for (const assert of [assertNoNavigationEscape, assertNoConsoleNoise]) {
    try {
      assert();
    } catch (error: unknown) {
      failures.push(error);
    }
  }

  const [first, ...rest] = failures;
  if (first === undefined) {
    return;
  }

  throw rest.length === 0
    ? first
    : new AggregateError(failures, "This story leaked past two guards");
};

export const parameters: Preview["parameters"] = {
  controls: { matchers: { color: /(?<colorProp>background|color)$/iu, date: /Date$/u } },
};
