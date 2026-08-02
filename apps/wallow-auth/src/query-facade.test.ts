import {
  createQueryClient,
  QueryClient,
  QueryClientProvider,
  useMutation,
  useQuery,
  useQueryClient,
} from "@bc-solutions-coder/query";
import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * wallow-auth reaches TanStack Query through ONE door: `@bc-solutions-coder/query`,
 * the workspace facade. This spec is the half of that door's lock a linter cannot
 * turn (Wallow-x4qn.9.1, narrowed by Wallow-l5x2 and again by Wallow-xg9t.1).
 *
 * WHAT LINT ALREADY OWNS, and what this file therefore no longer sweeps. The root
 * `.oxlintrc.json` restricts `@tanstack/react-query` outright, and since the lint
 * split (`pnpm lint` + `pnpm lint:tests`) that ban reaches SPECS as well as source —
 * which is what a per-file import table was for, because a spec holding its own
 * `QueryClient` binding is the copy-identity bug in miniature.
 *
 * WHAT PNPM OWNS, and what this file no longer restates. It used to assert that the
 * manifest declares the facade and not react-query, and that pnpm consequently links
 * neither react-query nor a stale facade into `node_modules`. Under pnpm's strict
 * `node_modules` those are the same fact as this file's own import resolving: a
 * package reaches only what it declares, so the facade import above IS the manifest
 * entry, and a direct react-query import would fail to resolve rather than fail an
 * assertion.
 *
 * WHAT IS LEFT is the runtime shape of what that door hands back, plus one config
 * contract of the same family: a linked workspace package is not pre-bundled by
 * default, so the browser project has to name the facade.
 *
 * DELIBERATELY GONE with the manifest cases: the cross-package copy-identity pole,
 * which imported the facade through `packages/testing`'s and `packages/forms`' own
 * links and compared module objects. It could only be written by walking
 * `node_modules` from three directories, and `workspace:*` leaves nothing for it to
 * catch — every importer's link is a symlink to the same `packages/query`, so the
 * comparison held by construction.
 *
 * Node project — it mounts nothing.
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
    expect(browserPreBundleList()).toContain(FACADE);
  });
});

describe("the facade as wallow-auth resolves it", () => {
  it("hands the app a QueryClient factory and the react-query surface the screens use", () => {
    // Named imports, resolved through this app's own `node_modules` — the same
    // link a screen's import walks. A missing re-export is a load-time error
    // here, not an assertion failure.
    expect(typeof createQueryClient).toBe("function");
    expect(typeof QueryClient).toBe("function");
    expect(typeof QueryClientProvider).toBe("function");
    expect(typeof useQuery).toBe("function");
    expect(typeof useMutation).toBe("function");
    expect(typeof useQueryClient).toBe("function");
  });

  it("gives the router a retry-disabled client of the facade's own QueryClient type", () => {
    const client: QueryClient = createQueryClient();

    // The symptom of two copies is a runtime "No QueryClient set" from a provider
    // the hook does not recognise, so identity — not shape — is what is asserted.
    expect(client).toBeInstanceOf(QueryClient);
    expect(client.getDefaultOptions().queries?.retry).toBe(false);
  });
});

/**
 * The browser project's `optimizeDeps.include`, read off the CONFIG OBJECT.
 *
 * This used to regex `vitest.config.ts` for a `const extraBrowserOptimizeDeps =
 * [...]` declaration, on the stated grounds that importing the config would boot
 * a second browser provider. It does not: `playwright()` returns a descriptor
 * and nothing launches until vitest runs the project — `src/browser-deps.test.ts`
 * has imported the same config from the same node project all along. Reading the
 * value also asserts what Vite actually receives rather than how the file happens
 * to be written, so inlining the list into the `createVitestProjects` call no
 * longer moves the goalposts.
 */
function browserPreBundleList(): readonly string[] {
  const projects = (vitestConfig.test?.projects ?? []) as readonly {
    optimizeDeps?: { include?: readonly string[] };
    test?: { name?: string };
  }[];

  return projects.find((project) => project.test?.name === "browser")?.optimizeDeps?.include ?? [];
}
