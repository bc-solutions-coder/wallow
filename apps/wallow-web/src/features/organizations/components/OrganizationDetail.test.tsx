import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The org-detail page body: loaded and not-found states, the mounted member
 * list, and the archive/reactivate actions.
 *
 * Runs the real SDK over a faked fetch (`createSdkHarness`) mounted on the
 * router context. The page fires several reads at once, so `routeHarness`
 * answers each by URL rather than in call order.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function seedActiveOrg() {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
    },
    { fallback: [] },
  );
}

describe("OrganizationDetail", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    // Unseeded queries (bound clients, post-mutation member refetches) resolve
    // to an empty array so no list render sees a non-array body.
    harness.resolveJson([]);
  });

  it("renders the org heading and a back link when the org loads", async () => {
    seedActiveOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toHaveTextContent("Acme");

    const backLink = page.getByTestId("organization-detail-back-link");
    await expect.element(backLink).toHaveTextContent("Back to organizations");
    await expect.element(backLink).toHaveAttribute("href", "/dashboard/organizations");

    await expect
      .element(page.getByTestId("organization-detail-clients-heading"))
      .toHaveTextContent("Bound Clients");
    await expect
      .element(page.getByTestId("organization-detail-register-submit"))
      .toHaveTextContent("Register client");
  });

  it("renders the not-found state when the org is missing", async () => {
    harness.resolveJson(null);

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    const notFound = page.getByTestId("organization-detail-not-found");
    await expect.element(notFound).toHaveTextContent("Organization not found.");
    await expect
      .element(notFound)
      .toHaveTextContent(
        "It may have been archived, or the link may point somewhere that no longer exists.",
      );
    await expect.element(page.getByTestId("organization-detail-heading")).not.toBeInTheDocument();
  });

  it("mounts the member list (members table) for the org", async () => {
    routeHarness(
      harness,
      {
        "GET /v1/identity/organizations/o1": org,
        "GET /v1/identity/organizations/o1/members": [
          {
            id: "u1",
            email: "ada@acme.io",
            firstName: "Ada",
            lastName: "L",
            enabled: true,
            roles: ["Owner"],
          },
        ],
      },
      { fallback: [] },
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-members-table")).toBeInTheDocument();
  });

  it("archives the org: POSTs to the org's archive endpoint", async () => {
    seedActiveOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await userEvent.click(page.getByTestId("organization-detail-archive"));

    await vi.waitFor(() => {
      const archiveCall = harness.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/archive",
      );
      expect(archiveCall).toBeDefined();
    });
  });

  it("reactivates the org: POSTs to the org's reactivate endpoint", async () => {
    seedActiveOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await userEvent.click(page.getByTestId("organization-detail-reactivate"));

    await vi.waitFor(() => {
      const reactivateCall = harness.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/reactivate",
      );
      expect(reactivateCall).toBeDefined();
    });
  });
});
