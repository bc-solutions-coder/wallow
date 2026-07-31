import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { RegisterAppForm } from "./RegisterAppForm";

/**
 * The register-app form on `@bc-solutions-coder/forms` — the shell it is built on.
 *
 * `displayName` is remapped to `clientName` on the wire, so an RFC 7807
 * `ClientName` message has no field to land on and stays in the banner. Nothing
 * clears an `onServer` error by itself, so the shell must clear it on the way
 * into the next submit or every later submit fails the validity gate silently.
 */

/** The register response the harness answers a successful submit with. */
const OK_RESPONSE = {
  clientId: "client-abc",
  clientSecret: "secret-xyz",
  registrationAccessToken: "rat-123",
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function formElement(): HTMLFormElement {
  return page.getByTestId("app-register-form").element() as HTMLFormElement;
}

function displayNameInput(): HTMLInputElement {
  return page.getByTestId("app-display-name").element() as HTMLInputElement;
}

function redirectUrisTextarea(): HTMLTextAreaElement {
  return page.getByTestId("app-redirect-uris").element() as HTMLTextAreaElement;
}

function postLogoutRedirectUrisTextarea(): HTMLTextAreaElement {
  return page.getByTestId("app-post-logout-redirect-uris").element() as HTMLTextAreaElement;
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId("app-register-submit").element() as HTMLButtonElement;
}

/**
 * The text of whatever names `control` — the `aria-labelledby` chain when there
 * is one (which is how Base UI names a `Select` trigger, since a `<label for>`
 * cannot name a button), else the `<label for>` pointing at it.
 *
 * Written as a name lookup rather than a `control.labels` read because the four
 * controls are labelled by two different mechanisms, and the claim is about the
 * NAME, not the mechanism.
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
 * The ids `control` points its `aria-describedby` at. Split rather than compared
 * whole: Base UI appends the message to whatever else already describes the
 * control, so the claim is that it is AMONG them, not that it is alone.
 */
function describedByIds(control: HTMLElement): readonly string[] {
  return (control.getAttribute("aria-describedby") ?? "")
    .split(" ")
    .filter((id: string) => id !== "");
}

describe("RegisterAppForm on @bc-solutions-coder/forms", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    harness.resolveJson(OK_RESPONSE);
  });

  it("renders through the forms shell, which leaves validation to the schema", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-register-form")).toBeInTheDocument();

    const element: HTMLFormElement = formElement();
    expect(element.tagName).toBe("FORM");
    // The shell's `noValidate`: the zod schema is the only validator, so the
    // browser must not also refuse the submit with a native bubble.
    expect(element.noValidate).toBe(true);
  });

  it("keeps every control under that one shell, including the two the catalog does not own", async () => {
    // forms has no multi-select-toggle field, so the scope toggles ride the
    // `AppField` render-prop escape hatch and the branding block is a plain
    // child; either can fall out of the `<form>` and stop being submitted.
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-register-form")).toBeInTheDocument();

    const element: HTMLFormElement = formElement();
    for (const testId of [
      "app-display-name",
      "app-client-type",
      "app-redirect-uris",
      "app-post-logout-redirect-uris",
      "app-scope-inquiries-read",
      "app-scope-offline_access",
      "app-branding",
      "app-branding-display-name",
      "app-logo-input",
      "app-register-submit",
    ]) {
      const control: HTMLElement = page.getByTestId(testId).element() as HTMLElement;
      expect(control.closest("form"), `${testId} must live under the form shell`).toBe(element);
    }
  });

  it("labels the display-name input", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-display-name")).toBeInTheDocument();

    expect(accessibleName(displayNameInput())).toBe("Display name");
  });

  it("labels the client-type select and both redirect-URI textareas", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-client-type")).toBeInTheDocument();

    const clientType: HTMLElement = page.getByTestId("app-client-type").element() as HTMLElement;
    expect(accessibleName(clientType)).toBe("Client type");
    expect(accessibleName(redirectUrisTextarea())).toBe("Redirect URIs");
    expect(accessibleName(postLogoutRedirectUrisTextarea())).toBe("Post-logout redirect URIs");
  });

  it("keeps the redirect-URI controls real textareas", async () => {
    // The newline-separated list only works if the control still accepts a
    // newline — a catalog field that resolved to an `<input>` would quietly turn
    // the multi-URI contract into a single-line one.
    renderWithWallow(<RegisterAppForm />, { harness });

    await expect.element(page.getByTestId("app-redirect-uris")).toBeInTheDocument();

    expect(redirectUrisTextarea().tagName).toBe("TEXTAREA");
    expect(postLogoutRedirectUrisTextarea().tagName).toBe("TEXTAREA");
  });

  it("associates the required-field message with the display-name input", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.click(page.getByTestId("app-register-submit"));

    const message = page.getByTestId("app-display-name-error");
    await expect.element(message).toBeInTheDocument();

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(displayNameInput())).toContain(messageId);
    expect(displayNameInput().getAttribute("aria-invalid")).toBe("true");
  });

  it("words the required-field message exactly as the validator always has", async () => {
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect
      .element(page.getByTestId("app-display-name-error"))
      .toHaveTextContent("Display name is required");
  });

  it("rejects a whitespace-only display name without reaching the endpoint", async () => {
    // The trim has to be part of the zod schema, not applied after it.
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "   ");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect
      .element(page.getByTestId("app-display-name-error"))
      .toHaveTextContent("Display name is required");
    expect(harness.calls).toHaveLength(0);
  });

  it("disables the controls and the submit while the registration is in flight", async () => {
    harness.pending();
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    // Wait for the request to REACH the transport first: the harness records a
    // call before its responder runs, so this is the earliest point at which
    // "in flight" is a fact rather than a race.
    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(1);
    });
    await expect.poll(() => displayNameInput().disabled).toBe(true);
    expect(redirectUrisTextarea().disabled).toBe(true);
    expect(postLogoutRedirectUrisTextarea().disabled).toBe(true);
    // A second registration would mint a second client secret, and only one of
    // the two would ever be shown.
    expect(submitButton().disabled).toBe(true);
  });

  it("shows a validation failure's per-property message on the field, not the banner", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
        status: 400,
        errors: { RedirectUris: ["'nope' is not an absolute URI."] },
      },
      400,
    );

    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.type(page.getByTestId("app-redirect-uris"), "nope");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect
      .element(page.getByTestId("app-redirect-uris-error"))
      .toHaveTextContent("'nope' is not an absolute URI.");
    // Everything landed on a field, so the banner would only repeat it.
    expect(page.getByTestId("app-register-error").elements()).toHaveLength(0);
  });

  it("keeps a message for a property the form has no field for in the banner", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
        status: 400,
        errors: { ClientName: ["An app with that name is already registered."] },
      },
      400,
    );

    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect
      .element(page.getByTestId("app-register-error"))
      .toHaveTextContent("An app with that name is already registered.");
  });

  it("clears a server field error on the next submit rather than wedging the form", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
        status: 400,
        errors: { RedirectUris: ["'nope' is not an absolute URI."] },
      },
      400,
    );

    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.type(page.getByTestId("app-redirect-uris"), "nope");
    await userEvent.click(page.getByTestId("app-register-submit"));
    await expect.element(page.getByTestId("app-redirect-uris-error")).toBeInTheDocument();

    harness.resolveJson(OK_RESPONSE);
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(2);
    });
    await expect.element(page.getByTestId("app-register-success")).toBeInTheDocument();
  });

  it("replaces the form with the one-time reveal after a successful registration", async () => {
    // A gate that only ADDED the reveal would leave a live form beside a secret
    // that can never be shown again.
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-register-success")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-register-form")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("app-register-submit")).not.toBeInTheDocument();
  });
});
