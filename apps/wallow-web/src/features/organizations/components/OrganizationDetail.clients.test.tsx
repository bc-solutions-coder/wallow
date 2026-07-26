import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Org-detail bound-clients + register-client reachability spec
 * (Wallow-ffpq.3.6). Once an org loads, the detail page must surface the
 * org's bound clients (`organization-detail-clients-table`) and a
 * register-client form (display-name / client-type / redirect-uris / submit),
 * reachable straight from the org detail page.
 *
 * Data flows through the SDK query layer, so the network seam is the shared SDK
 * client's `fetch`, overridden via `installSdkClientMock` (Wallow-evd5.2.6 — the
 * retired `getWallowSdk()` facade is no longer in the path). The org / members /
 * clients state is seeded into the cache (`staleTime: Infinity` keeps the seed
 * from refetching).
 */

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

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

function seedLoadedOrg(client: QueryClient): void {
  client.setQueryData(["orgs", "o1"], org);
  client.setQueryData(["orgs", "o1", "members"], []);
  client.setQueryData(["orgs", "o1", "clients"], []);
}

describe("OrganizationDetail bound clients + register-client", () => {
  // Installed so the seeded queries resolve through the mocked client rather
  // than the real network if any refetch fires.
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
    sdk.resolveJson([]);
  });

  it("renders the bound-clients table once the org loads", async () => {
    const client = newClient();
    seedLoadedOrg(client);

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

    await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-detail-clients-table")).toBeInTheDocument();
  });

  it("renders the register-client form fields reachable from the org detail page", async () => {
    const client = newClient();
    seedLoadedOrg(client);

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

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
