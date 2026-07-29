import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../../../test/harness";
import { ResetPasswordForm } from "./ResetPasswordForm";

/**
 * The ResetPassword screen ON `@bc-solutions-coder/forms` (Wallow-ov6w.3.2).
 *
 * WHY THIS IS A SECOND FILE. The sibling `ResetPasswordForm.test.tsx` is the
 * screen's frozen behaviour oracle — nineteen cases ported from Blazor, testid
 * for testid, including all four error paths. The migration's acceptance
 * criterion is that it passes UNCHANGED, so it is not edited here. What it
 * cannot say, because it predates the package, is anything about the shell the
 * screen is built ON, and it has three blind spots that this particular
 * migration walks straight into. This file says those, and only those.
 *
 * WHAT THE MIGRATION ADDS (these fail against the hand-rolled form):
 *
 *   1. The `<form>` is the package's `AppForm`, so it carries the derived
 *      `{testIdPrefix}-form` testid. That single id is the load-bearing proof
 *      of the migration: it can only appear if the screen renders through
 *      `<AppForm testIdPrefix="reset-password">`, which is also what makes the
 *      submit and the new-password field derive the ids the suites select.
 *   2. The required-field message is genuinely ASSOCIATED with its input
 *      (`aria-invalid` + `aria-describedby`), which the hand-rolled `<span>`
 *      never was — the oracle asserts only that the element appears.
 *   3. Both password inputs disable themselves while the reset is in flight.
 *      The oracle pins the submit button only, so an edit could still race the
 *      request it was typed into.
 *
 * WHAT THE MIGRATION MUST NOT DROP (these pass today — regression guards aimed
 * at the specific mistakes this migration invites):
 *
 *   4. The confirmation input keeps the testid `reset-password-confirm`. The
 *      derivation from the field name `confirmPassword` would produce
 *      `reset-password-confirm-password` instead, so the `testId` override is
 *      mandatory here; without it the E2E suite (which fills
 *      `reset-password-confirm`) breaks and nothing else notices. The same
 *      field also carries NO validator and so must render no error slot — the
 *      mismatch is a form-level banner, not a per-field message.
 *   5. An unusable link NEVER navigates. This is the escape hatch's trap: the
 *      guard clauses return early, and inside a `useAppForm` `onSubmit` an
 *      early return resolves the internal mutation SUCCESSFULLY, firing
 *      `onSuccess` and bouncing the user to `/login?message=password_reset` as
 *      though the reset had happened. The oracle checks only that no request
 *      goes out, which such a bug would still satisfy.
 *   6/7. The required message's wording and the submit's idle label. Neither is
 *      pinned anywhere else and both move house during the migration (into the
 *      zod schema, and into `SubmitButton`'s children).
 *   8. A client-side mismatch banner clears once the confirmation matches. The
 *      oracle pins the clear for a SERVER error only; the mismatch guard sits
 *      before the request, on the other side of the `setError(null)`.
 *
 * Same seam as the oracle: the REAL SDK with only its `fetch` faked
 * (`@bc-solutions-coder/testing/sdk-harness`), real router context via
 * `renderWithWallow`, real headless Chromium. The `useNavigate` mock stays for
 * the same reason it does there — navigation is a ROUTER seam, not an SDK one.
 */

const EMAIL = "ada@example.com";
const TOKEN = "reset-token-abc";
const PASSWORD = "N3w-Passw0rd!";

/** The endpoint the screen must reach (packages/sdk/src/generated/sdk.gen.ts). */
const ENDPOINT = "/v1/identity/auth/reset-password";

/** The 200 body: `AccountOperationResponse` — `{ succeeded: true }`, nothing more. */
const SUCCESS_BODY = { succeeded: true };

const OK = 200;

let harness: SdkHarness;

// Hoisted so the vi.mock factory and the test bodies share the same spy.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/** Render the screen as a valid reset link would: both query params present. */
function renderForm(props: Partial<{ email?: string; token?: string }> = {}) {
  return renderWithClient(<ResetPasswordForm email={EMAIL} token={TOKEN} {...props} />);
}

function newPasswordInput(): HTMLInputElement {
  return page.getByTestId("reset-password-new-password").element() as HTMLInputElement;
}

function confirmInput(): HTMLInputElement {
  return page.getByTestId("reset-password-confirm").element() as HTMLInputElement;
}

/** Type both password fields and submit — the whole happy interaction. */
async function submitPasswords(
  user: ReturnType<typeof userEvent.setup>,
  newPassword: string = PASSWORD,
  confirmPassword: string = newPassword,
) {
  await user.fill(page.getByTestId("reset-password-new-password"), newPassword);
  await user.fill(page.getByTestId("reset-password-confirm"), confirmPassword);
  await user.click(page.getByTestId("reset-password-submit"));
}

/**
 * The ids a control points its `aria-describedby` at. Split rather than compared
 * whole: Base UI appends the error to whatever else already describes the
 * control, so the claim is that the message is AMONG them, not that it is alone.
 */
function describedByIds(control: HTMLElement): readonly string[] {
  const value = control.getAttribute("aria-describedby") ?? "";

  return value.split(" ").filter((id: string) => id !== "");
}

