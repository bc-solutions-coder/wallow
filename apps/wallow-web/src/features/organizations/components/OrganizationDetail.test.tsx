import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Component spec for the org-detail page body (Wallow-8w1h.4.4). The
 * `getWallowSdk()` facade is mocked so detail/members queries are inert; the
 * detail state is driven by the `['orgs', id]` cache and archive/reactivate
 * delegate through the api.ts mutation factories to the mocked facade slice.
 *
 * Testids mirror the Blazor oracle: `organization-detail-back-link`,
 * `organization-detail-heading`, `organization-detail-not-found`, plus the new
 * lifecycle actions `organization-detail-archive` / `organization-detail-
 * reactivate`. It also mounts `MemberList`, surfacing
 * `organization-detail-members-table`.
 */

const mocks = vi.hoisted(() => ({
  get: vi.fn(),
  members: vi.fn(),
  addMember: vi.fn(),
  removeMember: vi.fn(),
  archive: vi.fn(),
  reactivate: vi.fn(),
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

function seedActiveOrg(client: QueryClient) {
  client.setQueryData(["orgs", "o1"], org);
  client.setQueryData(["orgs", "o1", "members"], []);
}

describe("OrganizationDetail", () => {
  beforeEach(() => {
    vi.clearAllMocks();
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

  it("archives the org: calls organizations.archive with the org id", async () => {
    const client = newClient();
    seedActiveOrg(client);
    mocks.archive.mockResolvedValue(undefined);

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

    await userEvent.click(page.getByTestId("organization-detail-archive"));

    await vi.waitFor(() => {
      expect(mocks.archive).toHaveBeenCalledWith("o1");
    });
  });

  it("reactivates the org: calls organizations.reactivate with the org id", async () => {
    const client = newClient();
    seedActiveOrg(client);
    mocks.reactivate.mockResolvedValue(undefined);

    renderWithClient(client, <OrganizationDetail orgId="o1" />);

    await userEvent.click(page.getByTestId("organization-detail-reactivate"));

    await vi.waitFor(() => {
      expect(mocks.reactivate).toHaveBeenCalledWith("o1");
    });
  });
});
