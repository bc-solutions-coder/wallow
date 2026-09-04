import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { chooseOption } from "@bc-solutions-coder/testing/catalog-select";
import { CreateInquiryForm } from "./CreateInquiryForm";

/**
 * The create-inquiry form as built on `@bc-solutions-coder/forms`: the shell,
 * its labels and message associations, in-flight disabling, and how a server
 * error is routed onto a field or into the banner.
 *
 * The `<form>` is stamped `inquiry-create-form` while every field, the submit
 * and the banner use the bare `inquiry` prefix. One derivation cannot produce
 * both, which is why `AppForm` carries an explicit `testId` beside
 * `testIdPrefix`.
 *
 * Runs the real SDK over a faked fetch (sdk-harness). Nothing is mocked.
 */

/** A valid value for every field, keyed by testid. */
const FIELD_FILLERS: Record<string, () => Promise<void>> = {
  "inquiry-name": () => userEvent.fill(page.getByTestId("inquiry-name"), "Ada Lovelace"),
  "inquiry-email": () => userEvent.fill(page.getByTestId("inquiry-email"), "ada@example.com"),
  "inquiry-phone": () => userEvent.fill(page.getByTestId("inquiry-phone"), "555-0100"),
  "inquiry-company": () =>
    userEvent.fill(page.getByTestId("inquiry-company"), "Analytical Engines"),
  "inquiry-project-type": () => chooseOption("inquiry-project-type", "Web Application"),
  "inquiry-budget-range": () => chooseOption("inquiry-budget-range", "$15,000 - $50,000"),
  "inquiry-timeline": () => chooseOption("inquiry-timeline", "1 - 3 months"),
  "inquiry-message": () =>
    userEvent.fill(page.getByTestId("inquiry-message"), "We need a project dashboard."),
};

/** The seven fields `SubmitInquiryValidator.cs` marks `.NotEmpty()`, and their messages. */
const REQUIRED_ERROR_TEST_IDS: readonly string[] = [
  "inquiry-name-error",
  "inquiry-email-error",
  "inquiry-phone-error",
  "inquiry-project-type-error",
  "inquiry-budget-range-error",
  "inquiry-timeline-error",
  "inquiry-message-error",
];

/** Every control the form owns, with the label each must answer to. */
const LABELLED_CONTROLS: ReadonlyArray<readonly [testId: string, label: string]> = [
  ["inquiry-name", "Name"],
  ["inquiry-email", "Email"],
  ["inquiry-phone", "Phone"],
  ["inquiry-project-type", "Project type"],
  ["inquiry-budget-range", "Budget range"],
  ["inquiry-timeline", "Timeline"],
  ["inquiry-message", "Message"],
];

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

function elementByTestId(testId: string): HTMLElement {
  return page.getByTestId(testId).element() as HTMLElement;
}

function formElement(): HTMLFormElement {
  return elementByTestId("inquiry-create-form") as HTMLFormElement;
}

function nameInput(): HTMLInputElement {
  return elementByTestId("inquiry-name") as HTMLInputElement;
}

function messageTextarea(): HTMLTextAreaElement {
  return elementByTestId("inquiry-message") as HTMLTextAreaElement;
}

function submitButton(): HTMLButtonElement {
  return elementByTestId("inquiry-submit") as HTMLButtonElement;
}

async function fillEveryField(): Promise<void> {
  for (const fill of Object.values(FIELD_FILLERS)) {
    await fill();
  }
}

/** Fill every field with a valid value except `skipTestId`, which is left alone. */
async function fillEveryFieldExcept(skipTestId: string): Promise<void> {
  for (const [testId, fill] of Object.entries(FIELD_FILLERS)) {
    if (testId !== skipTestId) {
      await fill();
    }
  }
}

/**
 * The text of whatever names `control` — the `aria-labelledby` chain when there
 * is one (how Base UI names a `Select` trigger, since a `<label for>` cannot
 * name a button), else the `<label for>` pointing at it. A name lookup rather
 * than a `control.labels` read because the controls are labelled both ways and
 * the claim is about the NAME, not the mechanism.
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

/** Assert `control` is described by the message element carrying `errorTestId`. */
function expectDescribedByMessage(control: HTMLElement, errorTestId: string): void {
  const messageId: string = elementByTestId(errorTestId).id;

  expect(messageId, `${errorTestId} needs an id to be referenced by`).not.toBe("");
  expect(describedByIds(control)).toContain(messageId);
  expect(control.getAttribute("aria-invalid")).toBe("true");
}

