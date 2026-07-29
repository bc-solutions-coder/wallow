import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../../../test/harness";
import { ForgotPasswordForm } from "./ForgotPasswordForm";

/**
 * The ForgotPassword screen ON `@bc-solutions-coder/forms` (Wallow-ov6w.3.1).
 *
 * WHY THIS IS A SECOND FILE. The sibling `ForgotPasswordForm.test.tsx` is the
 * screen's frozen behaviour oracle — the anti-enumeration contract, ported from
 * Blazor and asserted testid-for-testid. The migration's acceptance criterion is
 * that it passes UNCHANGED, so it is not edited here. What that file cannot say,
 * because it predates the package, is anything about the shell the screen is
 * built ON. This file says it, and only it: what the migration must ADD, and the
 * two behaviours it could silently DROP on the way through.
 *
 * WHAT THE MIGRATION ADDS (these fail against the hand-rolled form):
 *
 *   1. The `<form>` is the package's `AppForm`, so it carries the derived
 *      `{testIdPrefix}-form` testid. That single id is the load-bearing proof
 *      of the whole migration: it can only appear if the screen renders through
 *      `<AppForm testIdPrefix="forgot-password">`, which is also what makes
 *      every field and the submit derive the ids the E2E suite selects.
 *   2. The required-field message is genuinely ASSOCIATED with the input
 *      (`aria-invalid` + `aria-describedby`), which the hand-rolled `<span>`
 *      never was. A screen reader on the old form heard nothing when the submit
 *      bounced; the ui `Field` row the catalog field renders fixes that.
 *   3. The input disables itself while the request is in flight, alongside the
 *      submit the oracle already pins — one click, one email, and no edit can
 *      race the request it was typed into.
 *
 * WHAT THE MIGRATION MUST NOT DROP (these pass today — they are regression
 * guards aimed at the two mistakes this particular migration invites):
 *
 *   4. The address reaches the endpoint TRIMMED. The current screen trims by
 *      hand in its submit callback; a zod `z.string().trim()` does NOT replace
 *      it, because `@tanstack/form-core`'s standard-schema adapter validates
 *      against the form value and throws the parsed output away
 *      (`standardSchemaValidator.js` returns issues only), so the submit
 *      callback still receives the RAW value. The trim has to stay explicit.
 *   5. The required message keeps its exact wording, and the submit keeps both
 *      of its labels. Neither is pinned anywhere else, and both move house
 *      during the migration (into the zod schema and into `SubmitButton`'s
 *      `pendingLabel`).
 *
 * There is deliberately NO error-surface case here: the absence of
 * `forgot-password-error` is the oracle's to assert, in seven places, and
 * restating it would just create a second thing to keep in sync.
 *
 * Same seam as the oracle: the REAL SDK with only its `fetch` faked
 * (`@bc-solutions-coder/testing/sdk-harness`), real router context via
 * `renderWithWallow`, real headless Chromium.
 */

const EMAIL = "ada@example.com";
const PADDED_EMAIL = `  ${EMAIL}  `;
const ENDPOINT = "/v1/identity/auth/forgot-password";

let harness: SdkHarness;

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

function emailInput(): HTMLInputElement {
  return page.getByTestId("forgot-password-email").element() as HTMLInputElement;
}

/**
 * The ids `control` points its `aria-describedby` at. Split rather than compared
 * whole: Base UI appends the error to whatever else already describes the
 * control, so the claim is that the message is AMONG them, not that it is alone.
 */
function describedByIds(control: HTMLElement): readonly string[] {
  const value = control.getAttribute("aria-describedby") ?? "";

  return value.split(" ").filter((id: string) => id !== "");
}

beforeEach(() => {
  harness = createAuthHarness();
  harness.resolveJson({});
});

describe("ForgotPasswordForm on @bc-solutions-coder/forms", () => {
  it("renders through the forms shell, which stamps the derived form testid", async () => {
    await renderWithClient(<ForgotPasswordForm />);

    const form = page.getByTestId("forgot-password-form");

    await expect.element(form).toBeInTheDocument();
    expect(form.element().tagName).toBe("FORM");
    // The field and the submit must derive from the SAME prefix, or the shell
    // is present but not the thing the ids actually come from.
    expect(emailInput().closest("form")).toBe(form.element());
    expect(page.getByTestId("forgot-password-submit").element().closest("form")).toBe(
      form.element(),
    );
  });

  it("associates the required-field message with the input", async () => {
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.click(page.getByTestId("forgot-password-submit"));

    const message = page.getByTestId("forgot-password-email-error");
    await expect.element(message).toBeInTheDocument();

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(emailInput())).toContain(messageId);
    expect(emailInput().getAttribute("aria-invalid")).toBe("true");
  });

  it("words the required-field message exactly as the validator always has", async () => {
    // The string moves from a hand-written validator into the zod schema during
    // the migration, and nothing else pins it — the oracle asserts only that the
    // message element appears.
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.click(page.getByTestId("forgot-password-submit"));

    await expect
      .element(page.getByTestId("forgot-password-email-error"))
      .toHaveTextContent("Email is required");
  });

  it("trims the address before it reaches the endpoint", async () => {
    // A zod `.trim()` does not do this for you: the standard-schema adapter
    // discards the parsed output and the submit callback sees the raw value.
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.type(page.getByTestId("forgot-password-email"), PADDED_EMAIL);
    await user.click(page.getByTestId("forgot-password-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(ENDPOINT);
    });
    expect(harness.last?.body).toEqual({ email: EMAIL });
  });

  it("labels the submit for the idle state", async () => {
    await renderWithClient(<ForgotPasswordForm />);

    await expect
      .element(page.getByTestId("forgot-password-submit"))
      .toHaveTextContent("Send reset link");
  });

  it("disables the email input and relabels the submit while the request is in flight", async () => {
    let release: () => void = () => {};
    harness.respond(
      async () =>
        await new Promise<Response>((resolve) => {
          release = () => {
            resolve(Response.json({}, { status: 200 }));
          };
        }),
    );
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.type(page.getByTestId("forgot-password-email"), EMAIL);
    await user.click(page.getByTestId("forgot-password-submit"));

    // Wait for the request to REACH the transport before asserting, for the same
    // reason the oracle's in-flight case does: releasing into the gap before
    // `fetch` is called would leave the never-settling responder installed.
    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(1);
    });
    await expect.poll(() => emailInput().disabled).toBe(true);
    await expect
      .element(page.getByTestId("forgot-password-submit"))
      .toHaveTextContent("Sending...");

    release();

    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
  });
});
