/**
 * Settings (Profile) query module — READ-ONLY.
 *
 * Ported from apps/wallow-web/src/features/settings/api.ts. Profile is rendered
 * read-only from the authenticated `CurrentUserResponse`; there is NO backend
 * endpoint to mutate firstName/lastName/email from this surface, so this module
 * exposes ONLY a `profile()` query (there is no mutation and thus no invalidation
 * test). The queryFn keys off `queryKeys.settings.profile()`, calls
 * `ensureQueryBootstrapped()`, then `unwrap(getV1IdentityUsersMe())`.
 */
import { queryOptions } from "@tanstack/react-query";

import { unwrap } from "../facade";
import { getV1IdentityUsersMe } from "../generated";
import { ensureQueryBootstrapped } from "./bootstrap";
import { queryKeys } from "./keys";

/**
 * queryOptions factory for the current user's profile, keyed
 * `queryKeys.settings.profile()`. Read-only — there is no update mutation.
 */
export const settingsQueries = {
  profile: () =>
    queryOptions({
      queryKey: queryKeys.settings.profile(),
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1IdentityUsersMe());
      },
    }),
};
