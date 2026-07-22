import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MemberList } from "./MemberList";

/**
 * Component spec for the org-detail member list + management (Wallow-8w1h.4.4).
 * The `getWallowSdk()` facade is mocked so the members query is inert; the
 * seeded/empty/loading states are driven by the `['orgs', id, 'members']` cache,
 * and add/remove delegate through `addMemberMutation`/`removeMemberMutation`
 * (api.ts factories) to the mocked facade slice.
 *
 * Testids: `organization-detail-members-table` + `organization-detail-member-row`
 * (table), `organization-members-empty`/`organization-members-loading`
 * (states), `organization-member-userid` + `organization-member-add-submit`
 * (add form), `organization-member-remove` (per-row remove) — all
 * `{page}-{element}` kebab-case.
 */

const mocks = vi.hoisted(() => ({
  members: vi.fn(),
  addMember: vi.fn(),
  removeMember: vi.fn(),
}));

// Mock the facade module (`../../lib/wallow-sdk` from api.ts;
// `../../../lib/wallow-sdk` from this test file's depth).
vi.mock("../../../lib/wallow-sdk", () => ({
  getWallowSdk: () => ({
    organizations: {
      members: mocks.members,
      addMember: mocks.addMember,
      removeMember: mocks.removeMember,
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
  beforeEach(() => {
    vi.clearAllMocks();
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
    mocks.members.mockReturnValue(new Promise<never>(() => {}));

    renderWithClient(client, <MemberList orgId="o1" />);

    await expect.element(page.getByTestId("organization-members-loading")).toBeInTheDocument();
  });

  it("adds a member: submits the userId to organizations.addMember for the org", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    mocks.addMember.mockResolvedValue({ id: "u9" });

    renderWithClient(client, <MemberList orgId="o1" />);

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(mocks.addMember).toHaveBeenCalledWith("o1", { userId: "u9" });
    });
  });

  it("invalidates the members query after a successful add", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.addMember.mockResolvedValue({ id: "u9" });

    renderWithClient(client, <MemberList orgId="o1" />);

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["orgs", "o1", "members"] });
    });
  });

  it("removes a member: calls organizations.removeMember with the org and user id", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    mocks.removeMember.mockResolvedValue(undefined);

    renderWithClient(client, <MemberList orgId="o1" />);

    const removeButtons = page.getByTestId("organization-member-remove");
    await expect.element(removeButtons.first()).toBeInTheDocument();
    await userEvent.click(removeButtons.first());

    await vi.waitFor(() => {
      expect(mocks.removeMember).toHaveBeenCalledWith("o1", "u1");
    });
  });
});
