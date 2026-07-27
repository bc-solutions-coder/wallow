import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { chooseOption, expectCatalogSelect } from "../../../test/catalog-select";
import { installSdkClientMock } from "../../../test/sdk-client-mock";
import { byTestId, waitForTestId } from "../../../test/style-contract";
import { OrganizationDetail } from "./OrganizationDetail";

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

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

/** Render the detail page with its org, members, and clients already in cache. */
async function renderDetail(): Promise<void> {
  const client = newClient();
  client.setQueryData(["orgs", "o1"], org);
  client.setQueryData(["orgs", "o1", "members"], []);
  client.setQueryData(["orgs", "o1", "clients"], []);

  renderWithClient(client, <OrganizationDetail orgId="o1" />);
  await waitForTestId("organization-detail-register-form");
}

beforeEach(() => {
  const sdk = installSdkClientMock();
  sdk.resolveJson([]);
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
