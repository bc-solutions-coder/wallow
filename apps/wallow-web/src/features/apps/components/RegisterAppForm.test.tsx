import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "@shared/testing/invalidation";
import { appsGetUserAppsQueryKey } from "../api";
import { RegisterAppForm } from "./RegisterAppForm";

/**
 * The register-app form: submitted body, scope toggles, the one-time secret
 * reveal, and the server-error surface.
 *
 * The wire contract remaps `displayName` to `clientName` and `scopes` to
 * `requestedScopes`; the redirect-URI textareas are newline-separated lists.
 * `clientSecret` comes back ONLY from the register call, so it is never re-fetchable.
 */

const OK_RESPONSE = {
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  registrationAccessToken: "rat-123",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("RegisterAppForm", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson(OK_RESPONSE);
  });

  it("renders the display-name, client-type, redirect-uris, scope, and submit controls", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-display-name")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-client-type")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-redirect-uris")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-scope-inquiries-read")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-scope-announcements-read")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-register-submit")).toBeInTheDocument();
  });

  it("renders the post-logout redirect URIs field (reworked AppsController contract)", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-post-logout-redirect-uris")).toBeInTheDocument();
  });

  it("offers the BFF login scopes (openid, profile, email, offline_access) as selectable", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-scope-openid")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-scope-profile")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-scope-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-scope-offline_access")).toBeInTheDocument();
  });

  it("does NOT reveal the client secret or client id before a successful registration", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-client-secret")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("app-client-id")).not.toBeInTheDocument();
  });

  it("submits, POSTing the remapped body (clientName, default scope, public, parsed redirect + post-logout URIs)", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.type(
      page.getByTestId("app-redirect-uris"),
      "https://a.com/cb{enter}https://b.com/cb",
    );
    await userEvent.type(
      page.getByTestId("app-post-logout-redirect-uris"),
      "https://a.com/logout{enter}https://b.com/logout",
    );
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });
    expect(harness.last?.path).toBe("/api/v1/identity/apps/register");
    expect(harness.last?.body).toEqual({
      clientName: "My App",
      requestedScopes: ["inquiries.read"],
      clientType: "public",
      redirectUris: ["https://a.com/cb", "https://b.com/cb"],
      postLogoutRedirectUris: ["https://a.com/logout", "https://b.com/logout"],
    });
  });

  it("includes a toggled-on login scope (offline_access) in the submitted requestedScopes", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-scope-offline_access"));
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });
    const body = harness.last?.body as { requestedScopes: string[] };
    expect(body.requestedScopes).toContain("offline_access");
  });

  it("adds a toggled-on scope to the submitted requestedScopes", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-scope-announcements-read"));
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });
    const body = harness.last?.body as { requestedScopes: string[] };
    expect(body.requestedScopes).toEqual(
      expect.arrayContaining(["inquiries.read", "announcements.read"]),
    );
    expect(body.requestedScopes).toHaveLength(2);
  });

  it("removes a default scope when its toggle is clicked off", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-scope-inquiries-read"));
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });
    const body = harness.last?.body as { requestedScopes: string[] };
    expect(body.requestedScopes).not.toContain("inquiries.read");
  });

  it("reveals the one-time client secret and client id after a successful registration", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-client-secret")).toHaveTextContent("secret-xyz");
    await expect.element(page.getByTestId("app-client-id")).toHaveTextContent("client-abc");
  });

  it("copies the revealed client secret to the clipboard", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText },
      configurable: true,
    });

    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-client-secret")).toBeInTheDocument();
    await userEvent.click(page.getByTestId("app-client-secret-copy"));

    expect(writeText).toHaveBeenCalledWith("secret-xyz");
  });

  it("sweeps the app list query after a successful registration", async () => {
    const { queryClient } = renderWithWallow(<RegisterAppForm />, { harness });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expectSwept(invalidateSpy, appsGetUserAppsQueryKey());
  });

  it("blocks submit and shows a required error when the display name is empty", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-display-name-error")).toBeInTheDocument();
    expect(harness.calls).toHaveLength(0);
  });

  it("surfaces the ProblemDetails detail when registration fails", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "Bad Request",
        status: "400",
        detail: "That redirect URI is not allowed.",
      },
      400,
    );

    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect
      .element(page.getByTestId("app-register-error"))
      .toHaveTextContent("That redirect URI is not allowed.");
  });
});
