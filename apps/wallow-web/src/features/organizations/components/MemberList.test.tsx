import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import { MemberList } from "./MemberList";

/**
 * Component spec for the org-detail member list + management (Wallow-8w1h.4.4).
 * Data flows through the SDK query layer (`organizationsQueries.members()` +
 * `addMemberMutation`/`removeMemberMutation`), so the network seam is the shared
 * SDK client's `fetch`, overridden per test via `installSdkClientMock`
 * (Wallow-evd5.2.6 — the retired `getWallowSdk()` facade is no longer in the
 * path). Seeded/empty states are driven by the `['orgs', id, 'members']` cache
 * (`staleTime: Infinity` keeps the seed from refetching), loading by a
 * never-settling request (`sdk.pending()`), and add/remove are asserted via the
 * recorded outgoing request (`sdk.calls`) and the live client's
 * `invalidateQueries`.
 *
 * Testids: `organization-detail-members-table` + `organization-detail-member-row`
 * (table), `organization-members-empty`/`organization-members-loading`
 * (states), `organization-member-userid` + `organization-member-add-submit`
 * (add form), `organization-member-remove` (per-row remove) — all
 * `{page}-{element}` kebab-case.
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

const twoMembers = [
  {
    id: "u1",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "L",
    enabled: true,
    roles: ["Owner"],
  },
  {
    id: "u2",
    email: "bob@acme.io",
    firstName: "Bob",
    lastName: "R",
    enabled: true,
    roles: ["Member"],
  },
];

describe("MemberList", () => {
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("renders each seeded member as an organization-detail-member-row", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);

    renderWithClient(client, <MemberList orgId="o1" />);

    await expect.element(page.getByTestId("organization-detail-members-table")).toBeInTheDocument();
    expect(page.getByTestId("organization-detail-member-row").elements()).toHaveLength(2);
    await expect.element(page.getByText("ada@acme.io")).toBeInTheDocument();
    await expect.element(page.getByText("bob@acme.io")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when there are no members", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], []);

    renderWithClient(client, <MemberList orgId="o1" />);

    await expect.element(page.getByTestId("organization-members-empty")).toBeInTheDocument();
    expect(page.getByTestId("organization-detail-member-row").elements()).toHaveLength(0);
  });

  it("shows a loading indicator while the members query is pending", async () => {
    const client = newClient();
    sdk.pending();

    renderWithClient(client, <MemberList orgId="o1" />);

    await expect.element(page.getByTestId("organization-members-loading")).toBeInTheDocument();
  });

  it("adds a member: POSTs the userId to the org's members endpoint", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    // The post-success invalidation refetches the members list; keep it an array.
    sdk.resolveJson([]);

    renderWithClient(client, <MemberList orgId="o1" />);

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      const addCall = sdk.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/members",
      );
      expect(addCall).toBeDefined();
      expect(addCall?.body).toEqual({ userId: "u9" });
    });
  });

  it("invalidates the members query after a successful add", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    sdk.resolveJson([]);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");

    renderWithClient(client, <MemberList orgId="o1" />);

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["orgs", "o1", "members"] });
    });
  });

  it("removes a member: DELETEs the org member by user id", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    sdk.resolveJson([]);

    renderWithClient(client, <MemberList orgId="o1" />);

    const removeButtons = page.getByTestId("organization-member-remove");
    await expect.element(removeButtons.first()).toBeInTheDocument();
    await userEvent.click(removeButtons.first());

    await vi.waitFor(() => {
      const removeCall = sdk.calls.find(
        (c) => c.method === "DELETE" && c.path === "/api/v1/identity/organizations/o1/members/u1",
      );
      expect(removeCall).toBeDefined();
    });
  });
});
