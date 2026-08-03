/// <reference types="vite/client" />

/**
 * Browser-project setup: give every shell spec the CSS a real app serves, and a
 * navigation guard that turns a leaked hand-off into a failing assertion.
 *
 * The guard is installed HERE rather than imported per spec because the spec that
 * leaks is not the spec that reports: an unvetoed cross-document navigation drops
 * the iframe, and Vitest surfaces the error against whichever file it was loading
 * next. A guard a file has to opt into cannot catch the file that forgot.
 *
 * The CSS is two halves, exactly as a consuming app has them:
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
 *
 * The console guard beside it answers a leak of the same shape: React reports
 * real defects through `console.error` — a key collision, an update outside
 * `act`, a boundary catch — and every one of them scrolls past a run that
 * reports green. It is installed here for the same reason, and a spec that
 * drives an error path on purpose reads the messages back with
 * `consumeConsoleNoise()`/`expectConsoleError()` from the same entry instead of
 * letting the `afterEach` fail it.
 */

import {
  assertNoConsoleNoise,
  installConsoleGuard,
} from "@bc-solutions-coder/testing/console-guard";
import {
  assertNoNavigationEscape,
  installNavigationEscapeGuard,
} from "@bc-solutions-coder/testing/navigation-escape";
import { afterEach } from "vitest";

import "./vitest-styles.css";
import "virtual:wallow-theme.css";

installNavigationEscapeGuard();
installConsoleGuard();

// One afterEach PER guard: the hooks are independent, so a console failure
// cannot stop the navigation assertion from clearing its own record, and vice
// versa — either would leak a failure into the next test.
afterEach(() => {
  assertNoNavigationEscape();
});

afterEach(() => {
  assertNoConsoleNoise();
});
