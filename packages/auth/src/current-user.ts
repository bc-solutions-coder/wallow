/**
 * The current-user query every app's router gates read — the ONE canonical
 * definition of "who is signed in" for this workspace.
 *
 * The deleted hand-written query layer answered this from the BFF's own
 * `/bff/user` endpoint through a module-global SSR request context. Both halves
 * of that are gone: the context module is deleted, and its server branch was
 * already unreachable (nothing had installed a resolver since the app moved to
 * Start's per-request middleware), so a server render was left calling `fetch`
 * with a relative URL that Node cannot parse.
 *
 * This asks the API instead, through the request's own SDK instance — which is
 * what carries the session cookie and the internal origin a server render needs,
 * so one query works on both sides. It is the GENERATED operation and the
 * GENERATED key; the only hand-written parts are the two things the generator
 * cannot express:
 *
 *   1. a 401 is the ANSWER "anonymous", not a failure — the SDK's
 *      `getCurrentUser` owns that softening, and without it every anonymous
 *      visitor would hit a route's error boundary instead of its login gate;
 *   2. `sub`, so the resolved user satisfies the SDK's `WallowUser` and the
 *      shared `requireAuth` guard can read it. It is a rename, not an
 *      invention: `UsersController.GetCurrentUser` fills `Id` from
 *      `User.GetUserId()`, i.e. the very `sub` claim `AuthorizationController`
 *      issued.
 *
 * The 30-second `staleTime` is what keeps a `beforeLoad` that calls
 * `ensureQueryData` on every navigation from re-reading the user on each route
 * change (see `docs/development/frontend-state.md`).
 */
import { queryOptions } from "@bc-solutions-coder/query";
import {
  getCurrentUser,
  type CurrentUserResponse,
  type WallowSdk,
  type WallowUser,
} from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";

/** How long a resolved user is held before `beforeLoad` reads it again. */
const CURRENT_USER_STALE_TIME_MS: number = 30_000;

/**
 * The signed-in user: every field the API answers with, plus the `sub` the SDK's
 * claim helpers key off.
 */
export type CurrentUser = CurrentUserResponse & WallowUser;

/**
 * queryOptions for the signed-in user, resolving `null` when the browser is
 * anonymous.
 *
 * @param client The request-scoped client from `createWallowSdk()`, off the
 *               router context — never a module-global one.
 */
export function currentUserQuery(client: WallowSdk["client"]) {
  return queryOptions({
    queryKey: usersGetCurrentUserQueryKey({ client }),
    queryFn: async (): Promise<CurrentUser | null> => {
      const user: CurrentUserResponse | null = await getCurrentUser({ client });

      return user === null ? null : { ...user, sub: user.id ?? "" };
    },
    staleTime: CURRENT_USER_STALE_TIME_MS,
  });
}
