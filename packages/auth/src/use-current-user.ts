/**
 * The React hook every screen reads the signed-in user through.
 *
 * Deliberately imports NO router (the same rule `packages/sdk/src/route-context.ts`
 * follows): the client is passed in, so this package gains no dependency on
 * `@tanstack/react-router` and stays usable — and unit-testable — outside a
 * router. Screens get the client from their router context; nothing here reaches
 * for a module-global one.
 */
import { useQuery, type DefaultError, type UseQueryResult } from "@bc-solutions-coder/query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";

import { currentUserQuery, type CurrentUser } from "./current-user";

/**
 * Subscribe to the current-user query.
 *
 * @param client The request-scoped client from the router context
 *               (`context.sdk.client`).
 */
export function useCurrentUser(
  client: WallowSdk["client"],
): UseQueryResult<CurrentUser | null, DefaultError> {
  return useQuery(currentUserQuery(client));
}
