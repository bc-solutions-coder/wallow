import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Org-detail bound-clients + register-client reachability spec
 * (Wallow-ffpq.3.6) — the React port of Blazor `OrganizationDetail.razor`'s
 * OAuth-client section. Once an org loads, the detail page must surface the
 * org's bound clients (`organization-detail-clients-table`) and a
 * register-client form (display-name / client-type / redirect-uris / submit),
 * reachable straight from the org detail page. Testids mirror the Blazor oracle.
 *
 * The `getWallowSdk()` facade is mocked (like `OrganizationDetail.test.tsx`);
 * the `clients` / `registerClient` slice methods are stubbed so the green
 * implementation's api.ts wiring has a seam to bind to.
 */

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  members: vi.fn(),
  addMember: vi.fn(),
  removeMember: vi.fn(),
  archive: vi.fn(),
  reactivate: vi.fn(),
  clients: vi.fn(),
  registerClient: vi.fn(),
}));

vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    organizations: {
      get: mocks.get,
      members: mocks.members,
      addMember: mocks.addMember,
      removeMember: mocks.removeMember,
      archive: mocks.archive,
      reactivate: mocks.reactivate,
      clients: mocks.clients,
      registerClient: mocks.registerClient,
    },
  }),
}));

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
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
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.clients.mockResolvedValue([]);
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
