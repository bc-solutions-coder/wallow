import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { ConnectedAppsSection } from "./ConnectedAppsSection";

/**
 * ConnectedAppsSection — the consent ledger card: the applications the user has
 * authorized, each with its scopes and a withdraw action. Withdraw issues the
 * DELETE and then refetches the list through invalidation, so the responder
 * below answers the list differently once the DELETE has landed — the empty
 * result proves the sweep ran, not just that the button called something.
 */

const connectedApp = {
  id: "auth-1",
  clientId: "acme-crm",
  displayName: "Acme CRM",
  scopes: ["openid", "profile", "email"],
  createdAt: "2026-08-30T12:00:00Z",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("ConnectedAppsSection", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders one row per connected application with its name and scopes", async () => {
    harness.resolveJson([connectedApp]);

    renderWithWallow(<ConnectedAppsSection />, { harness });

    await expect
      .element(page.getByRole("heading", { name: "Connected applications" }))
      .toBeInTheDocument();
    await expect.element(page.getByTestId("connected-app-name")).toHaveTextContent("Acme CRM");
    await expect
      .element(page.getByTestId("connected-app-scopes"))
      .toHaveTextContent("openid, profile, email");
  });

  it("falls back to the client id when the application has no display name", async () => {
    harness.resolveJson([{ ...connectedApp, displayName: null }]);

    renderWithWallow(<ConnectedAppsSection />, { harness });

    await expect.element(page.getByTestId("connected-app-name")).toHaveTextContent("acme-crm");
  });

  it("renders the empty state when nothing is connected", async () => {
    harness.resolveJson([]);

    renderWithWallow(<ConnectedAppsSection />, { harness });

    await expect
      .element(page.getByTestId("connected-apps-empty"))
      .toHaveTextContent("No connected applications.");
  });

  it("renders the loading state while the list is pending", async () => {
    harness.pending();

    renderWithWallow(<ConnectedAppsSection />, { harness });

    await expect
      .element(page.getByTestId("connected-apps-loading"))
      .toHaveTextContent("Loading connected applications…");
  });

  it("renders the error state when the list read fails with no cached list", async () => {
    harness.rejectJson({ title: "boom" }, 500);

    renderWithWallow(<ConnectedAppsSection />, { harness });

    await expect.element(page.getByTestId("connected-apps-error")).toBeInTheDocument();
  });

  it("withdraws an application and the refreshed list drops it", async () => {
    let withdrawn = false;
    harness.respond((call) => {
      if (call.method === "DELETE" && call.path.endsWith("/v1/identity/me/authorizations/auth-1")) {
        withdrawn = true;
        return new Response(null, { status: 204 });
      }
      if (call.method === "GET" && call.path.endsWith("/v1/identity/me/authorizations")) {
        return Response.json(withdrawn ? [] : [connectedApp]);
      }
      return Response.json({ title: "unexpected request" }, { status: 404 });
    });

    renderWithWallow(<ConnectedAppsSection />, { harness });

    await expect.element(page.getByTestId("connected-app-name")).toHaveTextContent("Acme CRM");
    await userEvent.click(page.getByTestId("connected-app-withdraw"));

    // The empty state can only appear if the DELETE landed AND the invalidation
    // refetched the list — a missed sweep leaves the stale row rendered.
    await expect
      .element(page.getByTestId("connected-apps-empty"))
      .toHaveTextContent("No connected applications.");
    const deleteCall = harness.calls.find((call) => call.method === "DELETE");
    expect(deleteCall?.path.endsWith("/v1/identity/me/authorizations/auth-1")).toBe(true);
  });
});
