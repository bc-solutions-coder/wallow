import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { organizationsGetMembersQueryKey, organizationsGetPendingMembersQueryKey } from "../api";
import { PendingRequestList } from "./PendingRequestList";

/**
 * Pending membership requests: roster, empty and loading states, and the
 * approve/deny mutations.
 *
 * Runs the real SDK over a faked fetch (`createSdkHarness`) mounted on the
 * router context. Every state comes off the wire rather than a primed cache:
 * loading is a request that never settles (`harness.pending()`).
 */

const twoRequests = [
  {
    userId: "u1",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "L",
    requestedAt: "2026-01-05T10:00:00Z",
  },
  { userId: "u2", email: "bob@acme.io", firstName: "Bob", lastName: "R", requestedAt: null },
];

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("PendingRequestList", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each seeded request as an organization-pending-request-item", async () => {
    harness.resolveJson(twoRequests);

    renderWithWallow(<PendingRequestList orgId="o1" />, { harness });

    await expect
      .element(page.getByTestId("organization-pending-requests-table"))
      .toBeInTheDocument();
    expect(page.getByTestId("organization-pending-request-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("ada@acme.io")).toBeInTheDocument();
    await expect.element(page.getByText("bob@acme.io")).toBeInTheDocument();
  });

  it("falls back when requestedAt is null", async () => {
    harness.resolveJson(twoRequests);

    renderWithWallow(<PendingRequestList orgId="o1" />, { harness });

    await expect.element(page.getByText("bob@acme.io")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-pending-request-item").last())
      .not.toHaveTextContent("Invalid Date");
  });

  it("renders the empty state when there are no pending requests", async () => {
    harness.resolveJson([]);

    renderWithWallow(<PendingRequestList orgId="o1" />, { harness });

    await expect
      .element(page.getByTestId("organization-pending-requests-empty"))
      .toBeInTheDocument();
    expect(page.getByTestId("organization-pending-request-item").elements()).toHaveLength(0);
  });

  it("shows a loading indicator while the pending query is pending", async () => {
    harness.pending();

    renderWithWallow(<PendingRequestList orgId="o1" />, { harness });

    await expect
      .element(page.getByTestId("organization-pending-requests-loading"))
      .toBeInTheDocument();
  });

  it("shows an error banner when the pending query fails and no data is cached", async () => {
    harness.rejectJson({ title: "Not Found", detail: "not found" }, 404);

    renderWithWallow(<PendingRequestList orgId="o1" />, { harness });

    await expect
      .element(page.getByTestId("organization-pending-requests-error"))
      .toBeInTheDocument();
  });

  it("approves a request: POSTs to the approve endpoint", async () => {
    // The POST and the post-success refetches share one responder; answer by
    // method so the refetch still yields an array.
    harness.respond((call) =>
      call.method === "GET"
        ? new Response(JSON.stringify(twoRequests), {
            status: 200,
            headers: { "content-type": "application/json" },
          })
        : new Response(null, { status: 204 }),
    );

    renderWithWallow(<PendingRequestList orgId="o1" />, { harness });

    const approveButtons = page.getByTestId("organization-pending-request-approve");
    await expect.element(approveButtons.first()).toBeInTheDocument();
    await userEvent.click(approveButtons.first());

    await vi.waitFor(() => {
      const approveCall = harness.calls.find(
        (c) =>
          c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/members/u1/approve",
      );
      expect(approveCall).toBeDefined();
    });
  });

  it("sweeps both the pending and members queries after a successful approve", async () => {
    harness.respond((call) =>
      call.method === "GET"
        ? new Response(JSON.stringify(twoRequests), {
            status: 200,
            headers: { "content-type": "application/json" },
          })
        : new Response(null, { status: 204 }),
    );

    const { queryClient } = renderWithWallow(<PendingRequestList orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.click(page.getByTestId("organization-pending-request-approve").first());

    await expectSwept(
      invalidateSpy,
      organizationsGetPendingMembersQueryKey({ path: { id: "o1" } }),
    );
    await expectSwept(invalidateSpy, organizationsGetMembersQueryKey({ path: { id: "o1" } }));
  });

  it("denies a request: POSTs to the deny endpoint", async () => {
    harness.respond((call) =>
      call.method === "GET"
        ? new Response(JSON.stringify(twoRequests), {
            status: 200,
            headers: { "content-type": "application/json" },
          })
        : new Response(null, { status: 204 }),
    );

    renderWithWallow(<PendingRequestList orgId="o1" />, { harness });

    const denyButtons = page.getByTestId("organization-pending-request-deny");
    await expect.element(denyButtons.first()).toBeInTheDocument();
    await userEvent.click(denyButtons.first());

    await vi.waitFor(() => {
      const denyCall = harness.calls.find(
        (c) =>
          c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/members/u1/deny",
      );
      expect(denyCall).toBeDefined();
    });
  });

  it("sweeps only the pending query after a successful deny", async () => {
    harness.respond((call) =>
      call.method === "GET"
        ? new Response(JSON.stringify(twoRequests), {
            status: 200,
            headers: { "content-type": "application/json" },
          })
        : new Response(null, { status: 204 }),
    );

    const { queryClient } = renderWithWallow(<PendingRequestList orgId="o1" />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.click(page.getByTestId("organization-pending-request-deny").first());

    await expectSwept(
      invalidateSpy,
      organizationsGetPendingMembersQueryKey({ path: { id: "o1" } }),
    );
  });

  it("renders a 422 approve failure and keeps the list mounted", async () => {
    harness.respond((call) => {
      if (call.method === "GET") {
        return new Response(JSON.stringify(twoRequests), {
          status: 200,
          headers: { "content-type": "application/json" },
        });
      }
      return new Response(
        JSON.stringify({
          title: "Unprocessable Entity",
          detail: "The membership request could not be found.",
          code: "Identity.MemberNotFound",
        }),
        { status: 422, headers: { "content-type": "application/json" } },
      );
    });

    renderWithWallow(<PendingRequestList orgId="o1" />, { harness });

    await userEvent.click(page.getByTestId("organization-pending-request-approve").first());

    await expect
      .element(page.getByTestId("organization-pending-requests-error"))
      .toHaveTextContent("The membership request could not be found.");
    expect(page.getByTestId("organization-pending-request-item").elements()).toHaveLength(2);
  });
});
