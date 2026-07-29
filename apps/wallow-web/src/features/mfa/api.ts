/**
 * MFA feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`). Routes/components keep importing from
 * `./api`; as of Wallow-pu6a.5.5 everything behind it is GENERATED from the
 * OpenAPI document. The status card's invalidation model is unchanged in
 * substance — confirm/disable/regenerate sweep the status query, `enrollTotp`
 * does not, because enrolling only mints a secret — but it is now expressed at
 * the call site through `queriesForOperation(mfaGetStatusQueryKey(...))`.
 */
export {
  mfaConfirmEnrollmentMutation,
  mfaDisableMutation,
  mfaEnrollTotpMutation,
  mfaGetStatusOptions,
  mfaGetStatusQueryKey,
  mfaRegenerateBackupCodesMutation,
  queriesForOperation,
  queriesWithTag,
} from "@bc-solutions-coder/sdk/query";
