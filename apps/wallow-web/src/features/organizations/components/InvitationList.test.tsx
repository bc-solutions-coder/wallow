import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { invitationsGetByTenantQueryKey } from "@bc-solutions-coder/sdk/query";
import { INVITATIONS_QUERY, InvitationList } from "./InvitationList";

/**
 * The outstanding-invitations screen: roster (Pending only), empty and loading
 * states, and revoke.
 *
 * Runs the real SDK over a faked fetch (`createSdkHarness`) mounted on the
 * router context. The endpoint is ambient-tenant (no org id anywhere), and
 * returns every status — the "outstanding" filter down to Pending happens in
 * the component, so the fixtures below seed a non-Pending row to prove it.
 */

const threeInvitations = [
  {
    id: "i1",
    email: "ada@acme.io",
    status: "Pending",
    // Noon UTC, not midnight: `formatLongDate` renders in the runner's local
    // timezone, and a midnight UTC timestamp crosses the day boundary west of
    // Greenwich, making the asserted date flaky per machine.
    expiresAt: "2026-09-01T12:00:00Z",
    createdAt: "2026-08-01T00:00:00Z",
    acceptedByUserId: null,
  },
  {
    id: "i2",
    email: "bob@acme.io",
    status: "Pending",
    expiresAt: "2026-09-05T12:00:00Z",
    createdAt: "2026-08-01T00:00:00Z",
    acceptedByUserId: null,
  },
  {
    id: "i3",
    email: "cleo@acme.io",
    status: "Accepted",
    expiresAt: "2026-09-05T00:00:00Z",
    createdAt: "2026-08-01T00:00:00Z",
    acceptedByUserId: "u9",
  },
];

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("InvitationList", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders one invitation-item per Pending invitation, filtering out other statuses", async () => {
    harness.resolveJson(threeInvitations);

    renderWithWallow(<InvitationList />, { harness });

    await expect.element(page.getByTestId("invitations-table")).toBeInTheDocument();
    expect(page.getByTestId("invitation-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("ada@acme.io")).toBeInTheDocument();
    await expect.element(page.getByText("bob@acme.io")).toBeInTheDocument();
    await expect.element(page.getByText("cleo@acme.io")).not.toBeInTheDocument();
  });

  it("shows email, status and expiry on each row", async () => {
    harness.resolveJson([threeInvitations[0]]);

    renderWithWallow(<InvitationList />, { harness });

    const row = page.getByTestId("invitation-item");
    await expect.element(row).toHaveTextContent("ada@acme.io");
    await expect.element(row).toHaveTextContent("Pending");
    await expect.element(row).toHaveTextContent("September 1, 2026");
  });

  it("renders the empty state when there are no outstanding invitations", async () => {
    harness.resolveJson([threeInvitations[2]]);

    renderWithWallow(<InvitationList />, { harness });

    await expect
      .element(page.getByTestId("invitations-empty"))
      .toHaveTextContent("No outstanding invitations.");
    expect(page.getByTestId("invitation-item").elements()).toHaveLength(0);
  });

  it("shows a loading indicator while the invitations query is pending", async () => {
    harness.pending();

    renderWithWallow(<InvitationList />, { harness });

    await expect.element(page.getByTestId("invitations-loading")).toBeInTheDocument();
  });

  it("renders an error and no request carries an orgId", async () => {
    harness.rejectJson({ title: "boom" }, 500);

    renderWithWallow(<InvitationList />, { harness });

    await expect.element(page.getByTestId("invitations-error")).toBeInTheDocument();

    // `SdkCall.path` is pathname-only; the query string (and the absence of an
    // orgId anywhere in it) lives on `url`.
    const getCall = harness.calls.find((c) => c.method === "GET");
    expect(getCall?.path).toBe("/api/v1/identity/invitations");
    expect(getCall?.url).toContain("skip=0");
    expect(getCall?.url).toContain("take=50");
    expect(getCall?.url).not.toContain("orgId");
  });

  it("revokes: DELETEs the invitation and does not blank the list on a failed refetch", async () => {
    // First GET seeds the roster; the DELETE succeeds but the post-revoke
    // refetch fails — the row must stay on screen rather than blank the list.
    let getCount = 0;
    harness.respond((call) => {
      if (call.method === "DELETE") {
        return new Response(null, { status: 204 });
      }
      getCount += 1;
      return getCount === 1
        ? new Response(JSON.stringify(threeInvitations), {
            status: 200,
            headers: { "content-type": "application/json" },
          })
        : new Response(JSON.stringify({ title: "boom" }), {
            status: 500,
            headers: { "content-type": "application/problem+json" },
          });
    });

    renderWithWallow(<InvitationList />, { harness });

    await expect.element(page.getByTestId("invitation-item").first()).toBeInTheDocument();
    await userEvent.click(page.getByTestId("invitation-revoke").first());

    await vi.waitFor(() => {
      const deleteCall = harness.calls.find(
        (c) => c.method === "DELETE" && c.path === "/api/v1/identity/invitations/i1",
      );
      expect(deleteCall).toBeDefined();
    });

    expect(page.getByTestId("invitation-item").elements()).toHaveLength(2);
  });

  it("sweeps the invitations query after a successful revoke", async () => {
    harness.respond((call) =>
      call.method === "GET"
        ? new Response(JSON.stringify(threeInvitations), {
            status: 200,
            headers: { "content-type": "application/json" },
          })
        : new Response(null, { status: 204 }),
    );

    const { queryClient } = renderWithWallow(<InvitationList />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.click(page.getByTestId("invitation-revoke").first());

    await expectSwept(invalidateSpy, invitationsGetByTenantQueryKey({ query: INVITATIONS_QUERY }));
  });
});
