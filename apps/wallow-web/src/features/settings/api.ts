/**
 * Settings (Profile) feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query
 * entry (`@bc-solutions-coder/sdk/query`). Profile is READ-ONLY (no mutation
 * endpoint), so the seam exposes only the current-user read. As of
 * Wallow-pu6a.5.5 that read is the GENERATED `usersGetCurrentUserOptions`, which
 * takes an explicit `{ client }` — the same operation the dashboard's auth guard
 * reads, so both resolve to ONE cache entry rather than two.
 */
export {
  queriesForOperation,
  usersGetCurrentUserOptions,
  usersGetCurrentUserQueryKey,
} from "@bc-solutions-coder/sdk/query";
