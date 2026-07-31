/// <reference types="vite/client" />

/**
 * Browser-project setup: give every component spec the CSS the app itself
 * serves, compiled by the `wallowStyles()` plugin this project adds
 * (Wallow-8ytl — before it, this app's browser project loaded NO stylesheet at
 * all).
 *
 * Two halves, exactly as the running app has them:
 *
 *   - ./vitest-styles.css — the compiled Tailwind utilities. Not cosmetic: a ui
 *     control gets its BOX from a Tailwind utility in its recipe, so with no
 *     stylesheet the catalog checkbox's `<span role="checkbox">` measures 0x0 and
 *     every spec that CLICKS it hangs to Playwright's actionability timeout.
 *     Four specs here carried a focus+Space workaround for exactly that.
 *   - virtual:wallow-theme.css — the fork theme, i.e. the custom-property VALUES
 *     those utilities read. `@bc-solutions-coder/styles`'s shared entry maps every
 *     colour token onto a valueless custom property, so without it a `bg-card`
 *     element paints `rgba(0, 0, 0, 0)` and a rendered-colour assertion cannot
 *     tell a contrast defect from a passing test.
 *
 * The theme arrives as a VIRTUAL stylesheet served by `wallowStyles()`, never as
 * a JS `import { renderThemeStyle } from "@bc-solutions-coder/styles"`. styles is
 * a LINKED workspace package, so that import would be Vite's first sight of the
 * dep, re-optimize the graph MID-RUN and reload the page with a second copy of
 * `@tanstack/react-router` — the failure that kept the theme out of wallow-web's
 * setup file. A virtual id lives outside `node_modules`, so there is nothing for
 * the optimizer to discover.
 */

import "./vitest-styles.css";
import "virtual:wallow-theme.css";
