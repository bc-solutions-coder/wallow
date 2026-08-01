import { describe, expect, it } from "vitest";

/**
 * The inquiries `api.ts` seam: a re-export of the generated
 * `@bc-solutions-coder/sdk/query` surface, plus which curated
 * predicate reaches which queries.
 *
 * The per-inquiry sweeps go through `queriesForOperation`, NOT the
 * `Inquiries` tag — the tag spans list, detail and comments together,
 * so using it would refetch the whole feature after every comment.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import { sweeps } from "@bc-solutions-coder/testing/invalidation";
import * as api from "./api";

const commentsKey: readonly unknown[] = api.inquiriesGetCommentsQueryKey({ path: { id: "i1" } });
const detailKey: readonly unknown[] = api.inquiriesGetByIdQueryKey({ path: { id: "i1" } });

describe("api.ts re-exports the SDK inquiries query surface", () => {
  it("re-exports each symbol by identity from @bc-solutions-coder/sdk/query", () => {
    expect(api.inquiriesGetAllOptions).toBe(query.inquiriesGetAllOptions);
    expect(api.inquiriesGetAllQueryKey).toBe(query.inquiriesGetAllQueryKey);
    expect(api.inquiriesGetByIdOptions).toBe(query.inquiriesGetByIdOptions);
    expect(api.inquiriesGetByIdQueryKey).toBe(query.inquiriesGetByIdQueryKey);
    expect(api.inquiriesGetCommentsOptions).toBe(query.inquiriesGetCommentsOptions);
    expect(api.inquiriesGetCommentsQueryKey).toBe(query.inquiriesGetCommentsQueryKey);
    expect(api.inquiriesSubmitMutation).toBe(query.inquiriesSubmitMutation);
    expect(api.inquiriesAddCommentMutation).toBe(query.inquiriesAddCommentMutation);
    expect(api.inquiriesUpdateStatusMutation).toBe(query.inquiriesUpdateStatusMutation);
    expect(api.queriesForOperation).toBe(query.queriesForOperation);
    expect(api.queriesWithTag).toBe(query.queriesWithTag);
  });
});

describe("inquiries invalidation", () => {
  it("sweeps the inquiry list by tag", () => {
    expect(sweeps(api.queriesWithTag("Inquiries"), api.inquiriesGetAllQueryKey())).toBe(true);
  });

  it("leaves another feature's queries alone", () => {
    expect(sweeps(api.queriesWithTag("Inquiries"), query.appsGetUserAppsQueryKey())).toBe(false);
  });

  it("reaches an inquiry's comments by operation", () => {
    expect(sweeps(api.queriesForOperation(commentsKey), commentsKey)).toBe(true);
  });

  it("does not drag the detail query along when only the comments were swept", () => {
    expect(sweeps(api.queriesForOperation(commentsKey), detailKey)).toBe(false);
  });
});
