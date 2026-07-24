/**
 * Organizations feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM over
 * the SDK query layer (`@bc-solutions-coder/sdk/query`), which now owns the
 * canonical organizations query/mutation factories (ported from this file in
 * Feature 1). Routes/components still import from `./api`, so the
 * "api.ts is the only data import" convention survives as a seam while the
 * query keys and invalidation behavior live in one place in the SDK.
 */
export {
  organizationsQueries,
  createOrganizationMutation,
  addMemberMutation,
  removeMemberMutation,
  archiveOrganizationMutation,
  reactivateOrganizationMutation,
  registerClientMutation,
  type CreateOrganizationBody,
  type AddMemberBody,
  type RegisterClientBody,
} from "@bc-solutions-coder/sdk/query";
