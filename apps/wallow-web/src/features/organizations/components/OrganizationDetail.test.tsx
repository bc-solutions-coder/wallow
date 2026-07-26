import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Component spec for the org-detail page body (Wallow-8w1h.4.4). Data flows
 * through the SDK query layer (`organizationsQueries.detail()` +
 * `archiveOrganizationMutation`/`reactivateOrganizationMutation`), so the network
 * seam is the shared SDK client's `fetch`, overridden per test via
 * `installSdkClientMock` (Wallow-evd5.2.6 — the retired `getWallowSdk()` facade
 * is no longer in the path). Detail state is driven by the `['orgs', id]` cache
 * (`staleTime: Infinity` keeps the seed from refetching); archive/reactivate are
 * asserted via the recorded outgoing request (`sdk.calls`).
 *
 * Testids: `organization-detail-back-link`,
 * `organization-detail-heading`, `organization-detail-not-found`, plus the new
 * lifecycle actions `organization-detail-archive` / `organization-detail-
 * reactivate`. It also mounts `MemberList`, surfacing
 * `organization-detail-members-table`.
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

function seedActiveOrg(client: QueryClient) {
  client.setQueryData(["orgs", "o1"], org);
  client.setQueryData(["orgs", "o1", "members"], []);
}

describe("OrganizationDetail", () => {
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
    // Unseeded queries (the bound-clients list, member refetches after a
    // lifecycle mutation) resolve to an empty array so their list renders never
    // see a non-array body.
    sdk.resolveJson([]);
  });

  it("renders the org heading and a back link when the org loads", async () => {
    const client = newClient();
    seedActiveOrg(client);

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

    await expect.element(page.getByTestId("organization-detail-heading")).toHaveTextContent("Acme");
    await expect.element(page.getByTestId("organization-detail-back-link")).toBeInTheDocument();
  });

  it("renders the not-found state when the org is missing", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1"], null);

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

    await expect.element(page.getByTestId("organization-detail-not-found")).toBeInTheDocument();
    await expect.element(page.getByTestId("organization-detail-heading")).not.toBeInTheDocument();
  });

  it("mounts the member list (members table) for the org", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1"], org);
    client.setQueryData(
      ["orgs", "o1", "members"],
      [
        {
          id: "u1",
          email: "ada@acme.io",
          firstName: "Ada",
          lastName: "L",
          enabled: true,
          roles: ["Owner"],
        },
      ],
    );

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

    await expect.element(page.getByTestId("organization-detail-members-table")).toBeInTheDocument();
  });

  it("archives the org: POSTs to the org's archive endpoint", async () => {
    const client = newClient();
    seedActiveOrg(client);

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

    await userEvent.click(page.getByTestId("organization-detail-archive"));

    await vi.waitFor(() => {
      const archiveCall = sdk.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/archive",
      );
      expect(archiveCall).toBeDefined();
    });
  });

  it("reactivates the org: POSTs to the org's reactivate endpoint", async () => {
    const client = newClient();
    seedActiveOrg(client);

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

    await userEvent.click(page.getByTestId("organization-detail-reactivate"));

    await vi.waitFor(() => {
      const reactivateCall = sdk.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/reactivate",
      );
      expect(reactivateCall).toBeDefined();
    });
  });
});
