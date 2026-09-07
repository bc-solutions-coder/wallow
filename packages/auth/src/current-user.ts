/**
 * Fetch the current API user through the supplied SDK client and share the generated operation
 * query key. Only HTTP 401 becomes an anonymous result.
 */
import { queryOptions } from "@bc-solutions-coder/query";
import {
  getCurrentUser,
  type CurrentUserResponse,
  type WallowSdk,
  type WallowUser,
} from "@bc-solutions-coder/sdk";
import { usersGetCurrentUserQueryKey } from "@bc-solutions-coder/sdk/query";

/**
 * Freshness window used by current-user query observers. ensureCurrentUser still returns an
 * existing cache entry without refetching it.
 */
const CURRENT_USER_STALE_TIME_MS: number = 30_000;

/**
 * API user profile extended with sub for SDK authentication guards. sub copies id, or is an empty
 * string when id is absent.
 */
export type CurrentUser = CurrentUserResponse & WallowUser;

/**
 * Build current-user query options using the supplied SDK client and generated query key. HTTP
 * 401 resolves to null; other failures reject. Observers treat successful data as fresh for 30
 * seconds.
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
