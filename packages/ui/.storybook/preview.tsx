import "./preview.css";

import { forkResolvedBranding, renderThemeStyle } from "@bc-solutions-coder/styles";
import type { Decorator, Preview } from "@storybook/react-vite";

/**
 * Storybook preview annotations for @bc-solutions-coder/ui.
 *
 * Every story renders inside the fork's real theme: `renderThemeStyle` is fed
 * the same `forkResolvedBranding` an app's root route feeds it, so the custom
 * properties in Storybook are byte-for-byte the ones api/branding.json produces
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

export const parameters: Preview["parameters"] = {
  controls: { matchers: { color: /(background|color)$/iu, date: /Date$/u } },
};
