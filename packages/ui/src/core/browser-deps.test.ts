/*
 * Pre-bundle guard for packages/ui. The machinery — and the long explanation of
 * why an unresolvable `optimizeDeps.include` entry is a silent warning rather
 * than an error — lives in `@bc-solutions-coder/testing/browser-deps`; this file
 * only points it at this package.
 *
 * BOTH browser projects are checked. Storybook runs its own Vite server with its
 * own dep cache, so `vitest.config.ts` repeats the Base UI and recipe-runtime
 * lists for it, and a repeated list is exactly the kind that drifts.
 *
 * Pure-logic spec: runs in the vitest NODE project.
 */

import { fileURLToPath } from "node:url";

import { describeBrowserPreBundleList } from "@bc-solutions-coder/testing/browser-deps";

import config from "../../vitest.config";

// This guard lives at src/core/, so TWO levels up reaches the package root.
const packageDir = fileURLToPath(new URL("../..", import.meta.url));

describeBrowserPreBundleList({ packageDir, config });
describeBrowserPreBundleList({ packageDir, config, projectName: "storybook" });
