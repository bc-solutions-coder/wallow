/// <reference types="vite/client" />

/**
 * Browser-project setup: give every shell spec the CSS a real app serves.
 *
 * Two halves, exactly as a consuming app has them:
 *
 *   - ./vitest-styles.css — the compiled Tailwind utilities (see that file for
 *     why a spec that clicks a ui control cannot run without them).
 *   - virtual:wallow-theme.css — the fork theme, i.e. the custom-property VALUES
 *     those utilities read. `@bc-solutions-coder/styles`'s shared entry maps
 *     every colour token onto a valueless custom property, so without this a
 *     `bg-sidebar` rail paints `rgba(0, 0, 0, 0)` and a contrast assertion
 *     cannot tell a defect from a passing test.
 *
 * The theme arrives as a VIRTUAL stylesheet served by `wallowStyles()`, not as a
 * JS `import { renderThemeStyle } from "@bc-solutions-coder/styles"`. styles is
 * a LINKED workspace package, so that import is Vite's first sight of the
 * dependency: it re-optimizes the graph mid-run and the reload hands the specs a
 * second `@tanstack/react-router` copy. A virtual id lives outside
 * `node_modules`, so there is nothing to discover.
 */

import "./vitest-styles.css";
import "virtual:wallow-theme.css";
