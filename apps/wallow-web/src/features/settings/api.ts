/**
 * Settings (Profile) feature query layer (Wallow-8w1h.6.1) — copies the
 * Organizations api.ts template shape (queryOptions factory over
 * getWallowSdk().<slice>).
 *
 * RECONCILIATION (scout CRITICAL DIVERGENCE #1): profile is READ-ONLY. The
 * bead DESIGN's `updateProfileMutation` maps to a generic key/value settings
 * PUT that is not a profile endpoint; there is NO backend
 * endpoint to mutate firstName/lastName/email from this surface. So this layer
 * exposes ONLY a `profile()` query (delegating to `settings.getProfile()`,
 * which wraps `getV1IdentityUsersMe` -> `CurrentUserResponse`) and NO mutation.
 */
import { queryOptions } from "@tanstack/react-query";

import { getWallowSdk } from "../../lib/wallow-sdk";

/**
 * queryOptions factory for the current user's profile, keyed
 * `['settings', 'profile']`. Read-only — there is no update mutation.
 */
export const settingsQueries = {
  profile: () =>
    queryOptions({
      queryKey: ["settings", "profile"] as const,
      queryFn: (): Promise<unknown> => getWallowSdk().settings.getProfile(),
    }),
};
