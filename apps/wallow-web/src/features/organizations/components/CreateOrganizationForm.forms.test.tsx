import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { CreateOrganizationForm } from "./CreateOrganizationForm";

/**
 * The create-organization form as built on `@bc-solutions-coder/forms`: the
 * `AppForm` shell, the labelled field, validation messages associated with the
 * input, in-flight disabling, and the RFC 7807 field/banner split.
 *
 * Runs the real SDK over a faked fetch (`createSdkHarness`) mounted on the
 * router context.
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
 * The ids `control` points its `aria-describedby` at. Split rather than
 * compared whole: Base UI appends the message to whatever else already
 * describes the control, so the claim is that it is AMONG them, not alone.
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
    // The zod schema is the only validator, so the browser must not also
    // refuse the submit with a native bubble.
    expect(element.noValidate).toBe(true);
    // A form element that is not the one the field and submit sit under would
    // satisfy the check above while owning none of the ids.
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
    renderWithWallow(<CreateOrganizationForm />, { harness });

    await userEvent.click(page.getByTestId("organization-create-submit"));

    await expect
      .element(page.getByTestId("organization-name-error"))
      .toHaveTextContent("Name is required");
  });

  it("rejects a whitespace-only name without reaching the endpoint", async () => {
    // `.trim()` in the schema is what makes `"   "` fail the `min(1)`; a bare
    // `min(1)` would let three spaces through.
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
