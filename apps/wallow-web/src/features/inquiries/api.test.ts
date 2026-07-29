import { describe, expect, it } from "vitest";

/**
 * Inquiries feature `api.ts` — a THIN RE-EXPORT SEAM over
 * `@bc-solutions-coder/sdk/query`. Everything behind it is GENERATED as of
 * Wallow-pu6a.5.5: the mutations no longer carry an `onSuccess`, so the
 * invalidation model (create sweeps the list, add-comment sweeps that inquiry's
 * comments, set-status sweeps its detail) now lives at the call sites, where the
 * component specs assert it against the rendered screen.
 *
 * What is still this seam's own decision — and what this spec pins — is which
 * curated predicate reaches which queries. The per-inquiry sweeps go through
 * `queriesForOperation`, NOT the `Inquiries` tag: the tag spans list, detail and
 * comments together, so using it would refetch the whole feature after every
 * comment. Both the reach and that narrowness are asserted here.
 */

import * as query from "@bc-solutions-coder/sdk/query";

import { sweeps } from "../../test/invalidation";
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
