/**
 * Browser-project setup: the three escape guards, installed once for every
 * spec in this project.
 *
 * Each guard is installed HERE rather than imported per spec because the spec
 * that leaks is not the spec the runner blames: an unvetoed cross-document
 * navigation drops the iframe, a console.error scrolls past a green run, and
 * an unowned fetch hangs in CI or passes against a live backend locally.
 *
 * The guards ride in from `@bc-solutions-coder/testing`'s subpath entries,
 * which import only `vitest` — taking them does not take the preset, its React
 * pre-bundle baseline, or any component tooling, so this package's React-free
 * charter is untouched (see `vitest.config.ts` for why the project pair itself
 * stays hand-rolled). The specs' own `vi.stubGlobal("fetch", …)` doubles sit
 * OVER the network guard's wrapper and `vi.unstubAllGlobals()` restores it, so
 * a stubbed transport never reads as an escape.
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
