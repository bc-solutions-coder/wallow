import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

/**
 * Inquiries feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. This spec pins the seam (re-export identity)
 * and the preserved key/invalidation behavior: create sweeps the list, add
 * comment sweeps that inquiry's comments, set status sweeps that inquiry's
 * detail. The old `vi.mock("../../lib/wallow-sdk")` delegation spec is gone.
 */

import * as api from "./api";
import { inquiriesQueries } from "./api";
import { queryKeys } from "@bc-solutions-coder/sdk/query";
import * as query from "@bc-solutions-coder/sdk/query";

/** A QueryClient whose invalidateQueries records the keys it was asked to sweep. */
function captureInvalidations(): { client: QueryClient; keys: unknown[] } {
  const client = new QueryClient();
  const keys: unknown[] = [];
  client.invalidateQueries = (filters?: { queryKey?: unknown }) => {
    keys.push(filters?.queryKey);
    return Promise.resolve();
  };
  return { client, keys };
}

describe("api.ts re-exports the SDK inquiries query layer", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.inquiriesQueries).toBe(query.inquiriesQueries);
    expect(api.createInquiryMutation).toBe(query.createInquiryMutation);
    expect(api.addCommentMutation).toBe(query.addCommentMutation);
    expect(api.setStatusMutation).toBe(query.setStatusMutation);
  });
});

describe("inquiriesQueries", () => {
  it("keys every option from the central queryKeys factory", () => {
    expect(inquiriesQueries.list().queryKey).toEqual(queryKeys.inquiries.all);
    expect(inquiriesQueries.detail("i1").queryKey).toEqual(queryKeys.inquiries.detail("i1"));
    expect(inquiriesQueries.comments("i1").queryKey).toEqual(queryKeys.inquiries.comments("i1"));
  });

  it("keeps the list queryKey stable across calls", () => {
    expect(inquiriesQueries.list().queryKey).toEqual(inquiriesQueries.list().queryKey);
  });
});

describe("inquiries mutation invalidation", () => {
  it("createInquiryMutation sweeps the inquiries list", () => {
    const { client, keys } = captureInvalidations();
    api.createInquiryMutation(client).onSuccess();
    expect(keys).toEqual([queryKeys.inquiries.all]);
  });

  it("addCommentMutation sweeps that inquiry's comments", () => {
    const { client, keys } = captureInvalidations();
    api.addCommentMutation(client, "i1").onSuccess();
    expect(keys).toEqual([queryKeys.inquiries.comments("i1")]);
  });

  it("setStatusMutation sweeps that inquiry's detail", () => {
    const { client, keys } = captureInvalidations();
    api.setStatusMutation(client, "i1").onSuccess();
    expect(keys).toEqual([queryKeys.inquiries.detail("i1")]);
  });
});
