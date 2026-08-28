/// <reference types="vite/client" />

/**
 * Browser-project setup: give every component spec the CSS the app itself
 * serves, compiled by the `wallowStyles()` plugin this project adds, and a
 * navigation guard that turns a leaked hand-off into a failing assertion.
 *
 * The guard is installed HERE rather than imported per spec because the spec that
 * leaks is not the spec that reports: an unvetoed cross-document navigation drops
 * the iframe, and Vitest surfaces the error against whichever file it was loading
 * next. A guard a file has to opt into cannot catch the file that forgot. Only
 * CROSS-document hand-offs are vetoed, so this app's real router keeps routing.
 *
 * This app's screens hand the browser off for real to finish an OIDC flow, so a
 * spec asserting one of those calls `expectNavigationEscape()` from the same
 * entry: it reads the hand-off out of the guard's own record, which is what keeps
 * the `afterEach` below from failing a test that meant to navigate.
 *
 * The stylesheet has two halves, exactly as the running app has them:
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
 *
 * The console guard beside it answers a leak of the same shape: React reports
 * real defects through `console.error` — a key collision, an update outside
 * `act`, a boundary catch — and every one of them scrolls past a run that
 * reports green. It is installed here for the same reason, and a spec that
 * drives an error path on purpose reads the messages back with
 * `consumeConsoleNoise()`/`expectConsoleError()` from the same entry instead of
 * letting the `afterEach` fail it.
 *
 * The network guard completes the trio: anything reaching `globalThis.fetch` is
 * traffic no `createSdkHarness()` owns, so it is answered with a 503 naming the
 * request rather than left to leave for the real network — where it hangs in CI
 * and passes against a live backend locally. A spec that provokes such a request
 * on purpose reads it back with `consumeNetworkEscapes()` from the same entry.
 */

import {
  assertNoConsoleNoise,
  installConsoleGuard,
} from "@bc-solutions-coder/testing/console-guard";
import {
  assertNoNavigationEscape,
  installNavigationEscapeGuard,
} from "@bc-solutions-coder/testing/navigation-escape";
import {
  assertNoNetworkEscape,
  installNetworkEscapeGuard,
} from "@bc-solutions-coder/testing/network-escape";
import { afterEach } from "vitest";

import "./vitest-styles.css";
import "virtual:wallow-theme.css";

installNavigationEscapeGuard();
installConsoleGuard();
installNetworkEscapeGuard();

// One afterEach PER guard: the hooks are independent, so one guard's failure
// cannot stop another from clearing its own record — either would leak a
// failure into the next test.
afterEach(() => {
  assertNoNavigationEscape();
});

afterEach(() => {
  assertNoConsoleNoise();
});

afterEach(() => {
  assertNoNetworkEscape();
});
