import { browserPreBundleList } from "@bc-solutions-coder/testing/browser-deps";
import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * wallow-auth reaches TanStack Query through ONE door: `@bc-solutions-coder/query`.
 * Lint owns the import ban and pnpm owns the manifest; what neither can see is the
 * harness contract below.
 *
 * Node project: mounts nothing.
 */

/** The one place react-query is allowed to enter this workspace. */
const FACADE = "@bc-solutions-coder/query";

describe("browser-mode pre-bundling survives the facade hop", () => {
  it("registers the facade with the browser project rather than leaving it to discovery", () => {
    // A linked workspace package is not pre-bundled by default, and a dependency
    // discovered mid-run triggers a Vite reload that DROPS the runner instead of
    // failing a test — the worst failure mode in this app, whose specs are the
    // auth-flow safety net.
    //
    // Only the facade's PRESENCE is asserted here. That the list is non-empty,
    // that every entry is declared, and that every entry resolves from the app
    // root under Vite's own conditions are the shared guard's three cases, run
    // for this app by `src/browser-deps.test.ts`.
    expect(browserPreBundleList(vitestConfig)).toContain(FACADE);
  });
});
