/*
 * Pre-bundle guard. The machinery — and the long explanation of why an
 * unresolvable `optimizeDeps.include` entry is a silent warning rather than an
 * error — lives in `@bc-solutions-coder/testing/browser-deps`; this file only
 * points it at this app.
 *
 * Pure-logic spec: runs in the vitest NODE project.
 */

import { fileURLToPath } from "node:url";

import { describeBrowserPreBundleList } from "@bc-solutions-coder/testing/browser-deps";

import config from "../vitest.config";

const packageDir = fileURLToPath(new URL("..", import.meta.url));

describeBrowserPreBundleList({ packageDir, config });
