import {
  createQueryClient,
  QueryClient,
  QueryClientProvider,
  queryOptions,
  useMutation,
  useQuery,
  useQueryClient,
} from "@bc-solutions-coder/query";
import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * wallow-web reaches TanStack Query through ONE door: `@bc-solutions-coder/query`,
 * the workspace facade. This spec is that door's lock (Wallow-x4qn.8).
 *
 * This app matters more than most for the lock: it is the REFERENCE dashboard a
 * fork copies from, so every import site here is a pattern that gets duplicated
 * downstream. A single surviving `@tanstack/react-query` import teaches the door
 * the facade exists to close — and, worse, is how a second copy of the library
 * (a second `QueryClientProvider` context, and a `useQuery` that throws
 * "No QueryClient set" against a provider it does not recognise) gets into the
 * graph.
 *
 * THE IMPORT HALF OF THAT LOCK IS LINT'S (Wallow-l5x2). The root `.oxlintrc.json`
 * restricts `@tanstack/react-query` outright, and since the lint split
 * (`pnpm lint` + `pnpm lint:tests`) the ban reaches specs as well as source.
 *
 * THE MANIFEST HALF IS PNPM'S. This file used to assert that `package.json`
 * declares the facade and not react-query. It cannot be otherwise and still pass:
 * under pnpm's strict `node_modules` a package resolves only what it declares, so
 * the facade import at the top of this file IS the manifest entry, and a
 * react-query entry that nothing imports changes no behaviour to assert.
 *
 * WHAT IS LEFT IS WHAT NEITHER CAN SEE: what the vitest harness hands Vite, and
 * what the facade actually gives this app at runtime.
 *
 * Node project: mounts nothing.
 */

/** The facade's specifier, for the harness assertions that name it as a string. */
const FACADE = "@bc-solutions-coder/query";
/** The auth package that rides on the facade, linked the same way. */
const AUTH = "@bc-solutions-coder/auth";

describe("the vitest harness resolves the facade explicitly", () => {
  // There is deliberately no spec pinning a `@tanstack/react-query` entry in the
  // pre-bundle list. One was here, on the theory that react-query is still the
  // module Vite pre-bundles, one facade hop away. It is not: this app does not
  // declare react-query, so under pnpm's strict `node_modules` the entry resolved
  // to nothing and Vite logged `Failed to resolve dependency` once per run and
  // pre-bundled nothing at all. The facade entry below is what does the work, and
  // `src/browser-deps.test.ts` now asserts the general invariant the dead entry
  // hid behind — every entry in the list must actually resolve.

  it("pre-bundles the facade and the auth package, which pnpm merely LINKS", () => {
    // A linked workspace package is not pre-bundled by default, and both of these
    // are imported from browser-project specs (a component reading a query; the
    // home-gate spec reading the current-user query). Unnamed, Vite discovers
    // them mid-run and reloads.
    const extras: readonly string[] = browserPreBundleList();

    expect(extras).toContain(FACADE);
    expect(extras).toContain(AUTH);
  });

  it("inlines the facade for the node project instead of externalizing it", () => {
    // The node project runs the SSR-side route specs; without `ssr.noExternal`
    // the linked facade is externalized to a bare Node import instead of being
    // transformed. Same knob `packages/testing`'s own config carries.
    const noExternal = vitestConfig.ssr?.noExternal;

    expect(Array.isArray(noExternal) ? noExternal : []).toContain(FACADE);
  });
});

describe("the facade as this app resolves it", () => {
  it("hands the app a QueryClient factory and the whole react-query surface", () => {
    // Named imports, resolved through this app's own `node_modules` — the same
    // link a component's import walks. A missing re-export is a load-time error
    // here, not an assertion failure.
    expect(typeof createQueryClient).toBe("function");
    expect(typeof useQuery).toBe("function");
    expect(typeof useMutation).toBe("function");
    expect(typeof useQueryClient).toBe("function");
    expect(typeof queryOptions).toBe("function");
    expect(typeof QueryClientProvider).toBe("function");
    expect(typeof QueryClient).toBe("function");
  });

  it("gives the router a retry-disabled client of the facade's own QueryClient type", () => {
    const client: QueryClient = createQueryClient();

    // One module instance, so `instanceof` holds — the symptom of two copies is
    // a runtime "No QueryClient set" from a provider the hook does not recognise.
    expect(client).toBeInstanceOf(QueryClient);
    expect(client.getDefaultOptions().queries?.retry).toBe(false);
  });
});

/**
 * The browser project's `optimizeDeps.include`, read off the CONFIG OBJECT.
 *
 * This used to regex `vitest.config.ts` for a `const extraBrowserOptimizeDeps =
 * [...]` declaration, on the stated grounds that importing the config would boot
 * a second browser provider. It does not: `playwright()` returns a descriptor and
 * nothing launches until vitest runs the project — `src/browser-deps.test.ts` has
 * imported the same config from the same node project all along. Reading the value
 * asserts what Vite actually receives rather than how the file happens to be
 * written, so inlining the list into the `createVitestProjects` call no longer
 * moves the goalposts.
 */
function browserPreBundleList(): readonly string[] {
  const projects = (vitestConfig.test?.projects ?? []) as readonly {
    optimizeDeps?: { include?: readonly string[] };
    test?: { name?: string };
  }[];

  return projects.find((project) => project.test?.name === "browser")?.optimizeDeps?.include ?? [];
}
