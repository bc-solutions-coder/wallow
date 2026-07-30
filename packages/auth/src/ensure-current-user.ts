/**
 * The `beforeLoad` half of the current-user contract: resolve the user (or
 * `null`) into the request's query cache before a route renders, so the gate and
 * the components that follow read ONE answer.
 *
 * `ensureQueryData` rather than `fetchQuery` is the whole point — paired with the
 * query's 30-second `staleTime` it makes a gate on every navigation a cache read
 * instead of a request per route change.
 */
import type { QueryClient } from "@bc-solutions-coder/query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";

import { currentUserQuery, type CurrentUser } from "./current-user";

/** Options for {@link ensureCurrentUser}. */
export interface EnsureCurrentUserOptions {
  /** The request's query cache, off the router context — never a module-global one. */
  readonly queryClient: QueryClient;
  /** The request-scoped client from `createWallowSdk()` (`context.sdk.client`). */
  readonly client: WallowSdk["client"];
}

/**
 * Resolve the signed-in user into the query cache, for use in a route's
 * `beforeLoad`.
 *
 * @param options See {@link EnsureCurrentUserOptions}.
 * @returns The signed-in user, or `null` when the visitor is anonymous.
 */
export function ensureCurrentUser(options: EnsureCurrentUserOptions): Promise<CurrentUser | null> {
  return options.queryClient.ensureQueryData(currentUserQuery(options.client));
}
