import { browserPreBundleList } from "@bc-solutions-coder/testing/browser-deps";
import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * wallow-web reaches TanStack Query through ONE door: `@bc-solutions-coder/query`.
 * Lint owns the import ban and pnpm owns the manifest; what neither can see is what
 * the vitest harness hands Vite, which is what this file covers.
 *
 * Node project: mounts nothing.
 */

/** The facade's specifier, for the harness assertions that name it as a string. */
const FACADE = "@bc-solutions-coder/query";
/** The auth package that rides on the facade, linked the same way. */
const AUTH = "@bc-solutions-coder/auth";

describe("the vitest harness resolves the facade explicitly", () => {
  it("pre-bundles the facade and the auth package, which pnpm merely LINKS", () => {
    // A linked workspace package is not pre-bundled by default, and both of these
    // are imported from browser-project specs (a component reading a query; the
    // home-gate spec reading the current-user query). Unnamed, Vite discovers
    // them mid-run and reloads.
    const extras: readonly string[] = browserPreBundleList(vitestConfig);

    expect(extras).toContain(FACADE);
    expect(extras).toContain(AUTH);
  });

  it("inlines the facade for the node project instead of externalizing it", () => {
    // The node project runs the SSR-side route specs; without `ssr.noExternal`
    // the linked facade is externalized to a bare Node import instead of being
    // transformed.
    const noExternal = vitestConfig.ssr?.noExternal;

    expect(Array.isArray(noExternal) ? noExternal : []).toContain(FACADE);
  });
});
