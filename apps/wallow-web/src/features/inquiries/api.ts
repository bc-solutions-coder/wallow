/**
 * Inquiries feature query layer (Wallow-8w1h.7.1) — copies the CANONICAL
 * Organizations `api.ts` shape. `api.ts` is the ONLY layer route/component files
 * import for data: it exposes TanStack Query `queryOptions` factories and
 * mutation factories, all delegating to `getWallowSdk().inquiries` (never
 * importing generated SDK ops directly; that is the facade's job).
 *
 */
import { queryOptions, type QueryClient } from "@tanstack/react-query";

import { getWallowSdk } from "../../lib/wallow-sdk";

import type { AddCommentBody, SubmitInquiryBody } from "./types";

/**
 * queryOptions factories for the inquiries list, a single inquiry's detail, and
 * an inquiry's comment thread. `list()` is keyed `['inquiries']`; `detail(id)` is
 * keyed `['inquiries', id]`; `comments(id)` is keyed `['inquiries', id, 'comments']`.
 */
export const inquiriesQueries = {
  list: () =>
    queryOptions({
      queryKey: ["inquiries"] as const,
      queryFn: () => getWallowSdk().inquiries.list(),
    }),
  detail: (id: string) =>
    queryOptions({
      queryKey: ["inquiries", id] as const,
      queryFn: () => getWallowSdk().inquiries.get(id),
    }),
  comments: (id: string) =>
    queryOptions({
      queryKey: ["inquiries", id, "comments"] as const,
      queryFn: () => getWallowSdk().inquiries.comments(id),
    }),
};

/**
 * Mutation factory for submitting an inquiry. Takes the router/context
 * `QueryClient` so its `onSuccess` invalidates the `['inquiries']` list query.
 */
export const createInquiryMutation = (queryClient: QueryClient) => ({
  mutationFn: (body: SubmitInquiryBody): Promise<unknown> => getWallowSdk().inquiries.create(body),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["inquiries"] });
  },
});

/**
 * Add-comment mutation factory. Closes over the target inquiry `id`; on success
 * invalidates that inquiry's comments query (`['inquiries', id, 'comments']`).
 * The 201 response body is untyped, so it is ignored — the invalidation refetches.
 */
export const addCommentMutation = (queryClient: QueryClient, id: string) => ({
  mutationFn: (body: AddCommentBody): Promise<unknown> =>
    getWallowSdk().inquiries.addComment(id, body),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["inquiries", id, "comments"] });
  },
});

/**
 * Status-change mutation factory. Closes over the target inquiry `id`; on success
 * invalidates that inquiry's detail query (`['inquiries', id]`).
 */
export const setStatusMutation = (queryClient: QueryClient, id: string) => ({
  mutationFn: (newStatus: string): Promise<unknown> =>
    getWallowSdk().inquiries.setStatus(id, newStatus),
  onSuccess: (): void => {
    void queryClient.invalidateQueries({ queryKey: ["inquiries", id] });
  },
});
