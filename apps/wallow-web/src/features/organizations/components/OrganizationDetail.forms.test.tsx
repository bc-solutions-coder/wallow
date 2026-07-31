import {
  createSdkHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { failsWith, neverSettles, routeHarness } from "@shared/testing/harness-routes";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * The org-detail register-client form on `@bc-solutions-coder/forms`: shell,
 * labels, validation, disabling, the RFC 7807 split, body, secret reveal.
 *
 * The form's field is `displayName` but the wire name is `name`, and
 * `clientType` never reaches the wire — the endpoint infers public vs
 * confidential from the secret, and the default `toVariables` would post it.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** The register response the harness answers a successful submit with. */
const OK_RESPONSE = {
  id: "c1",
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  name: "Dashboard",
};

/** The POST path the register-client flow issues. */
const REGISTER_ROUTE = "POST /v1/identity/clients";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Answer the org / members / clients reads with a loaded org, and the register
 * POST with `registerResponse`.
 *
 * The screen's controls do not exist at first paint — the reads go over the wire
 * rather than out of a pre-seeded cache — so every case here settles the detail
 * query before driving anything.
 */
function seedLoadedOrg(registerResponse: unknown = OK_RESPONSE): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/clients/by-tenant/o1": [],
      [REGISTER_ROUTE]: registerResponse,
    },
    { fallback: [] },
  );
}

/** Wait for the org read to paint before driving a control. */
async function awaitLoaded(): Promise<void> {
  await expect.element(page.getByTestId("organization-detail-heading")).toBeInTheDocument();
  await expect
    .element(page.getByTestId("organization-detail-register-display-name"))
    .toBeInTheDocument();
}

/**
 * Only the register POSTs. `harness.calls` also holds the screen's three reads
 * and their refetches, so "the endpoint was not reached" has to be said about
 * this operation rather than about the transport as a whole.
 */
function registerCalls(): readonly SdkCall[] {
  return harness.calls.filter(
    (call: SdkCall) => call.method === "POST" && call.path.endsWith("/v1/identity/clients"),
  );
}

function formElement(): HTMLFormElement {
  return page.getByTestId("organization-detail-register-form").element() as HTMLFormElement;
}

function displayNameInput(): HTMLInputElement {
  return page
    .getByTestId("organization-detail-register-display-name")
    .element() as HTMLInputElement;
}

function redirectUrisTextarea(): HTMLTextAreaElement {
  return page
    .getByTestId("organization-detail-register-redirect-uris")
    .element() as HTMLTextAreaElement;
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId("organization-detail-register-submit").element() as HTMLButtonElement;
}

/**
 * The text of whatever names `control` — the `aria-labelledby` chain when there
 * is one (which is how Base UI names a `Select` trigger, since a `<label for>`
 * cannot name a button), else the `<label for>` pointing at it.
 */
