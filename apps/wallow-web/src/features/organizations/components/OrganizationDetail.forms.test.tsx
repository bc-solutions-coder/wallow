import {
  createSdkHarness,
  failsWith,
  neverSettles,
  routeHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The register-application stepper on `@bc-solutions-coder/forms`: shell,
 * labels, disabling, the RFC 7807 split (a field error re-opens its step), the
 * wire body, and the one-time secret reveal.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** The register response the harness answers a successful submit with. */
const OK_RESPONSE = {
  client: {
    clientId: "app-acme-dashboard",
    name: "Dashboard",
    kind: "application",
    status: "active",
    redirectUris: ["https://a.example/cb"],
    postLogoutRedirectUris: [],
    scopes: ["openid"],
    createdByUserId: "u1",
    createdAt: "2026-01-01T00:00:00Z",
  },
  clientSecret: "secret-xyz",
  issuer: "https://auth.example/auth",
  apiBaseUrl: "https://api.example",
};

/** The POST path the register flow issues. */
const REGISTER_ROUTE = "POST /v1/identity/organizations/o1/clients";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Answer the org / members / clients / scopes reads with a loaded org, and
 * the register POST with `registerResponse`.
 */
function seedLoadedOrg(registerResponse: unknown = OK_RESPONSE): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/organizations/o1/clients": [],
      "GET /v1/identity/scopes": [],
      [REGISTER_ROUTE]: registerResponse,
    },
    { fallback: [] },
  );
}

/** Wait for the org read to paint, then open the stepper on its Basics step. */
async function openStepper(): Promise<void> {
  await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
  await userEvent.click(page.getByTestId("organization-detail-register-open"));
  await expect.element(page.getByTestId("organization-detail-register-name")).toBeVisible();
}

/** Open the stepper and satisfy every required field, landing on Redirects. */
async function openAndFillRequired(redirectUris = "https://a.example/cb"): Promise<void> {
  await openStepper();
  await userEvent.fill(page.getByTestId("organization-detail-register-name"), "Dashboard");
  await userEvent.click(page.getByTestId("organization-detail-register-next"));
  await expect
    .element(page.getByTestId("organization-detail-register-redirect-uris"))
    .toBeVisible();
  await userEvent.fill(
    page.getByTestId("organization-detail-register-redirect-uris"),
    redirectUris,
  );
  await expect.element(page.getByTestId("organization-detail-register-submit")).toBeEnabled();
}

/**
 * Only the register POSTs. `harness.calls` also holds the screen's reads and
 * their refetches, so "the endpoint was not reached" has to be said about this
 * operation rather than about the transport as a whole.
 */
function registerCalls(): readonly SdkCall[] {
  return harness.calls.filter(
    (call: SdkCall) =>
      call.method === "POST" && call.path.endsWith("/v1/identity/organizations/o1/clients"),
  );
}

function formElement(): HTMLFormElement {
  return page.getByTestId("organization-detail-register-form").element() as HTMLFormElement;
}

function nameInput(): HTMLInputElement {
  return page.getByTestId("organization-detail-register-name").element() as HTMLInputElement;
}

function redirectUrisTextarea(): HTMLTextAreaElement {
  return page
    .getByTestId("organization-detail-register-redirect-uris")
    .element() as HTMLTextAreaElement;
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId("organization-detail-register-submit").element() as HTMLButtonElement;
}

/** The text of the `<label for>` pointing at `control`. */
function accessibleName(control: HTMLElement): string {
  const label: Element | null =
    control.id === "" ? null : document.querySelector(`label[for="${CSS.escape(control.id)}"]`);

  return label?.textContent?.trim() ?? "";
}

/**
 * The ids `control` points its `aria-describedby` at. Split rather than
 * compared whole: Base UI appends the message to whatever else already
 * describes the control, so the claim is that it is AMONG them, not alone.
 */
function describedByIds(control: HTMLElement): readonly string[] {
  return (control.getAttribute("aria-describedby") ?? "")
    .split(" ")
    .filter((id: string) => id !== "");
}

function validationFailure(errors: Record<string, string[]>): unknown {
  return failsWith(
    {
      type: "https://httpstatuses.io/400",
      title: "One or more validation errors occurred.",
      code: "Validation.Failed",
      status: 400,
      errors,
    },
    400,
  );
}

