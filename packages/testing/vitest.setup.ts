/**
 * Install each browser guard and assert its record after every test.
 * Guard specs restore their globals in their own cleanup hooks, which run before
 * these project-level assertions.
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
