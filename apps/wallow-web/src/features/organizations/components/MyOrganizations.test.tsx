import {
  createSdkHarness,
  failsWith,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { expectSwept } from "@bc-solutions-coder/testing/invalidation";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { meGetOrganizationsQueryKey } from "../api";
import { MyOrganizations } from "./MyOrganizations";

/**
 * The member-facing "my organizations" screen (Wallow-yp3e.7): the caller's own
 * memberships from `meGetOrganizations` (tagged `Me`, not `Organizations`) with
 * a way to leave one.
 *
 * Leaving goes through `AlertDialog`, so every confirm/cancel press here is a
 * raw `.element().click()` rather than `userEvent.click`: Base UI's own
 * full-window pointer blocker sits inside the open popup, and `userEvent`'s
 * real hit-testing would hang on it. `userEvent.click` stays safe for the
 * Trigger, which is pressed before any popup exists.
 *
 * `AlertDialog.Close` always closes the popup on click regardless of the
 * mutation's outcome, so a failed leave cannot keep the dialog open to show its
 * own error — the refusal renders as a page-level banner AFTER the popup
 * closes, with the membership still in the list.
 */

let harness: SdkHarness;

const singleOwnedOrg = [{ organizationId: "o1", name: "Acme", slug: "acme", isOwner: true }];

describe("MyOrganizations", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders each membership as a my-organization-item, with an owner badge only where isOwner", async () => {
    harness.resolveJson([
      { organizationId: "o1", name: "Acme", slug: "acme", isOwner: true },
      { organizationId: "o2", name: "Globex", slug: "globex", isOwner: false },
    ]);

    renderWithWallow(<MyOrganizations />, { harness });

    await expect.element(page.getByTestId("my-organization-item").first()).toBeInTheDocument();
    expect(page.getByTestId("my-organization-item").elements()).toHaveLength(2);
    await expect.element(page.getByText("Acme", { exact: true })).toBeInTheDocument();
    await expect.element(page.getByText("Globex", { exact: true })).toBeInTheDocument();
    expect(page.getByTestId("my-organization-item-owner").elements()).toHaveLength(1);
  });

  it("offers a switch into each organization as a full-document login link carrying the hint", async () => {
    // The picker: `/bff/login` re-authorizes with the `organization` hint, so
    // the IdP scopes the new session to that organization. It is a link (a
    // BFF endpoint outside the route tree), never an imperative login().
    harness.resolveJson([
      { organizationId: "o1", name: "Acme", slug: "acme", isOwner: true },
      { organizationId: "o2", name: "Globex", slug: "globex", isOwner: false },
    ]);

    renderWithWallow(<MyOrganizations />, { harness });

    await expect.element(page.getByTestId("my-organization-switch").first()).toBeInTheDocument();
    const links = page.getByTestId("my-organization-switch").elements();
    expect(links.map((link) => link.getAttribute("href"))).toEqual([
      "/bff/login?returnTo=%2Fdashboard&organization=o1",
      "/bff/login?returnTo=%2Fdashboard&organization=o2",
    ]);
  });

  it("renders the empty state when the caller belongs to no organizations", async () => {
    harness.resolveJson([]);

    renderWithWallow(<MyOrganizations />, { harness });

    await expect.element(page.getByTestId("my-organizations-empty")).toBeInTheDocument();
    expect(page.getByTestId("my-organization-item").elements()).toHaveLength(0);
  });

  it("renders a loading indicator while the memberships query is pending", async () => {
    harness.pending();

    renderWithWallow(<MyOrganizations />, { harness });

    await expect.element(page.getByTestId("my-organizations-loading")).toBeInTheDocument();
    expect(page.getByTestId("my-organization-item").elements()).toHaveLength(0);
  });

  it("renders a query error and no list when the memberships request fails", async () => {
    harness.rejectJson({ title: "Server error" }, 500);

    renderWithWallow(<MyOrganizations />, { harness });

    await expect.element(page.getByTestId("my-organizations-error")).toBeInTheDocument();
    expect(page.getByTestId("my-organization-item").elements()).toHaveLength(0);
  });

  it("opens a confirmation dialog naming the organization before leaving", async () => {
    routeHarness(harness, {
      "GET /v1/identity/me/organizations": singleOwnedOrg,
    });

    renderWithWallow(<MyOrganizations />, { harness });

    await expect.element(page.getByTestId("my-organization-leave")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("my-organization-leave"));

    await expect.element(page.getByTestId("my-organization-leave-title")).toHaveTextContent("Acme");
    // Opening the dialog must not itself leave the organization.
    expect(harness.calls.some((call) => call.method === "POST")).toBe(false);
  });

  it("cancelling the dialog leaves the membership untouched", async () => {
    routeHarness(harness, {
      "GET /v1/identity/me/organizations": singleOwnedOrg,
    });

    renderWithWallow(<MyOrganizations />, { harness });

    await userEvent.click(page.getByTestId("my-organization-leave"));
    await expect.element(page.getByTestId("my-organization-leave-cancel")).toBeInTheDocument();

    (page.getByTestId("my-organization-leave-cancel").element() as HTMLElement).click();

    // Unmount is animation-frame-deferred, so a synchronous read would pass
    // even against a dialog that never closed.
    await expect
      .poll(() => document.body.querySelector('[data-testid="my-organization-leave-title"]'))
      .toBeNull();
    expect(harness.calls.some((call) => call.method === "POST")).toBe(false);
    await expect.element(page.getByTestId("my-organization-item")).toBeInTheDocument();
  });

  it("confirming leave POSTs the leave endpoint and sweeps the memberships query", async () => {
    routeHarness(harness, {
      "GET /v1/identity/me/organizations": singleOwnedOrg,
      "POST /v1/identity/organizations/o1/leave": {},
    });

    const { queryClient } = renderWithWallow(<MyOrganizations />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.click(page.getByTestId("my-organization-leave"));
    await expect.element(page.getByTestId("my-organization-leave-confirm")).toBeInTheDocument();

    (page.getByTestId("my-organization-leave-confirm").element() as HTMLElement).click();

    await vi.waitFor(() => {
      const call = harness.calls.find(
        (c) => c.method === "POST" && c.path === "/api/v1/identity/organizations/o1/leave",
      );
      expect(call).toBeDefined();
    });
    await expectSwept(invalidateSpy, meGetOrganizationsQueryKey());
  });

  it("renders the sole-owner refusal as an explained error and keeps the membership listed", async () => {
    routeHarness(harness, {
      "GET /v1/identity/me/organizations": singleOwnedOrg,
      "POST /v1/identity/organizations/o1/leave": failsWith(
        {
          title: "Unprocessable Entity",
          detail: "You are the last owner of this organization and cannot leave it.",
          extensions: { code: "Identity.LastOwner" },
        },
        422,
      ),
    });

    renderWithWallow(<MyOrganizations />, { harness });

    await userEvent.click(page.getByTestId("my-organization-leave"));
    await expect.element(page.getByTestId("my-organization-leave-confirm")).toBeInTheDocument();

    (page.getByTestId("my-organization-leave-confirm").element() as HTMLElement).click();

    await expect
      .element(page.getByTestId("my-organizations-leave-error"))
      .toHaveTextContent("You are the last owner of this organization and cannot leave it.");
    await expect.element(page.getByTestId("my-organization-item")).toBeInTheDocument();
  });
});
