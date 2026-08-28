/**
 * Browser-project setup: this package's own three escape guards, installed for
 * every spec in its browser project — the same wiring every consumer gets.
 *
 * Imported from `./src`, not from the package's own exports, for the same
 * reason `vitest.config.ts` imports `./src/vitest-projects`: a build has to be
 * able to run AFTER a green test run, so nothing here may resolve through
 * `dist/`.
 *
 * The guard specs themselves coexist with this: both installers are idempotent
 * (a per-spec install onto an already-guarded page is a no-op), and Vitest runs
 * `afterEach` hooks in reverse registration order, so a spec's own cleanup —
 * `network-escape.test.tsx` uninstalling to reach the bare global — runs before
 * the project assertions below.
 */

import { afterEach } from "vitest";

import { assertNoConsoleNoise, installConsoleGuard } from "./src/console-guard";
import { assertNoNavigationEscape, installNavigationEscapeGuard } from "./src/navigation-escape";
import { assertNoNetworkEscape, installNetworkEscapeGuard } from "./src/network-escape";

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
