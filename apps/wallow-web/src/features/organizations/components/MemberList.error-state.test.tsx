import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { MemberList } from "./MemberList";

/**
 * Query error-state spec for the organization member list (Wallow-lrlm.4.2).
 *
 * `MemberList` renders `members={data ?? []}`, so a failed
 * `organizationsGetMembersOptions()` read shows "No members yet." — actively
 * misleading on a page whose whole job is membership. The error replaces the
 * TABLE only: the heading and the add-member form are not query-backed, so they
 * stay reachable and the user can still act.
 */

const ORG_ID = "o1";

/** An RFC 7807 body the SDK's error interceptor brands as a `WallowError`. */
const PROBLEM = { status: 500, title: "Internal Server Error", detail: "Members are unavailable." };

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("MemberList — query error state", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders the ProblemDetails detail when the members query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<MemberList orgId={ORG_ID} />, { harness });

    await expect
      .element(page.getByTestId("organization-members-error"))
      .toHaveTextContent("Members are unavailable.");
  });

  it("does not show the empty state when the members query errors", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<MemberList orgId={ORG_ID} />, { harness });

    await expect.element(page.getByTestId("organization-members-error")).toBeInTheDocument();
    expect(page.getByTestId("organization-members-empty").elements()).toHaveLength(0);
    expect(page.getByTestId("organization-detail-members-table").elements()).toHaveLength(0);
  });

  it("keeps the add-member form reachable while the members query is errored", async () => {
    harness.rejectJson(PROBLEM, 500);

    renderWithWallow(<MemberList orgId={ORG_ID} />, { harness });

    await expect.element(page.getByTestId("organization-members-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-member-add-form")).toBeInTheDocument();
  });
});
