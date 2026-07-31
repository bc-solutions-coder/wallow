/*
 * Pre-bundle guard for packages/forms. The machinery — and the long explanation
 * of why an unresolvable `optimizeDeps.include` entry is a silent warning rather
 * than an error — lives in `@bc-solutions-coder/testing/browser-deps`; this file
 * only points it at this package.
 *
 * This package is where that failure was first diagnosed, which is why the check
 * now runs everywhere with a browser project instead of here alone.
 *
 * Pure-logic spec: runs in the vitest NODE project.
 */

import { fileURLToPath } from "node:url";

import {
  describeBrowserPreBundleList,
  describeSharedBaseUi,
} from "@bc-solutions-coder/testing/browser-deps";

import config from "../../vitest.config";

// This guard lives at src/core/, so TWO levels up reaches the package root.
const packageDir = fileURLToPath(new URL("../..", import.meta.url));

describeBrowserPreBundleList({ packageDir, config });

// This package declares `@base-ui/react` itself while reaching the parts through
// `@bc-solutions-coder/ui`, so the two must resolve to one directory.
describeSharedBaseUi(packageDir);
