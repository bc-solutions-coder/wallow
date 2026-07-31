import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "@shared/testing/invalidation";
import { organizationsGetMembersQueryKey } from "../api";
import { MemberList } from "./MemberList";

/**
 * Component spec for the org-detail member list + management (Wallow-8w1h.4.4).
 * Data flows through the GENERATED query surface
 * (`organizationsGetMembersOptions` + `organizationsAddMemberMutation` /
 * `organizationsRemoveMemberMutation`), so the network seam is the SDK instance
 * the render puts on the router context, backed by `createSdkHarness()`
 * (Wallow-pu6a.5.5). Seeded/empty states come off the wire rather than a
 * pre-primed cache, loading from a never-settling request (`harness.pending()`),
 * and add/remove are asserted via the recorded outgoing request
 * (`harness.calls`) and the render's own `invalidateQueries`.
 *
 * Testids: `organization-detail-members-table` + `organization-detail-member-item`
 * (table), `organization-members-empty`/`organization-members-loading`
 * (states), `organization-member-userid` + `organization-member-add-submit`
 * (add form), `organization-member-remove` (per-row remove) — all
 * `{page}-{element}` kebab-case.
 */

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

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("MemberList", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each seeded member as an organization-detail-member-item", async () => {
    harness.resolveJson(twoMembers);

    renderWithWallow(<MemberList orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-detail-members-table")).toBeInTheDocument();
    expect(page.getByTestId("organization-detail-member-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("ada@acme.io")).toBeInTheDocument();
    await expect.element(page.getByText("bob@acme.io")).toBeInTheDocument();
  });

  it("renders the empty state and no rows when there are no members", async () => {
    harness.resolveJson([]);

    renderWithWallow(<MemberList orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-members-empty")).toBeInTheDocument();
    expect(page.getByTestId("organization-detail-member-item").elements()).toHaveLength(0);
  });

  it("shows a loading indicator while the members query is pending", async () => {
    harness.pending();

    renderWithWallow(<MemberList orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-members-loading")).toBeInTheDocument();
  });

  it("adds a member: POSTs the userId to the org's members endpoint", async () => {
    // The POST and the post-success members refetch share one responder; answer
    // by method so the refetch still yields an array.
    harness.respond((call) =>
      call.method === "GET"
        ? new Response(JSON.stringify(twoMembers), {
            status: 200,
            headers: { "content-type": "application/json" },
          })
        : new Response(null, { status: 204 }),
    );

    renderWithWallow(<MemberList orgId="o1" />, { harness });

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await vi.waitFor(() => {
      const addCall = harness.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/members",
      );
      expect(addCall).toBeDefined();
      expect(addCall?.body).toEqual({ userId: "u9" });
    });
  });

  it("sweeps the members query after a successful add", async () => {
    harness.resolveJson(twoMembers);

    const { queryClient } = renderWithWallow(<MemberList orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.type(page.getByTestId("organization-member-userid"), "u9");
    await userEvent.click(page.getByTestId("organization-member-add-submit"));

    await expectSwept(invalidateSpy, organizationsGetMembersQueryKey({ path: { id: "o1" } }));
  });

  it("removes a member: DELETEs the org member by user id", async () => {
    // The DELETE and the post-success members refetch share one responder, so it
    // answers by method: the list read keeps returning members (the row has to
    // still be there to click), the delete returns an empty 204-ish body.
    harness.respond((call) =>
      call.method === "GET"
        ? new Response(JSON.stringify(twoMembers), {
            status: 200,
            headers: { "content-type": "application/json" },
          })
        : new Response(null, { status: 204 }),
    );

    renderWithWallow(<MemberList orgId="o1" />, { harness });

    const removeButtons = page.getByTestId("organization-member-remove");
    await expect.element(removeButtons.first()).toBeInTheDocument();
    await userEvent.click(removeButtons.first());

    await vi.waitFor(() => {
      const removeCall = harness.calls.find(
        (c) => c.method === "DELETE" && c.path === "/api/v1/identity/organizations/o1/members/u1",
      );
      expect(removeCall).toBeDefined();
    });
  });
});