describe("OrganizationDetail register-application stepper on @bc-solutions-coder/forms", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders through the forms shell, which leaves validation to the schema", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();

    const element: HTMLFormElement = formElement();
    expect(element.tagName).toBe("FORM");
    // The zod schema is the only validator, so the browser must not also
    // refuse the submit with a native bubble.
    expect(element.noValidate).toBe(true);
  });

  it("keeps every step's controls under that one shell", async () => {
    // Inactive steps stay mounted but hidden; a control that fell outside the
    // shell would silently stop being part of the submitted values.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();

    const element: HTMLFormElement = formElement();
    for (const testId of [
      "organization-detail-register-name",
      "organization-detail-register-redirect-uris",
      "organization-detail-register-post-logout-redirect-uris",
      "organization-detail-register-backchannel-logout-uri",
      "organization-detail-register-backchannel-logout-session-required",
      "organization-detail-register-submit",
    ]) {
      const control: HTMLElement = page.getByTestId(testId).element() as HTMLElement;
      expect(control.closest("form"), `${testId} must live under the form shell`).toBe(element);
    }
  });

  it("labels the name input and the redirect-URI textarea", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();

    expect(accessibleName(nameInput())).toBe("Name");
    expect(accessibleName(redirectUrisTextarea())).toBe("Redirect URIs");
  });

  it("keeps the redirect-URI control a real textarea", async () => {
    // The newline-separated list only works if the control still accepts a
    // newline — a catalog field that resolved to an `<input>` would quietly turn
    // the multi-URI contract into a single-line one.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openStepper();

    const redirectUris: HTMLTextAreaElement = redirectUrisTextarea();
    expect(redirectUris.tagName).toBe("TEXTAREA");
    for (const utility of ["min-h-20", "resize-y"]) {
      expect(redirectUris.classList.contains(utility), utility).toBe(true);
    }
  });

  it("disables the controls and the submit while the registration is in flight", async () => {
    seedLoadedOrg(neverSettles());

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openAndFillRequired();

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    // Wait for the request to REACH the transport first: the harness records a
    // call before its responder runs, so this is the earliest point at which
    // "in flight" is a fact rather than a race.
    await vi.waitFor(() => {
      expect(registerCalls()).toHaveLength(1);
    });
    await expect.poll(() => redirectUrisTextarea().disabled).toBe(true);
    expect(nameInput().disabled).toBe(true);
    // A second registration would mint a second client secret, and only one of
    // the two would ever be shown.
    expect(submitButton().disabled).toBe(true);
  });

  it("associates a validation failure's per-property message with its field, not the banner", async () => {
    seedLoadedOrg(validationFailure({ RedirectUris: ["'nope' is not an absolute URI."] }));

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openAndFillRequired("nope");

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    const message = page.getByTestId("organization-detail-register-redirect-uris-error");
    await expect.element(message).toHaveTextContent("'nope' is not an absolute URI.");

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(redirectUrisTextarea())).toContain(messageId);
    expect(redirectUrisTextarea().getAttribute("aria-invalid")).toBe("true");
    // Everything landed on a field, so the banner would only repeat it.
    expect(page.getByTestId("organization-detail-register-error").elements()).toHaveLength(0);
  });

  it("re-opens the step that owns a field the server rejected", async () => {
    // Register is reachable from any step, so a `Name` error raised while the
    // Redirects step is showing must bring Basics back or it is never seen.
    seedLoadedOrg(validationFailure({ Name: ["A client with that name is already registered."] }));

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openAndFillRequired();
    expect(page.getByTestId("organization-detail-register-name").element()).not.toBeVisible();

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-name-error"))
      .toHaveTextContent("A client with that name is already registered.");
    await expect.element(page.getByTestId("organization-detail-register-name")).toBeVisible();
  });

  it("shows the form's own sentence for a property the form has no field for", async () => {
    // A property the stepper does not hold cannot land on an input, and the
    // banner is one resolved sentence rather than the API's unmatched wording:
    // with no detail and no registry entry, the form's `fallbackError`.
    seedLoadedOrg(validationFailure({ Kind: ["Only applications can be registered here."] }));

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openAndFillRequired();

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-error"))
      .toHaveTextContent("Failed to register the application.");
  });

  it("surfaces a form-level failure's own sentence rather than a hardcoded one", async () => {
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/403",
          title: "Forbidden",
          code: "Identity.ClientLimitReached",
          status: 403,
          detail: "This organization has reached its client limit.",
        },
        403,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openAndFillRequired();

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-error"))
      .toHaveTextContent("This organization has reached its client limit.");
  });

  it("clears a server field error on the next submit rather than wedging the form", async () => {
    // Nothing in the form framework clears an `onServer` error by itself, so the
    // very next submit would otherwise fail the validity gate silently and never
    // reach the endpoint again — with no message to explain why.
    seedLoadedOrg(validationFailure({ RedirectUris: ["'nope' is not an absolute URI."] }));

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openAndFillRequired("nope");

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));
    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris-error"))
      .toBeInTheDocument();

    seedLoadedOrg();
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await vi.waitFor(() => {
      expect(registerCalls()).toHaveLength(2);
    });
  });

  it("posts the application body, splitting the URI lists and dropping empty optionals", async () => {
    // The only statement of the register wire contract anywhere.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openAndFillRequired("https://a.example/cb\n\n  https://b.example/cb  \n");

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await vi.waitFor(() => {
      expect(registerCalls()).toHaveLength(1);
    });
    expect(registerCalls()[0]?.body).toEqual({
      kind: "application",
      name: "Dashboard",
      redirectUris: ["https://a.example/cb", "https://b.example/cb"],
      postLogoutRedirectUris: [],
      backchannelLogoutSessionRequired: false,
      scopes: ["openid"],
    });
  });

  it("reveals the one-time client id and secret after a successful registration", async () => {
    // The secret is issued exactly once and can never be re-fetched, so a
    // reveal gate that never fires loses it outright.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await openAndFillRequired();

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-success"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-client-id"))
      .toHaveTextContent("app-acme-dashboard");
    await expect
      .element(page.getByTestId("organization-detail-register-client-secret"))
      .toHaveTextContent("secret-xyz");
  });
});
