/// <reference types="vite/client" />

/**
 * Browser-project setup: give every catalog spec the CSS a real app serves.
 *
 * Two halves, exactly as a consuming app has them:
 *
 *   - ./vitest-styles.css — the compiled Tailwind utilities (see that file for
 *     why a spec that clicks a ui control cannot run without them).
 *   - the fork theme — the custom properties those utilities read. It is
 *     rendered from `forkResolvedBranding`, the same input an app's root route
 *     feeds `renderThemeStyle`, so a spec sees the fork's real token values
 *     rather than a palette hand-written for tests. This mirrors what
 *     packages/ui's Storybook preview does with its theme decorator.
 *
 * Setup files run once per spec FILE, each in its own page, so the guard below
 * is belt-and-braces rather than load-bearing.
 */

import "./vitest-styles.css";

import { forkResolvedBranding, renderThemeStyle } from "@bc-solutions-coder/styles";

const THEME_STYLE_ID = "wallow-forms-theme";

if (document.querySelector(`#${THEME_STYLE_ID}`) === null) {
  const themeStyle = document.createElement("style");
  themeStyle.id = THEME_STYLE_ID;
  themeStyle.textContent = renderThemeStyle(forkResolvedBranding);
  document.head.append(themeStyle);
}
