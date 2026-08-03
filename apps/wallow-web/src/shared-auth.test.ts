import { currentUserQuery } from "@bc-solutions-coder/auth";
import type { WallowSdk } from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";
import { describe, expect, it } from "vitest";

/**
 * "Who is signed in" has exactly ONE definition in this workspace, and it is not in
 * this app: it is `@bc-solutions-coder/auth`. The query's own semantics are pinned
 * in `packages/auth`; what belongs here is the claim only this app can make — its
 * route gates and its profile screen meet on ONE cache entry.
 *
 * Node project: mounts nothing.
 */

describe("the auth package as this app resolves it", () => {
  it("keys the current-user query with the generated key the profile read uses", () => {
    // `src/features/settings/api.ts` re-exports `usersGetCurrentUserQueryKey`, so
    // the profile screen and the route gates share ONE cache entry. A hand-rolled
    // key (`['user','current']`) silently loses that.
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
