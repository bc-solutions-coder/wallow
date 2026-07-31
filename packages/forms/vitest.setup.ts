/// <reference types="vite/client" />

/**
 * Browser-project setup: give every catalog spec the CSS a real app serves.
 *
 * Two halves, exactly as a consuming app has them:
 *
 *   - ./vitest-styles.css — the compiled Tailwind utilities (see that file for
 *     why a spec that clicks a ui control cannot run without them).
 *   - virtual:wallow-theme.css — the fork theme, i.e. the custom-property VALUES
 *     those utilities read. It is the `renderThemeStyle(forkResolvedBranding)`
 *     output, the same input an app's root route feeds, so a spec sees the fork's
 *     real token values rather than a palette hand-written for tests.
 *
 * The theme used to be injected here by importing `renderThemeStyle` from
 * `@bc-solutions-coder/styles` and appending a `<style>`. It is now a VIRTUAL
 * stylesheet served by `wallowStyles()`, so this file, apps/wallow-web and
 * apps/wallow-auth all share ONE implementation (Wallow-8ytl). The move also
 * removes a hazard this package never hit but wallow-web did: styles is a LINKED
 * workspace package, so importing it from a setup file is Vite's first sight of
 * that dep, and the resulting mid-run re-optimize reloads the page with a second
 * copy of the router. A virtual id has nothing for the optimizer to discover.
 */

import "./vitest-styles.css";
import "virtual:wallow-theme.css";
