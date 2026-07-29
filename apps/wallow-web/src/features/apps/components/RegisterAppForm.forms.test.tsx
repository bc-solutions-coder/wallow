import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { RegisterAppForm } from "./RegisterAppForm";

/**
 * The register-app form ON `@bc-solutions-coder/forms` (Wallow-ov6w.4.2).
 *
 * WHY THIS IS A FOURTH FILE. The three specs already beside this one are the
 * form's frozen oracles and the migration's acceptance criterion is that all
 * three pass UNCHANGED, so none of them is edited here: `RegisterAppForm.test.tsx`
 * pins the behaviour (the `clientName`/`requestedScopes` remap, the newline-split
 * URI lists, the Apps tag sweep, the one-time secret, the RFC 7807 banner),
 * `RegisterAppForm.catalog.test.tsx` pins the client-type `Select` and the scope
 * `ToggleGroup`, and `RegisterAppForm.branding.test.tsx` pins the uncontrolled
 * branding subsection. What none of them can say, because all three predate the
 * package, is anything about the shell the form is built ON. This file says
 * that, and only that.
 *
 * WHAT THE MIGRATION ADDS (these fail against the hand-rolled form):
 *
 *   1. The `<form>` is the package's `AppForm`, so it is `noValidate` — the zod
 *      schema owns validation and the browser must not double-validate and pop a
 *      native bubble over the field message. The hand-rolled form has no such
 *      attribute.
 *   2. Every migrated control has a real, associated label. Today none of the
 *      four has one at all: a screen reader hears an unnamed textbox, an unnamed
 *      combobox and two more unnamed textboxes. The catalog fields render the ui
 *      `Field` row that fixes it.
 *   3. The required-field message is genuinely ASSOCIATED with the display-name
 *      input (`aria-invalid` + `aria-describedby`), which the hand-rolled
 *      `ErrorBanner` sibling never was.
 *   4. The controls and the submit disable themselves while the registration is
 *      in flight, so a second click cannot register a second app and an edit
 *      cannot race the request it was typed into. Today nothing disables — and
 *      this form is the one where a double submit costs the most, because each
 *      success mints a client secret that is shown exactly once.
 *   5. A validation failure's per-property messages land NEXT TO the control
 *      they belong to — `splitServerError` routes the RFC 7807 `errors` member
 *      onto the fields the form actually has, and keeps the rest in the banner.
 *      Today `errors` is dropped on the floor and the banner shows the generic
 *      fallback. This form is also where the SPLIT is genuinely interesting: it
 *      remaps `displayName` to `clientName` on the wire, so `RedirectUris` comes
 *      back to a field that exists while `ClientName` comes back to one that does
 *      not, and only the first may leave the banner.
 *   6. A server field error must not WEDGE the form: the shell clears it on the
 *      way into the next submit, or every later submit would fail the validity
 *      gate silently and never reach the endpoint again.
 *
 * WHAT THE MIGRATION MUST NOT DROP (these pass today — regression guards aimed
 * squarely at the parts that move house):
 *
 *   7. The required message keeps its exact wording. It moves from a hand-written
 *      `value.trim() ? undefined : "Display name is required"` validator into the
 *      zod schema, and the oracle only asserts that the message element appears.
 *   8. A whitespace-only display name is still rejected. That is what the
 *      hand-written validator's `.trim()` did, and a zod schema only keeps it if
 *      the trim is part of the schema.
 *   9. The scope toggles and the branding subsection stay INSIDE the shell. They
 *      are deliberately not catalog fields — forms has no multi-select-toggle
 *      field and the branding block is uncontrolled and wired to nothing — so
 *      they ride the `AppField` render-prop escape hatch and a plain child
 *      respectively. Neither may fall out of the `<form>` while the element
 *      around them is being replaced.
 *  10. The success view REPLACES the form rather than joining it. Today that is
 *      gated on `mutation.isSuccess`; after the migration the raw mutation is
 *      owned by `useAppForm` and the gate becomes state captured in its
 *      `onSuccess`. The oracle asserts the secret is revealed, not that the form
 *      is gone, so the swap needs its own guard.
 *
 * The submitted body, the tag sweep and the one-time secret are deliberately NOT
 * restated here — the oracle pins all three, and a second copy would only create
 * something to keep in sync.
 *
 * Same seam as the oracle: the REAL SDK with only its `fetch` faked
 * (`@bc-solutions-coder/testing/sdk-harness`), real router context via
 * `renderWithWallow`, real headless Chromium.
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
 * Written as a name lookup rather than a `control.labels` read for exactly that
 * reason: three of the four migrated controls are labelled one way and the
 * fourth the other, and the claim is about the NAME, not the mechanism.
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
    // The scope toggles ride the `AppField` render-prop escape hatch (forms has
    // no multi-select-toggle field) and the branding block is an uncontrolled
    // child wired to nothing. Both are easy to leave behind while the element
    // around them is replaced, and either would silently stop being submitted
    // with the rest.
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
    // the multi-URI contract the oracle asserts into a single-line one.
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
    // The string moves from a hand-written validator into the zod schema during
    // the migration, and the oracle asserts only that the element appears.
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect
      .element(page.getByTestId("app-display-name-error"))
      .toHaveTextContent("Display name is required");
  });

  it("rejects a whitespace-only display name without reaching the endpoint", async () => {
    // The hand-written validator trimmed before testing for emptiness; a zod
    // schema only keeps that behaviour if the trim is part of the schema.
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
    // The wire name is `clientName`; the form's field is `displayName`. The
    // remap means this message has nowhere to land, and dropping it — which is
    // what happens today — would leave the user staring at a generic fallback
    // while the API had said exactly what was wrong.
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

    // Nothing in the form framework clears an `onServer` error by itself, so the
    // very next submit would otherwise fail the validity gate silently and never
    // reach the endpoint again — with no message to explain why.
    harness.resolveJson(OK_RESPONSE);
    await userEvent.click(page.getByTestId("app-register-submit"));

    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(2);
    });
    await expect.element(page.getByTestId("app-register-success")).toBeInTheDocument();
  });

  it("replaces the form with the one-time reveal after a successful registration", async () => {
    // The secret itself is the oracle's claim; this one is about the SWAP. The
    // gate moves from `mutation.isSuccess` to state captured in the hook's
    // `onSuccess` during the migration, and a gate that only ADDED the reveal
    // would leave a live form beside a secret that can never be shown again.
    renderWithWallow(<RegisterAppForm />, { harness });

    await userEvent.type(page.getByTestId("app-display-name"), "My App");
    await userEvent.click(page.getByTestId("app-register-submit"));

    await expect.element(page.getByTestId("app-register-success")).toBeInTheDocument();
    await expect.element(page.getByTestId("app-register-form")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("app-register-submit")).not.toBeInTheDocument();
  });
});
