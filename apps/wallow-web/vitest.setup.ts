/// <reference types="vite/client" />

/**
 * Browser-project setup: give every component spec the CSS the app itself
 * serves, compiled by the `wallowStyles()` plugin this project adds.
 *
 * Not cosmetic. A ui control gets its BOX from a Tailwind utility in its recipe,
 * so with no stylesheet the catalog checkbox's `<span role="checkbox">` measures
 * 0x0 and every spec that CLICKS it hangs to Playwright's actionability timeout.
 * See ./vitest-styles.css.
 *
 * Both halves, exactly as the running app has them:
 *
 *   - ./vitest-styles.css — the compiled Tailwind utilities.
 *   - virtual:wallow-theme.css — the fork theme, i.e. the custom-property VALUES
 *     those utilities read. `@bc-solutions-coder/styles`'s shared entry maps every
 *     colour token onto a valueless custom property, so without this a
 *     `bg-sidebar` element paints `rgba(0, 0, 0, 0)` and a rendered-colour
 *     assertion cannot tell a contrast defect from a passing test.
 *
 * The theme arrives as a VIRTUAL stylesheet served by `wallowStyles()`, not as a
 * JS `import { renderThemeStyle } from "@bc-solutions-coder/styles"`. That import
 * is what previously forced the theme out of this file: styles is a LINKED
 * workspace package, so the import landed as the setup file ran, re-optimized the
 * dep graph MID-RUN, and the reload handed the specs a second
 * `@tanstack/react-router` copy — after which a `redirect` thrown through one
 * module instance stopped satisfying `isRedirect` from the other
 * (`src/app/routes/index.gate.test.tsx` catches exactly that). A virtual id is
 * outside `node_modules`, so the optimizer has nothing to discover.
 */

import "./vitest-styles.css";
import "virtual:wallow-theme.css";
