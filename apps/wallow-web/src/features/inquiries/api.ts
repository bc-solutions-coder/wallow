/**
 * Inquiries feature `api.ts` — a THIN RE-EXPORT SEAM over the SDK query entry
 * (`@bc-solutions-coder/sdk/query`). Routes/components keep importing from
 * `./api`; as of Wallow-pu6a.5.5 everything behind it is GENERATED from the
 * OpenAPI document. The invalidation model the hand-written slice encoded
 * (create sweeps the list, add-comment sweeps that inquiry's comments, set-status
 * sweeps its detail) now lives at the call sites, expressed through the curated
 * predicates re-exported here — generated keys are flat and have no prefix a
 * mutation could sweep by.
 */
export {
  inquiriesAddCommentMutation,
  inquiriesGetAllOptions,
  inquiriesGetAllQueryKey,
  inquiriesGetByIdOptions,
  inquiriesGetByIdQueryKey,
  inquiriesGetCommentsOptions,
  inquiriesGetCommentsQueryKey,
  inquiriesSubmitMutation,
  inquiriesUpdateStatusMutation,
  queriesForOperation,
  queriesWithTag,
} from "@bc-solutions-coder/sdk/query";
