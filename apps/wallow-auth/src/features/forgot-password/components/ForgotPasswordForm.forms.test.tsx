import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { ForgotPasswordForm } from "./ForgotPasswordForm";

/**
 * Forgot-password screen on `@bc-solutions-coder/forms`: the form-shell
 * behaviours its sibling `ForgotPasswordForm.test.tsx` does not cover — derived
 * testids, the required message's wiring and wording, the submit's two labels,
 * when validation fires, and the in-flight disabled state.
 *
 * A zod `.trim()` does not trim what is submitted: the standard-schema adapter
 * keeps only the issues and discards the parsed output, so the submit callback
 * sees the RAW value and the screen's explicit trim has to stay.
 */

const EMAIL = "ada@example.com";
const PADDED_EMAIL = `  ${EMAIL}  `;
const ENDPOINT = "/v1/identity/auth/forgot-password";

let harness: SdkHarness;

function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

function emailInput(): HTMLInputElement {
  return page.getByTestId("forgot-password-email").element() as HTMLInputElement;
}

/**
 * Split rather than compared whole: Base UI appends the error to whatever else
 * already describes the control, so the claim is that the message is AMONG them.
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
    // The field and the submit must derive from the SAME prefix, or the shell is
    // present but not the thing the ids come from.
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
    // Nothing else pins the string — the sibling spec asserts only that the
    // message element appears.
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.click(page.getByTestId("forgot-password-submit"));

    await expect
      .element(page.getByTestId("forgot-password-email-error"))
      .toHaveTextContent("Email is required");
  });

  it("trims the address before it reaches the endpoint", async () => {
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

  it("says nothing while a first-time visitor is still typing the address", async () => {
    // `"  "` genuinely breaks the schema's trimmed `min(1)`, so the only thing
    // keeping the message away is WHEN the shared hook validates.
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.fill(page.getByTestId("forgot-password-email"), "  ");
    await expect.poll(() => emailInput().value).toBe("  ");

    expect(page.getByTestId("forgot-password-email-error").query()).toBeNull();
  });

  it("re-validates as the user types once the submit has already flagged the field", async () => {
    // No second submit happens, which the untouched transport proves.
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.click(page.getByTestId("forgot-password-submit"));
    await expect
      .element(page.getByTestId("forgot-password-email-error"))
      .toHaveTextContent("Email is required");

    await user.fill(page.getByTestId("forgot-password-email"), EMAIL);
    await expect.poll(() => page.getByTestId("forgot-password-email-error").query()).toBeNull();

    await user.fill(page.getByTestId("forgot-password-email"), "");

    await expect
      .element(page.getByTestId("forgot-password-email-error"))
      .toHaveTextContent("Email is required");
    expect(harness.calls).toHaveLength(0);
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

    // Wait for the request to REACH the transport before asserting: releasing
    // into the gap before `fetch` is called leaves the never-settling responder
    // installed.
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
