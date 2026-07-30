import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { InquiryDetail } from "./InquiryDetail";

/**
 * Query error-state spec for the inquiry detail page's NESTED comment thread
 * (Wallow-lrlm.4.2).
 *
 * `InquiryDetail`'s top-level detail query is the template the rest of this task
 * copies and is covered by `InquiryDetail.test.tsx`. Its inner `CommentThread`
 * reads a SECOND query (`inquiriesGetCommentsOptions`) that had no error
 * handling at all: a failed comments read fell through `data ?? []` and rendered
 * "No comments yet." — telling the user the thread is empty when the server
 * merely fell over. This spec pins the same split the other surfaces got, and
 * keeps a genuinely empty thread rendering the empty state so the fix is a split
 * rather than a replacement.
 *
 * Same shape as `OrganizationDetail.error-state.test.tsx`'s `ClientsSection`
 * describe: only the nested read fails, everything else resolves, so the page
 * renders in full and the section is the only surface that speaks up.
 */

const INQUIRY_ID = "i1";

const INQUIRY = {
  id: INQUIRY_ID,
  name: "Ada Lovelace",
  email: "ada@example.com",
  company: "Analytical Engines",
  projectType: "web-app",
  status: "New",
  createdAt: "2026-07-15T00:00:00Z",
};

/** RFC 7807 body the SDK's error interceptor brands as a `WallowError`. */
const COMMENTS_PROBLEM = {
  status: 500,
  title: "Internal Server Error",
  detail: "Comments failed.",
};

function json(body: unknown, status = 200): Response {
  return Response.json(body, { status });
}

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("InquiryDetail — comment thread query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when only the comments query errors", async () => {
    harness.respond((call) =>
      call.path.endsWith("/comments") ? json(COMMENTS_PROBLEM, 500) : json(INQUIRY),
    );

    renderWithWallow(<InquiryDetail inquiryId={INQUIRY_ID} />, { harness });

    await expect.element(page.getByTestId("inquiry-detail-heading")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("inquiry-comments-error"))
      .toHaveTextContent("Comments failed.");
  });

  it("does not render the empty thread when the comments query errors", async () => {
    // The bug this pins: today a failed comments read collapses to `[]` and
    // claims the inquiry has no comments.
    harness.respond((call) =>
      call.path.endsWith("/comments") ? json(COMMENTS_PROBLEM, 500) : json(INQUIRY),
    );

    renderWithWallow(<InquiryDetail inquiryId={INQUIRY_ID} />, { harness });

    await expect.element(page.getByTestId("inquiry-comments-error")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-comments-empty").elements()).toHaveLength(0);
    expect(page.getByTestId("inquiry-comments-table").elements()).toHaveLength(0);
  });

  it("still renders the empty thread when the comments query resolves empty", async () => {
    // The other half of the split — a REGRESSION guard, not a new behaviour:
    // adding the error branch must not swallow a genuinely empty thread.
    harness.respond((call) => (call.path.endsWith("/comments") ? json([]) : json(INQUIRY)));

    renderWithWallow(<InquiryDetail inquiryId={INQUIRY_ID} />, { harness });

    await expect.element(page.getByTestId("inquiry-comments-empty")).toBeInTheDocument();
    expect(page.getByTestId("inquiry-comments-error").elements()).toHaveLength(0);
  });
});
