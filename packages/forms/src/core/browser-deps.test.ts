/**
 * Verify browser pre-bundle imports resolve and Base UI resolves to one directory.
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