describe("CreateInquiryForm on @bc-solutions-coder/forms", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("renders through the forms shell, which leaves validation to the schema", async () => {
    renderWithWallow(<CreateInquiryForm />, { harness });

    await expect.element(page.getByTestId("inquiry-create-form")).toBeInTheDocument();

    const element: HTMLFormElement = formElement();
    expect(element.tagName).toBe("FORM");
    // The zod schema is the only validator, so the browser must not also refuse
    // the submit with a native bubble over the field message.
    expect(element.noValidate).toBe(true);
  });

  it("keeps the form's own testid on `-create-` while every control stays on the bare prefix", async () => {
    // The C# E2E `InquiryPage` page object selects both spellings verbatim.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await expect.element(page.getByTestId("inquiry-create-form")).toBeInTheDocument();

    const element: HTMLFormElement = formElement();
    expect(element.dataset.testid).toBe("inquiry-create-form");

    for (const testId of [...Object.keys(FIELD_FILLERS), "inquiry-submit"]) {
      const control: HTMLElement = elementByTestId(testId);
      expect(control.closest("form"), `${testId} must live under the form shell`).toBe(element);
    }
  });

  it("labels every control the form owns", async () => {
    renderWithWallow(<CreateInquiryForm />, { harness });

    await expect.element(page.getByTestId("inquiry-name")).toBeInTheDocument();

    for (const [testId, label] of LABELLED_CONTROLS) {
      expect(accessibleName(elementByTestId(testId)), testId).toBe(label);
    }
  });

  it("marks company — the one server-optional field — optional in its label", async () => {
    renderWithWallow(<CreateInquiryForm />, { harness });

    await expect.element(page.getByTestId("inquiry-company")).toBeInTheDocument();

    const name: string = accessibleName(elementByTestId("inquiry-company"));
    expect(name).toContain("Company");
    expect(name.toLowerCase()).toContain("optional");
  });

  it("associates each validation message with the control it belongs to", async () => {
    renderWithWallow(<CreateInquiryForm />, { harness });

    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect.element(page.getByTestId("inquiry-name-error")).toBeInTheDocument();

    expectDescribedByMessage(nameInput(), "inquiry-name-error");
    expectDescribedByMessage(messageTextarea(), "inquiry-message-error");
    // A `Select` trigger is a button, so it carries the field row's invalid
    // state as a data attribute rather than through a `<label for>` pair.
    expect(Object.hasOwn(elementByTestId("inquiry-project-type").dataset, "invalid")).toBe(true);
  });

  it("words every required-field message exactly as the validator always has", async () => {
    // All seven messages are the SAME sentence: one shared zod refinement.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await userEvent.click(page.getByTestId("inquiry-submit"));

    for (const errorTestId of REQUIRED_ERROR_TEST_IDS) {
      await expect
        .element(page.getByTestId(errorTestId))
        .toHaveTextContent("This field is required");
    }
  });

  it("rejects a whitespace-only value without reaching the endpoint", async () => {
    // `.trim()` has to be part of the schema, or `"   "` passes the non-empty
    // check and three spaces reach the endpoint.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryFieldExcept("inquiry-name");
    await userEvent.fill(page.getByTestId("inquiry-name"), "   ");
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect
      .element(page.getByTestId("inquiry-name-error"))
      .toHaveTextContent("This field is required");
    expect(harness.calls).toHaveLength(0);
  });

  it("keeps the message control a real textarea, now on the catalog recipe", async () => {
    // A catalog field resolving to an `<input>` would silently turn a paragraph
    // into a single line. `min-h-20` / `resize-y` are the two utilities only
    // `textareaRecipe` adds, so they are what says "the CATALOG control" rather
    // than a hand-copied class string.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await expect.element(page.getByTestId("inquiry-message")).toBeInTheDocument();

    const message: HTMLTextAreaElement = messageTextarea();
    expect(message.tagName).toBe("TEXTAREA");
    for (const utility of ["min-h-20", "resize-y"]) {
      expect(message.classList.contains(utility), utility).toBe(true);
    }
  });

  it("disables every control and the submit while the inquiry is in flight", async () => {
    harness.pending();
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryField();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    // Wait for the request to REACH the transport: the harness records a call
    // before its responder runs, so this is the earliest point at which "in
    // flight" is a fact rather than a race.
    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(1);
    });

    await expect.poll(() => nameInput().disabled).toBe(true);
    for (const testId of ["inquiry-email", "inquiry-phone", "inquiry-company"]) {
      expect((elementByTestId(testId) as HTMLInputElement).disabled, testId).toBe(true);
    }
    expect(messageTextarea().disabled).toBe(true);
    for (const testId of ["inquiry-project-type", "inquiry-budget-range", "inquiry-timeline"]) {
      expect(Object.hasOwn(elementByTestId(testId).dataset, "disabled"), testId).toBe(true);
    }
    expect(submitButton().disabled).toBe(true);
  });

  it("posts the value the user typed, untrimmed", async () => {
    // `.trim()` in the schema GATES a whitespace-only submit; it must not
    // rewrite the payload.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryFieldExcept("inquiry-name");
    await userEvent.fill(page.getByTestId("inquiry-name"), "  Ada Lovelace  ");
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });

    const body = harness.last?.body as { name: string } | undefined;
    expect(body?.name).toBe("  Ada Lovelace  ");
  });

  it("shows a validation failure's per-property message on the field, not the banner", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
        code: "Validation.Failed",
        status: 400,
        errors: { ProjectType: ["'other' is not a recognised project type."] },
      },
      400,
    );

    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryField();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect
      .element(page.getByTestId("inquiry-project-type-error"))
      .toHaveTextContent("'other' is not a recognised project type.");
    // Everything landed on a field, so the banner would only repeat it.
    expect(page.getByTestId("inquiry-error").elements()).toHaveLength(0);
  });

  it("keeps a message for a property the form has no field for in the banner", async () => {
    // `splitServerError` matches on the form's own value keys, so a property the
    // form does not hold has nowhere to land but the banner.
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
        code: "Validation.Failed",
        status: 400,
        errors: { Captcha: ["Captcha verification failed."] },
      },
      400,
    );

    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryField();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect
      .element(page.getByTestId("inquiry-error"))
      .toHaveTextContent("Captcha verification failed.");
  });

  it("clears a server field error on the next submit rather than wedging the form", async () => {
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
        code: "Validation.Failed",
        status: 400,
        errors: { ProjectType: ["'other' is not a recognised project type."] },
      },
      400,
    );

    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryField();
    await userEvent.click(page.getByTestId("inquiry-submit"));
    await expect.element(page.getByTestId("inquiry-project-type-error")).toBeInTheDocument();

    // Nothing in the form framework clears an `onServer` error by itself, so the
    // shell has to: otherwise the next submit fails the validity gate silently
    // and never reaches the endpoint again, with no message to explain why.
    harness.resolveJson({});
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(2);
    });
    await expect.element(page.getByTestId("inquiry-success")).toBeInTheDocument();
  });

  it("falls back to the form's own sentence when the failure carries no detail", async () => {
    // `useAppForm`'s `fallbackError` supplies this sentence; without it the
    // transport's own HTTP message wins.
    harness.rejectJson(
      { type: "https://httpstatuses.io/500", code: "Server.Error", title: "Server error" },
      500,
    );

    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryField();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect
      .element(page.getByTestId("inquiry-error"))
      .toHaveTextContent("Could not submit the inquiry.");
  });

  it("replaces the form AND its heading with the success state", async () => {
    // A gate that only ADDED the thank-you would leave a live form under it,
    // inviting a duplicate inquiry.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryField();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect.element(page.getByTestId("inquiry-success")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-create-form")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-create-heading")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-submit")).not.toBeInTheDocument();
  });
});