function accessibleName(control: HTMLElement): string {
  const labelledBy: string | null = control.getAttribute("aria-labelledby");

  if (labelledBy !== null && labelledBy !== "") {
    return labelledBy
      .split(" ")
      .map((id: string) => document.querySelector(`#${CSS.escape(id)}`)?.textContent?.trim() ?? "")
      .filter((text: string) => text !== "")
      .join(" ");
  }

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

describe("OrganizationDetail register-client form on @bc-solutions-coder/forms", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson([]);
  });

  it("renders through the forms shell, which leaves validation to the schema", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    const element: HTMLFormElement = formElement();
    expect(element.tagName).toBe("FORM");
    // The zod schema is the only validator, so the browser must not also
    // refuse the submit with a native bubble.
    expect(element.noValidate).toBe(true);
  });

  it("keeps every control under that one shell", async () => {
    // The client-type select is the one at risk: the form reaches it through a
    // nested component, and a catalog field left outside the shell silently
    // stops being part of the submitted values.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    const element: HTMLFormElement = formElement();
    for (const testId of [
      "organization-detail-register-display-name",
      "organization-detail-register-client-type",
      "organization-detail-register-redirect-uris",
      "organization-detail-register-submit",
    ]) {
      const control: HTMLElement = page.getByTestId(testId).element() as HTMLElement;
      expect(control.closest("form"), `${testId} must live under the form shell`).toBe(element);
    }
  });

  it("labels the display-name input, the client-type select and the redirect-URI textarea", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    const clientType: HTMLElement = page
      .getByTestId("organization-detail-register-client-type")
      .element() as HTMLElement;
    expect(accessibleName(displayNameInput())).toBe("Display name");
    expect(accessibleName(clientType)).toBe("Client type");
    expect(accessibleName(redirectUrisTextarea())).toBe("Redirect URIs");
  });

  it("keeps the redirect-URI control a real textarea", async () => {
    // The newline-separated list only works if the control still accepts a
    // newline — a catalog field that resolved to an `<input>` would quietly turn
    // the multi-URI contract into a single-line one.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    const redirectUris: HTMLTextAreaElement = redirectUrisTextarea();
    expect(redirectUris.tagName).toBe("TEXTAREA");
    // `tagName` alone is satisfied by a hand-rolled `<textarea>` too; these two
    // utilities, which only `textareaRecipe` adds, are what says "the catalog
    // control".
    for (const utility of ["min-h-20", "resize-y"]) {
      expect(redirectUris.classList.contains(utility), utility).toBe(true);
    }
  });

  it("associates a required-display-name message with the input instead of posting an empty name", async () => {
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    const message = page.getByTestId("organization-detail-register-display-name-error");
    await expect.element(message).toBeInTheDocument();
    await expect.element(message).toHaveTextContent("Display name is required");

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(displayNameInput())).toContain(messageId);
    expect(displayNameInput().getAttribute("aria-invalid")).toBe("true");
    expect(registerCalls()).toHaveLength(0);
  });

  it("rejects a whitespace-only display name without reaching the endpoint", async () => {
    // `.trim()` in the schema is what makes `"   "` fail the `min(1)`; a bare
    // `min(1)` would let three spaces through and register a nameless client.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(page.getByTestId("organization-detail-register-display-name"), "   ");
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-display-name-error"))
      .toHaveTextContent("Display name is required");
    expect(registerCalls()).toHaveLength(0);
  });

  it("disables the controls and the submit while the registration is in flight", async () => {
    // Scoped to the POST: the screen's three reads must still settle, or the
    // form would never paint to be driven in the first place.
    seedLoadedOrg(neverSettles());

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    // Wait for the request to REACH the transport first: the harness records a
    // call before its responder runs, so this is the earliest point at which
    // "in flight" is a fact rather than a race.
    await vi.waitFor(() => {
      expect(registerCalls()).toHaveLength(1);
    });
    await expect.poll(() => displayNameInput().disabled).toBe(true);
    expect(redirectUrisTextarea().disabled).toBe(true);
    // A second registration would mint a second client secret, and only one of
    // the two would ever be shown.
    expect(submitButton().disabled).toBe(true);
  });

  it("shows a validation failure's per-property message on the field, not the banner", async () => {
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { RedirectUris: ["'nope' is not an absolute URI."] },
        },
        400,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.type(page.getByTestId("organization-detail-register-redirect-uris"), "nope");
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris-error"))
      .toHaveTextContent("'nope' is not an absolute URI.");
    // Everything landed on a field, so the banner would only repeat it.
    expect(page.getByTestId("organization-detail-register-error").elements()).toHaveLength(0);
  });

  it("keeps a message for a property the form has no field for in the banner", async () => {
    // The remap means a `Name` message has no field to land on; without the
    // banner fallback the API's own sentence never reaches the screen.
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { Name: ["A client with that name is already registered."] },
        },
        400,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-error"))
      .toHaveTextContent("A client with that name is already registered.");
  });

  it("surfaces a form-level failure's own sentence rather than a hardcoded one", async () => {
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/403",
          title: "Forbidden",
          status: 403,
          detail: "This organization has reached its client limit.",
        },
        403,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-error"))
      .toHaveTextContent("This organization has reached its client limit.");
  });

  it("clears a server field error on the next submit rather than wedging the form", async () => {
    // Nothing in the form framework clears an `onServer` error by itself, so the
    // very next submit would otherwise fail the validity gate silently and never
    // reach the endpoint again — with no message to explain why.
    seedLoadedOrg(
      failsWith(
        {
          type: "https://httpstatuses.io/400",
          title: "One or more validation errors occurred.",
          status: 400,
          errors: { RedirectUris: ["'nope' is not an absolute URI."] },
        },
        400,
      ),
    );

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.type(page.getByTestId("organization-detail-register-redirect-uris"), "nope");
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

  it("still posts the remapped body, and still keeps clientType off the wire", async () => {
    // The only statement of the register wire contract anywhere.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.fill(
      redirectUrisTextarea(),
      "https://a.example/cb\n\n  https://b.example/cb  \n",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await vi.waitFor(() => {
      expect(registerCalls()).toHaveLength(1);
    });
    expect(registerCalls()[0]?.body).toEqual({
      name: "Dashboard",
      redirectUris: ["https://a.example/cb", "https://b.example/cb"],
      postLogoutRedirectUris: [],
      tenantId: "o1",
    });
  });

  it("still reveals the one-time client id and secret after a successful registration", async () => {
    // The secret is issued exactly once and can never be re-fetched, so a
    // reveal gate that never fires loses it outright.
    seedLoadedOrg();

    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await userEvent.type(
      page.getByTestId("organization-detail-register-display-name"),
      "Dashboard",
    );
    await userEvent.click(page.getByTestId("organization-detail-register-submit"));

    await expect
      .element(page.getByTestId("organization-detail-register-success"))
      .toBeInTheDocument();
    await expect
      .element(page.getByTestId("organization-detail-register-client-id"))
      .toHaveTextContent("client-abc");
    await expect
      .element(page.getByTestId("organization-detail-register-client-secret"))
      .toHaveTextContent("secret-xyz");
  });
});
