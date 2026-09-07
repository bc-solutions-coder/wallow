/**
 * Subscribe to the shared current-user query without depending on a router.
 */
import { useQuery, type DefaultError, type UseQueryResult } from "@bc-solutions-coder/query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";

import { currentUserQuery, type CurrentUser } from "./current-user";

/**
 * Subscribe to the current user under a QueryClientProvider. Pass the SDK client for this browser
 * session or SSR request. data is undefined while loading, null when anonymous, or the user
 * profile; non-401 failures populate error.
 */
export function useCurrentUser(
  client: WallowSdk["client"],
): UseQueryResult<CurrentUser | null, DefaultError> {
  return useQuery(currentUserQuery(client));
}
