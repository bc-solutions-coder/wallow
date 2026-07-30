import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Org-detail bound-clients + register-client reachability spec
 * (Wallow-ffpq.3.6). Once an org loads, the detail page must surface the
 * org's bound clients (`organization-detail-clients-table`) and a
 * register-client form (display-name / client-type / redirect-uris / submit),
 * reachable straight from the org detail page.
 *
 * Data flows through the generated query options, so the network seam is the
 * `fetch` of the request-scoped client this spec's own `createSdkHarness()`
 * builds (Wallow-pu6a.5.5 — there is no shared module-global client left to
 * install a mock onto). The org / members / clients state is driven from the
 * transport rather than the cache: those three requests are in flight together,
 * so `routeHarness` answers each by URL rather than in call order.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function seedLoadedOrg(): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/clients/by-tenant/o1": [],
    },
    { fallback: [] },
  );
}

describe("OrganizationDetail bound clients + register-client", () => {
  // Installed so the seeded queries resolve through the mocked client rather
  // than the real network if any refetch fires.

  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders the bound-clients table once the org loads", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-detail-clients-table")).toBeInTheDocument();
  });

  it("renders the register-client form fields reachable from the org detail page", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-display-name"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-client-type"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-submit"))
      .toBeInTheDocument();
  });
});
