import { browserPreBundleList } from "@bc-solutions-coder/testing/browser-deps";
import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * This app's auth reads come through `@bc-solutions-coder/auth`, a LINKED workspace
 * package — which the harness has to name, because pnpm links it rather than
 * installing it and Vite pre-bundles neither by default.
 *
 * Node project: mounts nothing.
 */

/** The shared authn layer, and the only door this app's auth reads come through. */
const AUTH = "@bc-solutions-coder/auth";

describe("browser-mode pre-bundling covers the auth package", () => {
  it("registers it with the browser project rather than leaving it to discovery", () => {
    // A dependency discovered mid-run triggers a Vite reload that DROPS the runner
    // instead of failing a test, and the route specs in this app are the auth
    // flow's safety net — a silent reload there is the worst failure mode.
    const noExternal = vitestConfig.ssr?.noExternal;
    const inlinedForSsr: boolean = Array.isArray(noExternal) && noExternal.includes(AUTH);

    expect(inlinedForSsr || browserPreBundleList(vitestConfig).includes(AUTH)).toBe(true);
  });
});
