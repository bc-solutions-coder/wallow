import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { chooseOption } from "../../../test/catalog-select";
import { CreateInquiryForm } from "./CreateInquiryForm";

/**
 * The create-inquiry form ON `@bc-solutions-coder/forms` (Wallow-ov6w.4.3).
 *
 * WHY THIS IS A FOURTH FILE. The three specs already beside this one are the
 * form's frozen oracles and the migration's acceptance criterion is that they
 * keep passing: `CreateInquiryForm.test.tsx` pins the behaviour (every field
 * testid, the submitted `SubmitInquiryBody`, the Inquiries tag sweep, the
 * required-field parity with `SubmitInquiryValidator.cs`, the RFC 7807 banner),
 * `CreateInquiryForm.catalog.test.tsx` pins the three catalog `Select`s by the
 * LABEL a user reads, and `CreateInquiryForm.restyle.test.tsx` pins the chrome.
 * What none of them can say, because all three predate the package, is anything
 * about the shell the form is built ON. This file says that, and only that.
 *
 * THE TESTID SPLIT THIS FORM IS THE REASON FOR. The `<form>` is stamped
 * `inquiry-create-form` while every field, the submit and the banner use the
 * bare `inquiry` prefix (`inquiry-name`, `inquiry-submit`, `inquiry-error`). The
 * derivation alone cannot produce both, which is why `AppForm` carries an
 * explicit `testId` override next to `testIdPrefix` — case 2 pins the pair.
 *
 * WHAT THE MIGRATION ADDS (these fail against the hand-rolled form):
 *
 *   1. The `<form>` is the package's `AppForm`, so it is `noValidate` — the zod
 *      schema owns validation and the browser must not double-validate and pop a
 *      native bubble over the field message.
 *   2. All eight controls get a real, associated label. Today not one of them
 *      has any: a screen reader hears four unnamed textboxes, three unnamed
 *      comboboxes and an unnamed multi-line textbox.
 *   3. `company` — the ONE server-nullable field (`SubmitInquiryCommand.Company`
 *      is `string?`) — says so in its label instead of being the only field a
 *      user can discover is optional by submitting the form.
 *   4. Each validation message is genuinely ASSOCIATED with its control
 *      (`aria-invalid` + `aria-describedby`, `data-invalid` on a select trigger),
 *      which the hand-rolled `ErrorBanner` sibling never was. This is the
 *      error-surface unification: per-field messages move from `ErrorBanner` to
 *      the catalog field's `Field.Error` while KEEPING their testids.
 *   5. The message control becomes the catalog `Textarea` rather than a bare
 *      `<textarea>` carrying a hand-copied class string (see the note on the
 *      restyle spec's `CONTROL` constant below).
 *   6. Every control and the submit disable while the submit is in flight, so a
 *      second click cannot file a second inquiry.
 *   7. A validation failure's per-property messages land NEXT TO the control they
 *      belong to — `splitServerError` routes the RFC 7807 `errors` member onto
 *      the fields the form has and keeps the rest in the banner. Today `errors`
 *      is dropped on the floor entirely.
 *   8. A server field error must not WEDGE the form: the shell clears it on the
 *      way into the next submit, or every later submit would fail the validity
 *      gate silently and never reach the endpoint again.
 *   9. A failure carrying no `detail` shows the form's own sentence rather than
 *      the transport's message, because the fallback moves into `useAppForm`'s
 *      `fallbackError` where `errorText`'s `error.message` branch used to win.
 *
 * WHAT THE MIGRATION MUST NOT DROP (these pass today — regression guards aimed
 * squarely at the parts that move house):
 *
 *  10. The `<form>` testid stays `inquiry-create-form` while the fields stay on
 *      `inquiry`, and every control stays under that one element.
 *  11. The required message keeps its exact wording, uniformly, on all seven
 *      required fields. It moves from a hand-written `value.trim() ? undefined :
 *      "This field is required"` validator into the zod schema, and the oracle
 *      only asserts that the message element appears.
 *  12. A whitespace-only value is still rejected — what the hand-written
 *      validator's `.trim()` did, which a zod schema only keeps if the trim is
 *      part of the schema.
 *  13. The submitted value is NOT trimmed. `.trim()` in the schema gates the
 *      whitespace-only submit; it must not start rewriting the payload, which
 *      the oracle's exact-body assertion would not catch for a padded value.
 *  14. The success state REPLACES the form AND its heading. Today that is gated
 *      on `mutation.isSuccess`; after the migration the raw mutation is owned by
 *      `useAppForm` and the gate becomes state captured in its `onSuccess`.
 *
 * The submitted body, the tag sweep and the option sets are deliberately NOT
 * restated here — the oracles pin all three, and a second copy would only create
 * something to keep in sync.
 *
 * Same seam as the oracles: the REAL SDK with only its `fetch` faked
 * (`@bc-solutions-coder/testing/sdk-harness`), real router context via
 * `renderWithWallow`, real headless Chromium. Nothing is mocked.
 */

