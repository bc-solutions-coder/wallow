import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import {
  addCommentMutation,
  createInquiryMutation,
  inquiriesQueries,
  setStatusMutation,
} from "./inquiries";
import { queryKeys } from "./keys";

describe("inquiriesQueries", () => {
  it("keys every option from the central factory", () => {
    expect(inquiriesQueries.list().queryKey).toEqual(queryKeys.inquiries.all);
    expect(inquiriesQueries.detail("i1").queryKey).toEqual(queryKeys.inquiries.detail("i1"));
    expect(inquiriesQueries.comments("i1").queryKey).toEqual(queryKeys.inquiries.comments("i1"));
  });

  it("invalidates the factory keys on mutation success", async () => {
    const queryClient = new QueryClient();
    const invalidated: unknown[] = [];
    queryClient.invalidateQueries = (filters?: { queryKey?: unknown }) => {
      invalidated.push(filters?.queryKey);
      return Promise.resolve();
    };
    createInquiryMutation(queryClient).onSuccess();
    addCommentMutation(queryClient, "i1").onSuccess();
    setStatusMutation(queryClient, "i1").onSuccess();
    expect(invalidated).toEqual([
      queryKeys.inquiries.all,
      queryKeys.inquiries.comments("i1"),
      queryKeys.inquiries.detail("i1"),
    ]);
  });
});
