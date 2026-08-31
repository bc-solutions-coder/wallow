/**
 * Settings feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`). The profile read is the GENERATED
 * `usersGetCurrentUserOptions`, which takes an explicit `{ client }` — the same
 * operation the dashboard's auth guard reads, so both resolve to ONE cache
 * entry rather than two. The connected-applications trio backs the consent
 * ledger card: the list read, its key (for the post-withdraw sweep), and the
 * withdraw mutation.
 */
export {
  meAuthorizationsListConnectedApplicationsOptions,
  meAuthorizationsListConnectedApplicationsQueryKey,
  meAuthorizationsWithdrawConsentMutation,
  queriesForOperation,
  usersGetCurrentUserOptions,
  usersGetCurrentUserQueryKey,
} from "@bc-solutions-coder/sdk/query";
