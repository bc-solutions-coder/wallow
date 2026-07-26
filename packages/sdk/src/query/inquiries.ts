/**
 * Inquiries query module.
 *
 * Ported from apps/wallow-web/src/features/inquiries/api.ts (op-to-call mapping at
 * apps/wallow-web/src/lib/wallow-sdk.ts:288-297) with the canonical template's
 * three changes: (a) every queryKey comes from `queryKeys.inquiries.*`; (b) every
 * queryFn/mutationFn starts with `ensureQueryBootstrapped()` then calls the
 * generated op directly via `unwrap(...)`; (c) the request-body interfaces live
 * and are exported here.
 *
 * SDK-ACCURATE MAPPING: `list()` wraps `getV1InquiriesSubmitted()` (the caller's
 * OWN inquiries, not the admin all-view); `setStatus()` sends `{ newStatus }`
 * (the field is `newStatus`, not `status`). `addComment`'s 201 body is untyped,
 * so it is ignored — the invalidation refetches the thread.
 */
import { queryOptions, type QueryClient } from "@tanstack/react-query";

import { unwrap } from "../facade";
import {
  getV1InquiriesById,
  getV1InquiriesByIdComments,
  getV1InquiriesSubmitted,
  patchV1InquiriesByIdStatus,
  postV1Inquiries,
  postV1InquiriesByIdComments,
} from "../generated";
import { ensureQueryBootstrapped } from "./bootstrap";
import { queryKeys } from "./keys";

/**
 * queryOptions factories for the inquiries list, a single inquiry's detail, and
 * an inquiry's comment thread. `list()` is keyed `queryKeys.inquiries.all`;
 * `detail(id)` is keyed `queryKeys.inquiries.detail(id)`; `comments(id)` is keyed
 * `queryKeys.inquiries.comments(id)`.
 */
export const inquiriesQueries = {
  list: () =>
    queryOptions({
      queryKey: queryKeys.inquiries.all,
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1InquiriesSubmitted());
      },
    }),
  detail: (id: string) =>
    queryOptions({
      queryKey: queryKeys.inquiries.detail(id),
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1InquiriesById({ path: { id } }));
      },
    }),
  comments: (id: string) =>
    queryOptions({
      queryKey: queryKeys.inquiries.comments(id),
      queryFn: () => {
        ensureQueryBootstrapped();
        return unwrap(getV1InquiriesByIdComments({ path: { id } }));
      },
    }),
};

/**
 * Submit-inquiry request body (mirrors the API `SubmitInquiryRequest`). Kept here
 * so callers depend on the query module, not the generated SDK types directly.
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

/** Add-comment request body (mirrors the API `AddInquiryCommentRequest`). */
export interface AddCommentBody {
  content: string;
  isInternal: boolean;
}

/**
 * Mutation factory for submitting an inquiry. Takes the router/context
 * `QueryClient` so its `onSuccess` invalidates the inquiries list query.
 */
export const createInquiryMutation = (queryClient: QueryClient) => ({
  mutationFn: (body: SubmitInquiryBody): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(postV1Inquiries({ body }));
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.inquiries.all });
  },
});

/**
 * Add-comment mutation factory. Closes over the target inquiry `id`; on success
 * invalidates that inquiry's comments query. The 201 response body is untyped, so
 * it is ignored — the invalidation refetches.
 */
export const addCommentMutation = (queryClient: QueryClient, id: string) => ({
  mutationFn: (body: AddCommentBody): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(postV1InquiriesByIdComments({ path: { id }, body }));
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.inquiries.comments(id) });
  },
});

/**
 * Status-change mutation factory. Closes over the target inquiry `id`; on success
 * invalidates that inquiry's detail query. The body field is `newStatus`.
 */
export const setStatusMutation = (queryClient: QueryClient, id: string) => ({
  mutationFn: (newStatus: string): Promise<unknown> => {
    ensureQueryBootstrapped();
    return unwrap(patchV1InquiriesByIdStatus({ path: { id }, body: { newStatus } }));
  },
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.inquiries.detail(id) });
  },
});
