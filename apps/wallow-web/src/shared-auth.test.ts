import {
  currentUserQuery,
  ensureCurrentUser,
  hasPermission,
  hasRole,
  isAdmin,
  loginRedirect,
  requireAuth,
  useCurrentUser,
} from "@bc-solutions-coder/auth";
import type { WallowSdk } from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";
import { describe, expect, it } from "vitest";

/**
 * "Who is signed in" has exactly ONE definition in this workspace, and it is not
 * in this app: it is `@bc-solutions-coder/auth` (Wallow-x4qn.8).
 *
 * wallow-web carried a byte-for-byte copy of that query in
 * `src/lib/current-user.ts` — the same generated operation, the same generated
 * key, the same `sub` rename, the same 30-second `staleTime` — and wallow-auth
 * carried a third variant inline. Copies of an auth read are the failure that
 * does not announce itself: they resolve the SAME cache key, so nothing breaks
 * until one copy drifts (a different `staleTime`, a 401 no longer softened to
 * `null`) and the gate on one route starts disagreeing with the gate on another.
 *
 * WHAT THIS FILE NO LONGER DOES (Wallow-xg9t.1). It used to pin the deletion by
 * stat'ing `src/shared/lib/current-user.ts`, sweeping every module's import
 * specifiers for `current-user`, and grepping the two route gates for
 * `ensureQueryData` and `context.sdk.client`. Those are disk and source-text
 * assertions. What they stood in for is pinned as behaviour instead: the gates
 * are driven through their real `beforeLoad` in `src/app/routes/index.ssr-gate.test.ts`
 * and `src/app/routes/dashboard/route.ssr.test.tsx`, which fail on a gate that
 * fetches per navigation or reads a module-global client — and a resurrected local
 * copy that disagreed with the package would have to fail one of them to matter.
 *
 * WHAT IS LEFT is the ADDITION, at the level a grep cannot reach: the package
 * this app resolves exposes the whole auth surface from one barrel, and its
 * current-user query carries the SAME generated key the profile screen's read uses
 * (`src/features/settings/api.ts`) — one cache entry for one resource, which is
 * the property the deleted local copy existed to provide and the one a hand-rolled
 * key (`['user','current']`) silently loses.
 *
 * Node project: mounts nothing.
 */

describe("the auth package as this app resolves it", () => {
  it("exposes the current-user layer and the route guards from one barrel", () => {
    // The point of the package: an app's auth imports come from ONE place instead
    // of being split between a local module and the SDK. These are named imports
    // resolved through this app's own link, so a missing export is a load-time
    // error rather than an assertion failure.
    expect(typeof currentUserQuery).toBe("function");
    expect(typeof ensureCurrentUser).toBe("function");
    expect(typeof useCurrentUser).toBe("function");
    expect(typeof hasRole).toBe("function");
    expect(typeof hasPermission).toBe("function");
    expect(typeof isAdmin).toBe("function");
    expect(typeof requireAuth).toBe("function");
    expect(typeof loginRedirect).toBe("function");
  });

  it("keys the current-user query with the generated key the profile read uses", () => {
    // `src/features/settings/api.ts` re-exports `usersGetCurrentUserQueryKey`, so
    // the profile screen and the route gates share ONE cache entry.
    //
    // The query's own semantics — the 401 softening, the `sub` rename and the
    // 30-second `staleTime` — are pinned once, in
    // `packages/auth/src/current-user.test.ts`, against a real SDK. Only the key
    // is asserted from this side, because only the key is a claim about how THIS
    // app's two readers meet.
    expect(currentUserQuery(fakeClient()).queryKey).toEqual(
      usersGetCurrentUserQueryKey({ client: fakeClient() }),
    );
  });
});

/**
 * The only thing a query KEY needs off a client: the base URL it is scoped to.
 * Building the key never issues a request, so a real transport would add nothing.
 */
function fakeClient(): WallowSdk["client"] {
  return { getConfig: () => ({ baseUrl: "https://wallow.test/api" }) } as WallowSdk["client"];
}
