/**
 * Browser-project setup: the three escape guards, installed once for every
 * spec in this project.
 *
 * Each guard is installed HERE rather than imported per spec because the spec
 * that leaks is not the spec the runner blames: an unvetoed cross-document
 * navigation drops the iframe, a console.error scrolls past a green run, and
 * an unowned fetch hangs in CI or passes against a live backend locally. A
 * guard a file has to opt into cannot catch the file that forgot.
 *
 * No stylesheet is imported: this app's specs assert visibility and wiring,
 * not painted colour, so the project runs without the Tailwind/theme pair the
 * product apps attach.
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
