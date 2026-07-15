/** @vitest-environment jsdom */
import * as matchers from "@testing-library/jest-dom/matchers";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { MemberList } from "./MemberList";

// No global `expect` (vitest `globals` is off), so register the jest-dom
// matchers explicitly — the DOM-matcher convention for wallow-web's RTL tests.
expect.extend(matchers);

/**
 * Component spec for the org-detail member list + management (Wallow-8w1h.4.4).
 * The `getWallowSdk()` facade is mocked so the members query is inert; the
 * seeded/empty/loading states are driven by the `['orgs', id, 'members']` cache,
 * and add/remove delegate through `addMemberMutation`/`removeMemberMutation`
 * (api.ts factories) to the mocked facade slice.
 *
 * Testids: `organization-detail-members-table` + `organization-detail-member-row`
 * (Blazor oracle), `organization-members-empty`/`organization-members-loading`
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

    const rows = await screen.findAllByTestId("organization-detail-member-row");
    expect(rows).toHaveLength(2);
    expect(screen.getByTestId("organization-detail-members-table")).toBeInTheDocument();
    expect(screen.getByText("ada@acme.io")).toBeInTheDocument();
    expect(screen.getByText("bob@acme.io")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when there are no members", async () => {
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], []);

    renderWithClient(client, <MemberList orgId="o1" />);

    expect(await screen.findByTestId("organization-members-empty")).toBeInTheDocument();
    expect(screen.queryAllByTestId("organization-detail-member-row")).toHaveLength(0);
  });

  it("shows a loading indicator while the members query is pending", () => {
    const client = newClient();
    mocks.members.mockReturnValue(new Promise<never>(() => {}));

    renderWithClient(client, <MemberList orgId="o1" />);

    expect(screen.getByTestId("organization-members-loading")).toBeInTheDocument();
  });

  it("adds a member: submits the userId to organizations.addMember for the org", async () => {
    const user = userEvent.setup();
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    mocks.addMember.mockResolvedValue({ id: "u9" });

    renderWithClient(client, <MemberList orgId="o1" />);

    await user.type(await screen.findByTestId("organization-member-userid"), "u9");
    await user.click(screen.getByTestId("organization-member-add-submit"));

    await waitFor(() => {
      expect(mocks.addMember).toHaveBeenCalledWith("o1", { userId: "u9" });
    });
  });

  it("invalidates the members query after a successful add", async () => {
    const user = userEvent.setup();
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    const invalidateSpy = vi.spyOn(client, "invalidateQueries");
    mocks.addMember.mockResolvedValue({ id: "u9" });

    renderWithClient(client, <MemberList orgId="o1" />);

    await user.type(await screen.findByTestId("organization-member-userid"), "u9");
    await user.click(screen.getByTestId("organization-member-add-submit"));

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["orgs", "o1", "members"] });
    });
  });

  it("removes a member: calls organizations.removeMember with the org and user id", async () => {
    const user = userEvent.setup();
    const client = newClient();
    client.setQueryData(["orgs", "o1", "members"], twoMembers);
    mocks.removeMember.mockResolvedValue(undefined);

    renderWithClient(client, <MemberList orgId="o1" />);

    const removeButtons = await screen.findAllByTestId("organization-member-remove");
    await user.click(removeButtons[0]);

    await waitFor(() => {
      expect(mocks.removeMember).toHaveBeenCalledWith("o1", "u1");
    });
  });
});
