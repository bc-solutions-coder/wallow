/**
 * The inquiry status domain (rehomed from the deleted `types.ts` by
 * Wallow-pu6a.5.5).
 *
 * Everything else that file held was a view-model mirroring a generated DTO, and
 * those are gone: under `responseStyle: "data"` a generated read already resolves
 * `InquiryResponse`/`InquiryCommentResponse`, so there is nothing left to narrow
 * at the render boundary. This is the one part the OpenAPI document cannot
 * express — `InquiryResponse.status` is a bare `string` there — so the union and
 * its ordered list stay hand-written, mirroring the backend enum names emitted by
 * `JsonStringEnumConverter`:
 * `api/src/Modules/Inquiries/Wallow.Inquiries.Domain/Enums/InquiryStatus.cs`
 * (New -> Reviewed -> Contacted -> Closed — sequential transitions only).
 */
export type InquiryStatus = "New" | "Reviewed" | "Contacted" | "Closed";

/** Ordered status list for status-change controls (sequential transition order). */
export const INQUIRY_STATUSES: readonly InquiryStatus[] = [
  "New",
  "Reviewed",
  "Contacted",
  "Closed",
];
