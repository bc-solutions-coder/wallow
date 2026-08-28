/// <reference types="vite/client" />

/**
 * Browser-project setup: give every catalog spec the CSS a real app serves, and
 * a navigation guard that turns a leaked hand-off into a failing assertion.
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
