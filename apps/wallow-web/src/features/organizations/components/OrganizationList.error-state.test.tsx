import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationList } from "./OrganizationList";

/**
 * Query error-state spec for the CANONICAL list page (Wallow-lrlm.4.2).
 *
 * `OrganizationList` reads `organizationsGetAllOptions()` but collapses `data ??
 * []` on every non-pending render, so a failed read is indistinguishable from a
 * genuinely empty tenant: the user is told "No organizations yet." when the API
 * actually answered 500. The fix is the branch
 * `features/inquiries/components/InquiryDetail.tsx` already ships — error only
 * when there is NO data to fall back on — with the sentence produced by
 * `errorText()` from `@shared/lib/error-text`, never a raw ProblemDetails cast.
 *
 * The two halves are tested separately because they are separate claims:
 *   1. errored with nothing cached  -> `organizations-error`, no empty state.
 *   2. errored WITH data cached (a failed background refetch) -> rows stay, and
 *      NO error banner appears. This is the half a bare `isError` check breaks.
 *
 * Error text comes off the wire, not a literal: `rejectJson` sends a real RFC
 * 7807 body, the SDK's error interceptor brands it, and `errorText` reads its
 * `detail` — the same path the app takes in production.
 */

/** An RFC 7807 body the SDK's error interceptor brands as a `WallowError`. */
const PROBLEM = { status: 500, title: "Internal Server Error", detail: "Organizations are down." };

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("OrganizationList — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when the list query errors with nothing cached", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<OrganizationList />, { harness });

    await expect
      .element(page.getByTestId("organizations-error"))
      .toHaveTextContent("Organizations are down.");
  });

  it("does not show the empty state when the list query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<OrganizationList />, { harness });

    // Awaiting the error first is what makes the negative assertion meaningful:
    // it pins the render to the settled error state rather than the pending one.
    await expect.element(page.getByTestId("organizations-error")).toBeInTheDocument();
    expect(page.getByTestId("organizations-empty-state").elements()).toHaveLength(0);
    expect(page.getByTestId("organization-item").elements()).toHaveLength(0);
  });

  it("keeps the loaded rows and shows no error when a background refetch fails", async () => {
    harness.resolveJson([{ id: "o1", name: "Acme", domain: null, memberCount: "3" }]);

    const { queryClient } = renderWithWallow(<OrganizationList />, { harness });
    await expect.element(page.getByTestId("organization-item").first()).toBeInTheDocument();

    harness.rejectJson(PROBLEM, 500);
    await queryClient.refetchQueries();

    // React Query retains the last resolved data across a failed refetch, so the
    // screen must NOT blank: `isError` alone would replace the list with a banner.
    await expect.element(page.getByTestId("organization-item").first()).toBeInTheDocument();
    expect(page.getByTestId("organizations-error").elements()).toHaveLength(0);
  });
});
