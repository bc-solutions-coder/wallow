import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Component spec for the org-detail page body (Wallow-8w1h.4.4). Data flows
 * through the generated `organizationsGetByIdOptions()` +
 * `organizationsArchiveMutation`/`organizationsReactivateMutation`, so the
 * network seam is the `fetch` of the request-scoped client this spec's own
 * `createSdkHarness()` builds (Wallow-pu6a.5.5 — there is no shared
 * module-global client left to install a mock onto). Detail state is driven
 * from the transport, not the cache: the page fires several requests at once,
 * so `routeHarness` answers each by URL rather than in call order.
 * Archive/reactivate are asserted via the recorded outgoing request
 * (`harness.calls`).
 *
 * Testids: `organization-detail-back-link`,
 * `organization-detail-heading`, `organization-detail-not-found`, plus the new
 * lifecycle actions `organization-detail-archive` / `organization-detail-
 * reactivate`. It also mounts `MemberList`, surfacing
 * `organization-detail-members-table`.
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
    // Unseeded queries (the bound-clients list, member refetches after a
    // lifecycle mutation) resolve to an empty array so their list renders never
    // see a non-array body.
    harness.resolveJson([]);
  });

  it("renders the org heading and a back link when the org loads", async () => {
    seedActiveOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toHaveTextContent("Acme");
    await expect.element(page.getByTestId("organization-detail-back-link")).toBeInTheDocument();
  });

  it("renders the not-found state when the org is missing", async () => {
    harness.resolveJson(null);

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-not-found")).toBeInTheDocument();
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
