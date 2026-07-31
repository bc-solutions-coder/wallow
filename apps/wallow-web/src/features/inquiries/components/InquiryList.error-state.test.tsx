import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { InquiryList } from "./InquiryList";

/**
 * The inquiries list's query error state. A failed `inquiriesGetAllOptions()`
 * read must not fall through `data ?? []` and render "No inquiries yet." —
 * that reads as "nothing has arrived" rather than "we could not ask".
 */

/** An RFC 7807 body the SDK's error interceptor brands as a `WallowError`. */
const PROBLEM = {
  status: 500,
  title: "Internal Server Error",
  detail: "Could not load inquiries.",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("InquiryList — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when the inquiries query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<InquiryList />, { harness });

    await expect
      .element(page.getByTestId("inquiries-error"))
      .toHaveTextContent("Could not load inquiries.");
  });

  it("does not show the empty state when the inquiries query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<InquiryList />, { harness });

    await expect.element(page.getByTestId("inquiries-error")).toBeInTheDocument();
    expect(page.getByTestId("inquiries-empty-state").elements()).toHaveLength(0);
    expect(page.getByTestId("inquiry-item").elements()).toHaveLength(0);
  });
});
