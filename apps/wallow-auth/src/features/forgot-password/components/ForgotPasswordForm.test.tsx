import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../../../test/harness";
import { Route as forgotPasswordRoute } from "../../../routes/forgot-password";
import { ForgotPasswordForm } from "./ForgotPasswordForm";

/**
 * Component spec for the ForgotPassword screen (Wallow-vec7.3.1).
 *
 * THE POINT OF THIS SCREEN IS ANTI-ENUMERATION. The oracle renders one fixed
 * "if an account exists..." message and never tells the caller whether the
 * address resolved to a user; a screen that surfaced a backend error would leak
 * exactly the fact the endpoint is designed to hide. That asymmetry — an error
 * path that must be *swallowed*, not shown — is what these tests pin, and it is
 * the one place this port is allowed to look "wrong" next to every other screen
 * in the app (all of which surface `{page}-error`). Hence the deliberate
 * assertions below that NO error testid ever appears.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `forgot-password-email`, `forgot-password-submit`, `forgot-password-success`.
 * The oracle has NO error testid for this screen, by design — see above.
 *
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness` (Wallow-pu6a.5.1). The
 * SDK is the REAL one and only its `fetch` is faked, so the screen's whole
 * pipeline — request-scoped SDK -> generated operation -> CSRF interceptor ->
 * serialization -> error shaping -> React Query — runs here, and the assertions
 * below read the outgoing REQUEST rather than a spy on a stand-in double.
 * `renderWithWallow` supplies the router context the screen reads its SDK off,
 * and `createAuthHarness()` pins the harness origin to this app's root-mounted
 * API surface (Wallow-pu6a.5.5).
 *
 * REJECTION SHAPE: the failure tests answer with a bare 500 carrying no problem
 * details. That is deliberate and costs nothing here — this screen must behave
 * identically for EVERY failure regardless of shape, and testing with the
 * least-informative one is the strongest form of that claim.
 */

const EMAIL = "ada@example.com";
const ENDPOINT = "/v1/identity/auth/forgot-password";
const SERVER_ERROR = 500;

let harness: SdkHarness;

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/** Fill the email field and press submit — the whole happy interaction. */
async function submitEmail(user: ReturnType<typeof userEvent.setup>, email: string = EMAIL) {
  await user.type(page.getByTestId("forgot-password-email"), email);
  await user.click(page.getByTestId("forgot-password-submit"));
}

/**
 * The screen must never render an error surface. Checked as an explicit absence
 * because the anti-enumeration guarantee is exactly "no branch reveals the
 * backend's answer" — `{page}-error` is the testid a copy-paste from any other
 * screen in this app would introduce.
 */
function expectNoErrorSurface() {
  expect(page.getByTestId("forgot-password-error").query()).toBeNull();
}

beforeEach(() => {
  harness = createAuthHarness();
  harness.resolveJson({});
});

