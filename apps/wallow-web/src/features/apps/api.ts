/**
 * Apps feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM over the SDK
 * query layer (`@bc-solutions-coder/sdk/query`). Routes/components keep importing
 * from `./api`; the query keys and invalidation behavior live in the SDK. The
 * adoption also surfaces `upsertBrandingMutation`, which the hand-rolled layer
 * lacked.
 */
export {
  appsQueries,
  registerAppMutation,
  upsertBrandingMutation,
  type RegisterAppBody,
  type UpsertBrandingBody,
} from "@bc-solutions-coder/sdk/query";
