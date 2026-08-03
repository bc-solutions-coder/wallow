/**
 * Browser-project setup: a navigation guard that turns a leaked hand-off into a
 * failing assertion.
 *
 * The guard is installed HERE rather than imported per spec because the spec that
 * leaks is not the spec that reports: an unvetoed cross-document navigation drops
 * the iframe, and Vitest surfaces the error against whichever file it was loading
 * next. A guard a file has to opt into cannot catch the file that forgot.
 *
 * This file loads no CSS, and that is deliberate — the `browser` project here
 * serves no Tailwind (see CLAUDE.md); the `storybook` project is where a story
 * meets the real pipeline, and it installs the guard through
 * .storybook/preview.tsx instead, since `storybookTest()` never reads
 * `browserSetupFiles`.
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
