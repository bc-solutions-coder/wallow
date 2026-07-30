import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";

import { routeHarness } from "@shared/testing/harness-routes";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { chooseOption, expectCatalogSelect } from "@shared/testing/catalog-select";
import { byTestId, waitForTestId } from "@shared/testing/style-contract";
import { OrganizationDetail } from "./OrganizationDetail";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Catalog-migration spec for the org-detail register-client form
 * (Wallow-m5aq.5.3). Its one hand-rolled primitive is the public/confidential
 * client-type `<select>`, which becomes the catalog `Select`; the testid
 * `organization-detail-register-client-type` is preserved and now names the
 * trigger.
 *
 * The register-client form's own behaviour (the submitted body, the one-time
 * secret reveal) stays pinned by `OrganizationDetail.clients.test.tsx`.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** Render the detail page with its org, members, and clients already in cache. */
async function renderDetail(): Promise<void> {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/clients/by-tenant/o1": [],
    },
    { fallback: [] },
  );

  renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
  await waitForTestId("organization-detail-register-form");
}

beforeEach(() => {
  harness = createSdkHarness();
});

describe("OrganizationDetail client type (catalog Select)", () => {
  it("presents the client-type control as a combobox rather than a native select", async () => {
    await renderDetail();

    expectCatalogSelect("organization-detail-register-client-type");
  });

  it("offers both client types as named options", async () => {
    await renderDetail();

    await userEvent.click(byTestId("organization-detail-register-client-type"));

    await expect.element(page.getByRole("option", { name: "Public", exact: true })).toBeVisible();
    await expect
      .element(page.getByRole("option", { name: "Confidential", exact: true }))
      .toBeVisible();
  });

  it("reports the chosen client type on the trigger", async () => {
    await renderDetail();

    await chooseOption("organization-detail-register-client-type", "Confidential");

    await expect
      .element(page.getByTestId("organization-detail-register-client-type"))
      .toHaveTextContent("Confidential");
  });
});