/** A valid value for every field, keyed by testid. */
const FIELD_FILLERS: Record<string, () => Promise<void>> = {
  "inquiry-name": () => userEvent.type(page.getByTestId("inquiry-name"), "Ada Lovelace"),
  "inquiry-email": () => userEvent.type(page.getByTestId("inquiry-email"), "ada@example.com"),
  "inquiry-phone": () => userEvent.type(page.getByTestId("inquiry-phone"), "555-0100"),
  "inquiry-company": () =>
    userEvent.type(page.getByTestId("inquiry-company"), "Analytical Engines"),
  "inquiry-project-type": () => chooseOption("inquiry-project-type", "Web Application"),
  "inquiry-budget-range": () => chooseOption("inquiry-budget-range", "$15,000 - $50,000"),
  "inquiry-timeline": () => chooseOption("inquiry-timeline", "1 - 3 months"),
  "inquiry-message": () =>
    userEvent.type(page.getByTestId("inquiry-message"), "We need a project dashboard."),
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
 * is one (which is how Base UI names a `Select` trigger, since a `<label for>`
 * cannot name a button), else the `<label for>` pointing at it.
 *
 * Written as a name lookup rather than a `control.labels` read for exactly that
 * reason: five of the eight controls are labelled one way and three the other,
 * and the claim is about the NAME, not the mechanism.
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
    // The shell's `noValidate`: the zod schema is the only validator, so the
    // browser must not also refuse the submit with a native bubble over the
    // field message.
    expect(element.noValidate).toBe(true);
  });

  it("keeps the form's own testid on `-create-` while every control stays on the bare prefix", async () => {
    // This form is the reason `AppForm` takes an explicit `testId` beside
    // `testIdPrefix`: `inquiry-create-form` and `inquiry-name` cannot both come
    // out of one derivation, and the C# E2E `InquiryPage` page object selects
    // both spellings verbatim.
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
    // `SubmitInquiryCommand.Company` is the only `string?` on the command, and
    // it is the only field with no validator. Today the sole way to learn that
    // is to submit the form and see which six fields complain.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await expect.element(page.getByTestId("inquiry-company")).toBeInTheDocument();

    const name: string = accessibleName(elementByTestId("inquiry-company"));
    expect(name).toContain("Company");
    expect(name.toLowerCase()).toContain("optional");
  });

  it("associates each validation message with the control it belongs to", async () => {
    // The error-surface unification: the message moves out of a sibling
    // `ErrorBanner` — which named nothing and was announced to nobody — into the
    // field's own `Field.Error`, KEEPING the `{field}-error` testid the oracle
    // and the E2E page object select.
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
    // Unlike the wallow-auth forms, all seven messages are the SAME sentence —
    // one shared zod refinement replaces one shared `required` validator, and
    // the oracle asserts only that each element appears.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await userEvent.click(page.getByTestId("inquiry-submit"));

    for (const errorTestId of REQUIRED_ERROR_TEST_IDS) {
      await expect
        .element(page.getByTestId(errorTestId))
        .toHaveTextContent("This field is required");
    }
  });

  it("rejects a whitespace-only value without reaching the endpoint", async () => {
    // The hand-written validator trimmed before testing for emptiness; a zod
    // schema only keeps that behaviour if the trim is part of the schema.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryFieldExcept("inquiry-name");
    await userEvent.type(page.getByTestId("inquiry-name"), "   ");
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect
      .element(page.getByTestId("inquiry-name-error"))
      .toHaveTextContent("This field is required");
    expect(harness.calls).toHaveLength(0);
  });

  it("keeps the message control a real textarea, now on the catalog recipe", async () => {
    // Two claims in one. The newline-accepting control must survive the move (a
    // catalog field resolving to an `<input>` would silently turn a paragraph
    // into a single line), and the look must now come FROM the shared
    // `Textarea` rather than from this app hand-copying a class string — which
    // is what `min-h-20` / `resize-y`, the two utilities only the catalog recipe
    // adds, witness. The restyle spec's `CONTROL` pin is narrowed to the overlap
    // for the same reason its select pin already was.
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

    // Wait for the request to REACH the transport first: the harness records a
    // call before its responder runs, so this is the earliest point at which
    // "in flight" is a fact rather than a race.
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
    // `.trim()` in the schema exists to GATE a whitespace-only submit, not to
    // rewrite the payload — the pre-migration form posted the raw value and the
    // oracle's exact-body assertion only ever uses unpadded input.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryFieldExcept("inquiry-name");
    await userEvent.type(page.getByTestId("inquiry-name"), "  Ada Lovelace  ");
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.method).toBe("POST");
    });

    const body = harness.last?.body as { name: string } | undefined;
    expect(body?.name).toBe("  Ada Lovelace  ");
  });

  it("shows a validation failure's per-property message on the field, not the banner", async () => {
    // Today the RFC 7807 `errors` member is dropped on the floor: the user is
    // told "Could not submit the inquiry." while the API said exactly which
    // field it rejected and why.
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
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
    // form does not hold has nowhere to land — and dropping it would leave the
    // user staring at a generic fallback while the API had been specific.
    harness.rejectJson(
      {
        type: "https://httpstatuses.io/400",
        title: "One or more validation errors occurred.",
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
    // very next submit would otherwise fail the validity gate silently and never
    // reach the endpoint again — with no message to explain why.
    harness.resolveJson({});
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(2);
    });
    await expect.element(page.getByTestId("inquiry-success")).toBeInTheDocument();
  });

  it("falls back to the form's own sentence when the failure carries no detail", async () => {
    // The fallback moves into `useAppForm`'s `fallbackError`. Today `errorText`
    // prefers the transport's own `message` over the caller's fallback, so a
    // detail-less failure shows the user an HTTP sentence instead of the one
    // sentence this form wrote for exactly this case.
    harness.rejectJson({ type: "https://httpstatuses.io/500", title: "Server error" }, 500);

    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryField();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect
      .element(page.getByTestId("inquiry-error"))
      .toHaveTextContent("Could not submit the inquiry.");
  });

  it("replaces the form AND its heading with the success state", async () => {
    // The success copy is the restyle spec's claim; this one is about the SWAP.
    // The gate moves from `mutation.isSuccess` to state captured in the hook's
    // `onSuccess` during the migration, and a gate that only ADDED the thank-you
    // would leave a live form under it, inviting a duplicate inquiry.
    renderWithWallow(<CreateInquiryForm />, { harness });

    await fillEveryField();
    await userEvent.click(page.getByTestId("inquiry-submit"));

    await expect.element(page.getByTestId("inquiry-success")).toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-create-form")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-create-heading")).not.toBeInTheDocument();
    await expect.element(page.getByTestId("inquiry-submit")).not.toBeInTheDocument();
  });
});
