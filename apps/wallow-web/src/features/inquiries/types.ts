/**
 * Inquiries feature view types (Wallow-8w1h.7.1).
 *
 * The generated `InquiryResponse.status` is a bare `string` (no enum type in the
 * OpenAPI spec), so this local `InquiryStatus` union is the narrowing boundary
 * the components use instead of leaking `string`/`any`. The values mirror the
 * backend enum names emitted by `JsonStringEnumConverter`:
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

/**
 * List/detail view-model for an inquiry (mirrors the generated `InquiryResponse`
 * Dto). The list page (Wallow-8w1h.7.2) narrows the facade's `unknown` list to
 * `Inquiry[]` at the render boundary — the sanctioned pattern the Organizations
 * `Organization` view-model established. `status` is narrowed to the local
 * `InquiryStatus` union rather than leaking the Dto's bare `string`.
 */
export interface Inquiry {
  id: string;
  name: string;
  email: string;
  company: string | null;
  projectType: string;
  status: InquiryStatus;
  createdAt: string;
}

/**
 * Submit-inquiry request body (mirrors the SDK `SubmitInquiryRequest` /
 * Wallow.Web `InquiryModel`). Kept local so components/forms depend on the
 * feature, not the generated SDK types directly.
 */
export interface SubmitInquiryBody {
  name: string;
  email: string;
  phone: string;
  company: string | null;
  projectType: string;
  budgetRange: string;
  timeline: string;
  message: string;
}

/** Add-comment request body (mirrors the SDK `AddInquiryCommentRequest`). */
export interface AddCommentBody {
  content: string;
  isInternal: boolean;
}

/**
 * Comment-thread view-model for an inquiry (mirrors the generated
 * `InquiryCommentResponse` Dto). The detail page (Wallow-8w1h.7.4) narrows the
 * facade's `unknown` comments payload to `InquiryComment[]` at the render
 * boundary — the sanctioned pattern the Organizations `OrganizationMember`
 * view-model established.
 */
export interface InquiryComment {
  id: string;
  inquiryId: string;
  authorId: string;
  authorName: string;
  content: string;
  isInternal: boolean;
  createdAt: string;
}