describe("ForgotPasswordForm", () => {
  it("renders the oracle's form fields, and no success message before submit", async () => {
    await renderWithClient(<ForgotPasswordForm />);

    await expect.element(page.getByTestId("forgot-password-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("forgot-password-submit")).toBeInTheDocument();
    expect(page.getByTestId("forgot-password-success").query()).toBeNull();
  });

  it("links back to sign in", async () => {
    // The card footer. It has no testid and the scout's inventory forbids
    // inventing one for an element that shipped without it, so this asserts the
    // link by role + href instead.
    await renderWithClient(<ForgotPasswordForm />);

    await expect
      .element(page.getByRole("link", { name: /back to sign in/iu }))
      .toHaveAttribute("href", "/login");
  });

  it("sends the typed email to the forgot-password endpoint", async () => {
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await submitEmail(user);

    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(ENDPOINT);
    });
    expect(harness.last?.method).toBe("POST");
    expect(harness.last?.body).toEqual({ email: EMAIL });
  });

  it("replaces the form with the confirmation once the request is sent", async () => {
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await submitEmail(user);

    // The oracle swaps the whole card content on `_submitted` — the form goes
    // away, so the user cannot re-submit into the same success state.
    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
    expect(page.getByTestId("forgot-password-email").query()).toBeNull();
    expect(page.getByTestId("forgot-password-submit").query()).toBeNull();
  });

  it("words the confirmation so it does not confirm the account exists", async () => {
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await submitEmail(user);

    // The oracle's copy, verbatim in substance: conditional ("if an account
    // exists"), never "we sent you a link".
    const success = page.getByTestId("forgot-password-success");
    await expect.element(success).toHaveTextContent(/check your email/iu);
    await expect.element(success).toHaveTextContent(/if an account exists/iu);
  });

  it("shows the same confirmation when the backend rejects the request", async () => {
    // THE anti-enumeration criterion. An unknown address, a rate limit, a 500 —
    // the user sees the identical screen either way.
    harness.rejectJson({ detail: "user_not_found" }, SERVER_ERROR);
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await submitEmail(user, "nobody@example.com");

    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
    expectNoErrorSurface();
  });

  it("never leaks the rejection reason into the page", async () => {
    // A generic error surface is a leak too if it appears only for some inputs;
    // this pins that the reason string itself never reaches the DOM.
    harness.rejectJson({ detail: "user_not_found" }, SERVER_ERROR);
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await submitEmail(user, "nobody@example.com");

    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
    expect(page.getByText(/user_not_found/iu).query()).toBeNull();
    expect(document.body.textContent).not.toMatch(/not found|does not exist|no account/iu);
  });

  it("renders the same confirmation markup whether the backend accepts or rejects", async () => {
    // The strongest statement of the criterion: the two branches are
    // indistinguishable to a caller diffing the rendered page.
    const user = userEvent.setup();

    const accepted = await renderWithClient(<ForgotPasswordForm />);
    await submitEmail(user);
    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
    const acceptedHtml: string = accepted.container.innerHTML;
    await accepted.unmount();

    harness.rejectJson({ detail: "user_not_found" }, SERVER_ERROR);
    const rejected = await renderWithClient(<ForgotPasswordForm />);
    await submitEmail(user, "nobody@example.com");
    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();

    expect(rejected.container.innerHTML).toBe(acceptedHtml);
  });

  it("requires an email before calling the endpoint", async () => {
    // Oracle: `if (string.IsNullOrWhiteSpace(_email)) return;` — a blank submit
    // is a no-op that never reaches the network and never claims success.
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.click(page.getByTestId("forgot-password-submit"));

    expect(harness.calls).toHaveLength(0);
    expect(page.getByTestId("forgot-password-success").query()).toBeNull();
    await expect.element(page.getByTestId("forgot-password-email-error")).toBeInTheDocument();
  });

  it("treats a whitespace-only email as blank", async () => {
    // `IsNullOrWhiteSpace`, not `IsNullOrEmpty` — "   " must not be submitted.
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.type(page.getByTestId("forgot-password-email"), "   ");
    await user.click(page.getByTestId("forgot-password-submit"));

    expect(harness.calls).toHaveLength(0);
    await expect.element(page.getByTestId("forgot-password-email-error")).toBeInTheDocument();
  });

  it("disables submit while the request is in flight", async () => {
    // Oracle: `Disabled="_loading"` — one click, one email.
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

    await submitEmail(user);

    // Wait for the request to REACH the transport before releasing it: the
    // submit button goes disabled the moment the form starts submitting, which
    // is a tick or two before `fetch` is called, and releasing into that gap
    // would leave the never-settling responder installed forever.
    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(1);
    });
    await expect.element(page.getByTestId("forgot-password-submit")).toBeDisabled();

    release();
    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
  });
});

describe("/forgot-password route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path itself is the contract and is
    // not this task's to change (router.tsx is off-limits).
    const RouteComponent = forgotPasswordRoute.options.component as () => ReactElement;

    await renderWithClient(<RouteComponent />);

    await expect.element(page.getByTestId("forgot-password-email")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });
});
