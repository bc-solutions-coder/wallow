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
 * The utilities only, deliberately — NOT the fork theme that packages/forms'
 * setup file also renders. The theme supplies the custom properties the COLOUR
 * utilities read; nothing about an element's geometry depends on it, so it buys
 * this project nothing, and importing `@bc-solutions-coder/styles` here costs
 * something real: it is a LINKED workspace package, so the import lands as the
 * setup file runs and re-optimizes the dep graph MID-RUN. That reload hands the
 * specs a second `@tanstack/react-router` copy, after which a `redirect` thrown
 * through one module instance stops satisfying `isRedirect` from the other
 * (`src/app/routes/index.gate.test.tsx` catches exactly that).
 */

import "./vitest-styles.css";