beforeEach(() => {
  vi.clearAllMocks();
  harness = createAuthHarness();
  harness.resolveJson(SUCCESS_BODY);
});

describe("ResetPasswordForm on @bc-solutions-coder/forms", () => {
  it("renders through the forms shell, which stamps the derived form testid", async () => {
    await renderForm();

    const form = page.getByTestId("reset-password-form");

    await expect.element(form).toBeInTheDocument();
    expect(form.element().tagName).toBe("FORM");
    // Both fields and the submit must derive from the SAME prefix, or the shell
    // is present but not the thing the ids actually come from.
    expect(newPasswordInput().closest("form")).toBe(form.element());
    expect(confirmInput().closest("form")).toBe(form.element());
    expect(page.getByTestId("reset-password-submit").element().closest("form")).toBe(
      form.element(),
    );
  });

  it("associates the required-field message with the new-password input", async () => {
    const user = userEvent.setup();
    await renderForm();

    await user.click(page.getByTestId("reset-password-submit"));

    const message = page.getByTestId("reset-password-new-password-error");
    await expect.element(message).toBeInTheDocument();

    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(newPasswordInput())).toContain(messageId);
    expect(newPasswordInput().getAttribute("aria-invalid")).toBe("true");
  });

  it("disables both password inputs while the reset is in flight", async () => {
    // The oracle pins the submit button only. Leaving the inputs live lets an
    // edit race the request the values were read into.
    let release: () => void = () => {};
    harness.respond(
      async () =>
        await new Promise<Response>((resolve) => {
          release = () => {
            resolve(Response.json(SUCCESS_BODY, { status: OK }));
          };
        }),
    );
    const user = userEvent.setup();
    await renderForm();

    await submitPasswords(user);

    // Wait for the request to REACH the transport before asserting, for the same
    // reason the oracle's in-flight case does: releasing into the gap before
    // `fetch` is called would leave the never-settling responder installed.
    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(1);
    });
    await expect.poll(() => newPasswordInput().disabled).toBe(true);
    await expect.poll(() => confirmInput().disabled).toBe(true);

    release();

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalled();
    });
  });

  it("keeps the confirmation's legacy testid and gives it no error slot", async () => {
    // `confirmPassword` kebab-derives to `reset-password-confirm-password`, which
    // is NOT the id the E2E suite fills — the `testId` override is load-bearing.
    // The field also has no validator of its own: a mismatch is the form-level
    // banner's to report, never a message under this control.
    const user = userEvent.setup();
    await renderForm();

    await expect.element(page.getByTestId("reset-password-confirm")).toBeInTheDocument();
    expect(page.getByTestId("reset-password-confirm-password").query()).toBeNull();

    await user.click(page.getByTestId("reset-password-submit"));
    await expect.element(page.getByTestId("reset-password-new-password-error")).toBeInTheDocument();
    expect(page.getByTestId("reset-password-confirm-error").query()).toBeNull();

    await submitPasswords(user, PASSWORD, "something-else");

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/passwords do not match/iu);
    expect(page.getByTestId("reset-password-confirm-error").query()).toBeNull();
  });

  it("never navigates when the link is unusable", async () => {
    // The escape hatch's trap: an early `return` out of a `useAppForm` submit
    // callback still RESOLVES the internal mutation, so a naive port fires
    // `onSuccess` and sends the user to the login banner as though the reset had
    // gone through. The oracle only checks that no request left the page, which
    // that bug satisfies.
    const user = userEvent.setup();
    await renderForm({ token: undefined });

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(harness.calls).toHaveLength(0);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("words the required-password message exactly as the validator always has", async () => {
    // The string moves from a hand-written validator into the zod schema during
    // the migration, and nothing else pins it — the oracle asserts only that the
    // message element appears.
    const user = userEvent.setup();
    await renderForm();

    await user.click(page.getByTestId("reset-password-submit"));

    await expect
      .element(page.getByTestId("reset-password-new-password-error"))
      .toHaveTextContent("New password is required");
  });

  it("labels the submit for the idle state", async () => {
    // The oracle pins the pending label ("Resetting...") but never the idle one,
    // which moves into `SubmitButton`'s children.
    await renderForm();

    await expect
      .element(page.getByTestId("reset-password-submit"))
      .toHaveTextContent("Reset password");
  });

  it("clears the mismatch banner once the confirmation matches", async () => {
    // The oracle pins the clear for a SERVER error. The mismatch guard sits on
    // the other side of it — before the request — so a port that clears only in
    // the mutation's own path would leave "Passwords do not match." sitting
    // above a reset that actually succeeded.
    const user = userEvent.setup();
    await renderForm();

    await submitPasswords(user, PASSWORD, "something-else");
    await expect.element(page.getByTestId("reset-password-error")).toBeInTheDocument();

    await user.fill(page.getByTestId("reset-password-confirm"), PASSWORD);
    await user.click(page.getByTestId("reset-password-submit"));

    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(ENDPOINT);
    });
    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/login?message=password_reset" });
    });
    expect(page.getByTestId("reset-password-error").query()).toBeNull();
  });
});
