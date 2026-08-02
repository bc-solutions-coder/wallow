import { currentUserQuery, useCurrentUser } from "@bc-solutions-coder/auth";
import { describe, expect, it } from "vitest";

import vitestConfig from "../vitest.config";

/**
 * "Who is signed in" has exactly ONE definition in this workspace, and it is not
 * in this app: it is `@bc-solutions-coder/auth` (Wallow-x4qn.9.2).
 *
 * wallow-auth carried a third variant of that query, hand-rolled inline inside
 * `routes/invitation.tsx`'s `InvitationRoute()`: the same generated key, its own
 * `queryFn` over the SDK's `getCurrentUser`, a local `retry: false`, and NEITHER
 * of the two things the canonical query adds — the 30-second `staleTime` and the
 * `sub` rename that satisfies the SDK's claim helpers. wallow-web's copy was
 * deleted by Wallow-x4qn.8 (`src/shared-auth.test.ts`, which this file mirrors);
 * this is the last one.
 *
 * A copy of an auth read is the failure that does not announce itself: both
 * copies resolve the SAME cache key, so nothing breaks until one drifts — and
 * this one had already drifted, in the direction of a probe that re-fetches on
 * every mount and stores a user the shared guards cannot read.
 *
 * WHAT THIS FILE NO LONGER DOES (Wallow-xg9t.1). It used to pin the deletion by
 * reading `routes/invitation.tsx` as text and sweeping every other module under
 * `src/` for the probe's parts, its `queryFn`, and a redeclared `currentUserQuery`.
 * Those are source greps, and the behaviour they stood in for is already pinned
 * where it belongs: `src/app/routes/invitation.test.tsx` drives the surviving read
 * in a real browser against the real SDK — a 200, a 401, a 500, and a user the
 * shared query already primed — so a reintroduced hand-rolled probe with no
 * `staleTime` and no `sub` rename fails there on behaviour rather than on spelling.
 *
 * WHAT IS LEFT is the shared package's own contract, which nothing else asserts
 * from this app's side, plus the harness wiring the linked package needs.
 *
 * Node project: mounts nothing.
 */

/** The shared authn layer, and the only door this app's auth reads come through. */
const AUTH = "@bc-solutions-coder/auth";

describe("the auth package as wallow-auth resolves it", () => {
  it("hands this app the current-user hook", () => {
    // Named imports, resolved through this app's own link. A missing export is a
    // load-time error here, not an assertion failure.
    //
    // What the query DOES — the generated key, the 401 softening, the `sub`
    // rename, and the 30-second `staleTime` that makes a re-mounted probe a cache
    // read — is pinned once, in `packages/auth/src/current-user.test.ts`, against
    // a real SDK. Restating any of it here would be a second copy of the contract
    // this task exists to collapse.
    expect(typeof useCurrentUser).toBe("function");
    expect(typeof currentUserQuery).toBe("function");
  });
});

describe("browser-mode pre-bundling covers the auth package", () => {
  it("registers it with the browser project rather than leaving it to discovery", () => {
    // A linked workspace package is not pre-bundled by default, and a dependency
    // discovered mid-run triggers a Vite reload that DROPS the runner instead of
    // failing a test. wallow-web already names this package for the same reason
    // (its `vitest.config.ts`), and the route specs in this app are the auth
    // flow's safety net — a silent reload there is the worst failure mode.
    const noExternal = vitestConfig.ssr?.noExternal;
    const inlinedForSsr: boolean = Array.isArray(noExternal) && noExternal.includes(AUTH);

    expect(inlinedForSsr || browserPreBundleList().includes(AUTH)).toBe(true);
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
