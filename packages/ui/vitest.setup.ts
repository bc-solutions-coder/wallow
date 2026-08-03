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
 */

import {
  assertNoNavigationEscape,
  installNavigationEscapeGuard,
} from "@bc-solutions-coder/testing/navigation-escape";
import { afterEach } from "vitest";

installNavigationEscapeGuard();

afterEach(() => {
  assertNoNavigationEscape();
});
