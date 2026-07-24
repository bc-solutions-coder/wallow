/**
 * MFA feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM over the SDK
 * query layer (`@bc-solutions-coder/sdk/query`). Routes/components keep importing
 * from `./api`; the status-card key and its invalidation model (confirm/disable/
 * regenerate sweep it; `enrollTotp` does not) live in the SDK.
 */
export {
  mfaQueries,
  enrollTotpMutation,
  confirmEnrollMutation,
  disableMfaMutation,
  regenerateBackupCodesMutation,
  type ConfirmEnrollBody,
} from "@bc-solutions-coder/sdk/query";
