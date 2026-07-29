import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { expectSwept } from "../../../test/invalidation";
import { appsGetUserAppsQueryKey } from "../api";
import { RegisterAppForm } from "./RegisterAppForm";

/**
 * Component spec for the register-app form (Wallow-8w1h.5.3). Copies the
 * CANONICAL create-form template (CreateOrganizationForm) — `useForm` (TanStack
 * Form) + `useMutation(registerAppMutation(queryClient))` — and adds the
 * behaviors unique to app registration:
 *
 *   - Field remap (API request contract):
 *     DisplayName -> clientName, Scopes -> requestedScopes; clientType defaults
 *     to "public"; redirect URIs are a newline-separated textarea split on `\n`
 *     with empty lines dropped.
 *   - Scope multi-select toggle buttons (available: inquiries.read,
 *     inquiries.write, announcements.read, storage.read; default selected:
 *     inquiries.read).
 *   - The ONE-TIME client secret: `AppRegistrationResponse.clientSecret` is
 *     returned ONLY from the register call (GET /apps and GET /apps/{id} carry
 *     no secret), so it is rendered exactly once — after a successful
 *     registration — via `data-testid=app-client-secret` (+ `app-client-id`),
 *     never before, and never re-fetchable. Mirrors RegisterAppResult.cs /
 *     RegisterApp.razor:37-49 ("Save your client secret now. It will not be
 *     shown again.").
 *
 * The form builds its mutation from the GENERATED `appsRegisterMutation({ client })`
 * re-exported by api.ts (Wallow-pu6a.5.5), so the network seam is the SDK
 * instance the render puts on the router context, backed by `createSdkHarness()`.
 * The register call resolves the one-time-secret response
 * (`harness.resolveJson(OK_RESPONSE)` in `beforeEach`); the submitted body is
 * asserted via the recorded outgoing request (`harness.last`); the success sweep
 * is checked by running the filter the mutation passed `invalidateQueries`
 * against the real `appsGetUserAppsQueryKey()` (`expectSwept`); a server
 * ProblemDetails is driven with `harness.rejectJson`.
 *
 * Testids follow the apps feature's `app-*` convention (like `app-item`, and
 * the bead-mandated `app-client-secret`/`app-client-id`): `app-display-name`
 * (input), `app-client-type` (select), `app-redirect-uris` (textarea),
 * `app-post-logout-redirect-uris` (textarea), `app-scope-{scope-dashed}` (toggle
 * buttons), `app-register-submit` (submit), `app-display-name-error`
 * (required-field validation), `app-register-error` (server RFC 7807
 * ProblemDetails surface), `app-client-secret` + `app-client-secret-copy` +
 * `app-client-id` (one-time success reveal).
 *
 * F7/T7.2 CONTRACT UPDATE (reworked AppsController): the regenerated
 * `RegisterAppRequest` gained `postLogoutRedirectUris`, and the register endpoint
 * now accepts the OIDC login scopes (`ApiScopes.LoginScopes` = openid, profile,
 * email, offline_access) in addition to the developer-app scopes. The form must
 * collect post-logout redirect URIs and offer the login scopes as selectable.
 * (Secret rotation + redirect-URI management remain DESCOPED — no AppsController
 * endpoint exists for them; tracked separately.)
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
