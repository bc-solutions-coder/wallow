import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { CreateOrganizationForm } from "./CreateOrganizationForm";

/**
 * The create-organization form ON `@bc-solutions-coder/forms` (Wallow-ov6w.4.1).
 *
 * WHY THIS IS A SECOND FILE. `CreateOrganizationForm.test.tsx` is the form's
 * frozen behaviour oracle — the `{ name, domain: null }` payload, the
 * Organizations tag sweep, the reset-on-success the cross-app journey leans on,
 * and the RFC 7807 banner — and the migration's acceptance criterion is that it
 * passes UNCHANGED, so it is not edited here. `.restyle.test.tsx` owns the
 * chrome (card, heading, `space-y-6` rhythm) and is likewise untouched. What
 * neither can say, because both predate the package, is anything about the shell
 * the form is built ON. This file says that, and only that.
 *
 * WHAT THE MIGRATION ADDS (these fail against the hand-rolled form):
 *
 *   1. The `<form>` is the package's `AppForm`, so it is `noValidate` — the zod
 *      schema owns validation and the browser must not double-validate and pop a
 *      native bubble over the field message. The hand-rolled form has no such
 *      attribute.
 *   2. The name input has a real, associated `<label>`. Today it has none at
 *      all: a screen reader hears an unnamed textbox. The catalog `TextField`
 *      renders the ui `Field` row that fixes it.
 *   3. The required-field message is genuinely ASSOCIATED with the input
 *      (`aria-invalid` + `aria-describedby`), which the hand-rolled
 *      `ErrorBanner` sibling never was.
 *   4. The input and the submit disable themselves while the create is in
 *      flight, so a second click cannot fire a second create and an edit cannot
 *      race the request it was typed into. Today neither disables.
 *   5. A validation failure's per-property messages land NEXT TO the input
 *      rather than in the form-level banner — `splitServerError` routes the
 *      RFC 7807 `errors` member onto the fields the form actually has. Today
 *      `errors` is dropped on the floor and the banner shows the generic
 *      fallback. That server message must also not WEDGE the form: the shell
 *      clears it on the way into the next submit, or every later submit would
 *      fail the validity gate silently.
 *
 * WHAT THE MIGRATION MUST NOT DROP (these pass today — regression guards aimed
 * at the two things that move house during the migration):
 *
 *   6. The required message keeps its exact wording. It moves from a hand-written
 *      `value.trim() ? undefined : "Name is required"` validator into the zod
 *      schema, and the oracle only asserts that the message element appears.
 *   7. A whitespace-only name is still rejected. That is what the hand-written
 *      validator's `.trim()` did, and a zod schema only keeps it if the trim is
 *      part of the schema.
 *
 * The payload, the tag sweep and the reset-on-success are deliberately NOT
 * restated here — the oracle pins all three, and a second copy would only create
 * something to keep in sync.
 *
 * Same seam as the oracle: the REAL SDK with only its `fetch` faked
 * (`@bc-solutions-coder/testing/sdk-harness`), real router context via
 * `renderWithWallow`, real headless Chromium.
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function nameInput(): HTMLInputElement {
  return page.getByTestId("organization-name").element() as HTMLInputElement;
}

function submitButton(): HTMLButtonElement {
  return page.getByTestId("organization-create-submit").element() as HTMLButtonElement;
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

describe("CreateOrganizationForm on @bc-solutions-coder/forms", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders through the forms shell, which leaves validation to the schema", async () => {
    renderWithWallow(<CreateOrganizationForm />, { harness });

    const form = page.getByTestId("organization-create-form");
    await expect.element(form).toBeInTheDocument();

    const element = form.element() as HTMLFormElement;
    expect(element.tagName).toBe("FORM");
    // The shell's `noValidate`: the zod schema is the only validator, so the
    // browser must not also refuse the submit with a native bubble.
    expect(element.noValidate).toBe(true);
    // The field and the submit must live under that same shell, or the form
    // element is present but is not the thing the ids come from.
    expect(nameInput().closest("form")).toBe(element);
    expect(submitButton().closest("form")).toBe(element);
  });

  it("labels the name input", async () => {
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await expect.element(page.getByTestId("organization-name")).toBeInTheDocument();

    const labels = [...(nameInput().labels ?? [])];
    expect(labels).toHaveLength(1);
    expect(labels[0]?.textContent).toBe("Name");
  });

  it("associates the required-field message with the input", async () => {
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.click(page.getByTestId("organization-create-submit"));

    const message = page.getByTestId("organization-name-error");
    await expect.element(message).toBeInTheDocument();

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(nameInput())).toContain(messageId);
    expect(nameInput().getAttribute("aria-invalid")).toBe("true");
  });

  it("words the required-field message exactly as the validator always has", async () => {
    // The string moves from a hand-written validator into the zod schema during
    // the migration, and the oracle asserts only that the element appears.
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect
      .element(page.getByTestId("organization-name-error"))
      .toHaveTextContent("Name is required");
  });

  it("rejects a whitespace-only name without reaching the endpoint", async () => {
    // The hand-written validator trimmed before testing for emptiness; a zod
    // schema only keeps that behaviour if the trim is part of the schema.
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.type(page.getByTestId("organization-name"), "   ");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect
      .element(page.getByTestId("organization-name-error"))
      .toHaveTextContent("Name is required");
    expect(harness.calls).toHaveLength(0);
  });

  it("disables the name input and the submit while the create is in flight", async () => {
    harness.pending();
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.type(page.getByTestId("organization-name"), "Acme");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    // Wait for the request to REACH the transport first: the harness records a
    // call before its responder runs, so this is the earliest point at which
    // "in flight" is a fact rather than a race.
    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(1);
    });
    await expect.poll(() => nameInput().disabled).toBe(true);
    expect(submitButton().disabled).toBe(true);
  });

  it("shows a validation failure's per-property message on the field, not the banner", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
        status: 400,
        errors: { Name: ["Name must be 3 characters or more."] },
      },
      400,
    );

    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.type(page.getByTestId("organization-name"), "Ac");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect
      .element(page.getByTestId("organization-name-error"))
      .toHaveTextContent("Name must be 3 characters or more.");
    // Everything landed on a field, so the banner would only repeat it.
    expect(page.getByTestId("organization-create-error").elements()).toHaveLength(0);
  });

  it("clears a server field error on the next submit rather than wedging the form", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
        status: 400,
        errors: { Name: ["Name must be 3 characters or more."] },
      },
      400,
    );

    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.type(page.getByTestId("organization-name"), "Ac");
    await userEvent.click(page.getByTestId("organization-create-submit"));
    await expect.element(page.getByTestId("organization-name-error")).toBeInTheDocument();

    // Nothing in the form framework clears an `onServer` error by itself, so a
    // corrected name would otherwise fail the validity gate silently and never
    // reach the endpoint again.
    harness.resolveJson({ id: "11111111-1111-1111-1111-111111111111", name: "Acme" }, 201);
    await userEvent.type(page.getByTestId("organization-name"), "me");
    await userEvent.click(page.getByTestId("organization-create-submit"));

    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(2);
    });
    expect(harness.last?.body).toEqual({ name: "Acme", domain: null });
    await expect.element(page.getByTestId("organization-name-error")).not.toBeInTheDocument();
  });
});
