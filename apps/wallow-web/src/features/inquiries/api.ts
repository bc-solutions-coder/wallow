/**
 * Inquiries feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM over the
 * SDK query layer (`@bc-solutions-coder/sdk/query`). Routes/components keep
 * importing from `./api`; the query keys and invalidation behavior (create
 * sweeps the list, add comment sweeps that inquiry's comments, set status sweeps
 * its detail) live in the SDK.
 */
export {
  inquiriesQueries,
  createInquiryMutation,
  addCommentMutation,
  setStatusMutation,
  type SubmitInquiryBody,
  type AddCommentBody,
} from "@bc-solutions-coder/sdk/query";
