import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as forgotPasswordRoute } from "@app/routes/forgot-password";
import { ForgotPasswordForm } from "./ForgotPasswordForm";

/**
 * Forgot-password screen. Anti-enumeration is the point: it renders one fixed
 * "if an account exists..." message and never reveals whether the address
 * resolved, so it is the one screen in this app with no `{page}-error` testid —
 * the absences asserted below are deliberate, not gaps.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so assertions read the
 * recorded request. Failures answer a bare 500 with no problem details: the
 * screen must behave identically for every failure shape.
 */

const EMAIL = "ada@example.com";
const ENDPOINT = "/v1/identity/auth/forgot-password";
const SERVER_ERROR = 500;

let harness: SdkHarness;

function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

async function submitEmail(user: ReturnType<typeof userEvent.setup>, email: string = EMAIL) {
  await user.type(page.getByTestId("forgot-password-email"), email);
  await user.click(page.getByTestId("forgot-password-submit"));
}

/** `{page}-error` is the testid a copy-paste from any other screen would introduce. */
function expectNoErrorSurface() {
  expect(page.getByTestId("forgot-password-error").query()).toBeNull();
}

beforeEach(() => {
  harness = createPassthroughHarness();
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
    // The card footer ships without a testid, so this asserts by role + href.
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

    // The whole card content swaps, so the user cannot re-submit into the same
    // success state.
    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
    expect(page.getByTestId("forgot-password-email").query()).toBeNull();
    expect(page.getByTestId("forgot-password-submit").query()).toBeNull();
  });

  it("words the confirmation so it does not confirm the account exists", async () => {
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await submitEmail(user);

    // Conditional ("if an account exists"), never "we sent you a link".
    const success = page.getByTestId("forgot-password-success");
    await expect.element(success).toHaveTextContent(/check your email/iu);
    await expect.element(success).toHaveTextContent(/if an account exists/iu);
  });

  it("shows the same confirmation when the backend rejects the request", async () => {
    // An unknown address, a rate limit, a 500 — the user sees the same screen.
    harness.rejectJson({ detail: "user_not_found" }, SERVER_ERROR);
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await submitEmail(user, "nobody@example.com");

    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
    expectNoErrorSurface();
  });

  it("never leaks the rejection reason into the page", async () => {
    harness.rejectJson({ detail: "user_not_found" }, SERVER_ERROR);
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await submitEmail(user, "nobody@example.com");

    await expect.element(page.getByTestId("forgot-password-success")).toBeInTheDocument();
    expect(page.getByText(/user_not_found/iu).query()).toBeNull();
    expect(document.body.textContent).not.toMatch(/not found|does not exist|no account/iu);
  });

  it("renders the same confirmation markup whether the backend accepts or rejects", async () => {
    // The two branches are indistinguishable to a caller diffing the page.
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
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.click(page.getByTestId("forgot-password-submit"));

    expect(harness.calls).toHaveLength(0);
    expect(page.getByTestId("forgot-password-success").query()).toBeNull();
    await expect.element(page.getByTestId("forgot-password-email-error")).toBeInTheDocument();
  });

  it("treats a whitespace-only email as blank", async () => {
    const user = userEvent.setup();
    await renderWithClient(<ForgotPasswordForm />);

    await user.type(page.getByTestId("forgot-password-email"), "   ");
    await user.click(page.getByTestId("forgot-password-submit"));

    expect(harness.calls).toHaveLength(0);
    await expect.element(page.getByTestId("forgot-password-email-error")).toBeInTheDocument();
  });

  it("disables submit while the request is in flight", async () => {
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

    // Wait for the request to REACH the transport before releasing it: submit
    // goes disabled a tick or two before `fetch` is called, and releasing into
    // that gap leaves the never-settling responder installed forever.
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
    const RouteComponent = forgotPasswordRoute.options.component as () => ReactElement;

    await renderWithClient(<RouteComponent />);

    await expect.element(page.getByTestId("forgot-password-email")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });
});
