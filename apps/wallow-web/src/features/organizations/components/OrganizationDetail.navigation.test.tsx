import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { byTestId, expectTag, waitForTestId } from "@bc-solutions-coder/testing/locators";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The org-detail page's outgoing links to its two `$orgId`-scoped screens:
 * pending requests and member roles. Neither reaches the global rail (it
 * cannot supply an `orgId`), so this page is their only way in.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

let harness: SdkHarness;

async function renderDetail() {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
    },
    { fallback: [] },
  );
  const { router } = renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
  await waitForTestId("organization-detail-heading");
  return router;
}

describe("OrganizationDetail outgoing navigation", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("points the requests link at this org's pending-requests screen", async () => {
    await renderDetail();

    const link = byTestId("organization-detail-requests-link");
    expectTag(link, "a");
    expect(link.getAttribute("href")).toBe("/dashboard/organizations/o1/requests");
  });

  it("points the members link at this org's member-roles screen", async () => {
    await renderDetail();

    const link = byTestId("organization-detail-members-link");
    expectTag(link, "a");
    expect(link.getAttribute("href")).toBe("/dashboard/organizations/o1/members");
  });

  it("navigates through the router when the requests link is clicked", async () => {
    const router = await renderDetail();

    await userEvent.click(byTestId("organization-detail-requests-link"));

    await expect
      .poll(() => router.state.location.pathname)
      .toBe("/dashboard/organizations/o1/requests");
  });

  it("navigates through the router when the members link is clicked", async () => {
    const router = await renderDetail();

    await userEvent.click(byTestId("organization-detail-members-link"));

    await expect
      .poll(() => router.state.location.pathname)
      .toBe("/dashboard/organizations/o1/members");
  });
});
