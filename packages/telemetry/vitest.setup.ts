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
afterEach(assertNoConsoleNoise);
afterEach(assertNoNavigationEscape);
afterEach(assertNoNetworkEscape);
