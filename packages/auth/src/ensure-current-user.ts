/**
 * Populate the current-user query when it has no cached data. Existing cached data is returned
 * without a freshness check.
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
 * Return the cached current user, or fetch and cache it when absent. A cached null remains an
 * anonymous result until the cache changes. HTTP 401 resolves to null; other fetch failures
 * reject.
 */
export function ensureCurrentUser(options: EnsureCurrentUserOptions): Promise<CurrentUser | null> {
  return options.queryClient.ensureQueryData(currentUserQuery(options.client));
}
