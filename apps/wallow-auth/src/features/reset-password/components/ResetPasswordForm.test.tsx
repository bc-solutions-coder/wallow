import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as resetPasswordRoute } from "@app/routes/reset-password";
import { ResetPasswordForm } from "./ResetPasswordForm";

/**
 * ResetPassword screen and /reset-password route.
 *
 * Real SDK over a faked fetch (sdk-harness), so assertions read the recorded
 * request; `useNavigate` is mocked because navigation is a router seam. Failures
 * arrive as a bare `{ error }` body, not problem details, so no reason string
 * survives — the screen narrows on `status === 400`, this endpoint's only 400.
 */

const EMAIL = "ada@example.com";
const TOKEN = "reset-token-abc";
const PASSWORD = "N3w-Passw0rd!";

/** The endpoint the screen must reach. */
const ENDPOINT = "/v1/identity/auth/reset-password";

/** The 200 body: `AccountOperationResponse` — `{ succeeded: true }`, nothing more. */
const SUCCESS_BODY = { succeeded: true };

/** The real 400 body: both of this endpoint's failure returns write exactly this. */
const INVALID_TOKEN_BODY = { succeeded: false, error: "invalid_token" };

const BAD_REQUEST = 400;
const SERVER_ERROR = 500;
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

/**
 * The ids a control points its `aria-describedby` at. Split rather than compared
 * whole: Base UI appends the error to whatever else already describes the
 * control, so the claim is that the message is AMONG them, not that it is alone.
 */
function describedByIds(control: HTMLElement): readonly string[] {
  const value = control.getAttribute("aria-describedby") ?? "";

  return value.split(" ").filter((id: string) => id !== "");
}

/** Type both password fields and submit — the whole happy interaction. */
async function submitPasswords(
  user: ReturnType<typeof userEvent.setup>,
  newPassword: string = PASSWORD,
  confirmPassword: string = newPassword,
) {
  if (newPassword !== "") {
    await user.type(page.getByTestId("reset-password-new-password"), newPassword);
  }
  if (confirmPassword !== "") {
    await user.type(page.getByTestId("reset-password-confirm"), confirmPassword);
  }
  await user.click(page.getByTestId("reset-password-submit"));
}

beforeEach(() => {
  vi.clearAllMocks();
  harness = createPassthroughHarness();
  harness.resolveJson(SUCCESS_BODY);
});

describe("ResetPasswordForm", () => {
  it("renders both fields and the idle submit label, and no error before submit", async () => {
    renderForm();

    await expect.element(page.getByTestId("reset-password-new-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("reset-password-confirm")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("reset-password-submit"))
      .toHaveTextContent("Reset password");
    expect(page.getByTestId("reset-password-error").query()).toBeNull();
  });

  it("masks both password fields", async () => {
    renderForm();

    await expect
      .element(page.getByTestId("reset-password-new-password"))
      .toHaveAttribute("type", "password");
    await expect
      .element(page.getByTestId("reset-password-confirm"))
      .toHaveAttribute("type", "password");
  });

  it("links back to sign in", async () => {
    // No testid on this footer link, so it is asserted by role + href.
    renderForm();

    await expect
      .element(page.getByRole("link", { name: /back to sign in/iu }))
      .toHaveAttribute("href", "/login");
  });

  it("sends the query's email and token with the typed password", async () => {
    // The reset link's identity comes from the URL, the secret from the form.
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(ENDPOINT);
    });
    expect(harness.last?.method).toBe("POST");
    expect(harness.last?.body).toEqual({
      email: EMAIL,
      token: TOKEN,
      newPassword: PASSWORD,
    });
  });

  it("redirects to the login page with the password_reset notice on success", async () => {
    // `href` (a raw location) rather than `to` + `search`: this screen must not
    // couple itself to /login's own `validateSearch`.
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/login?message=password_reset" });
    });
  });

  it("rejects a mismatched confirmation without calling the endpoint", async () => {
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user, PASSWORD, "something-else");

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/passwords do not match/iu);
    // The mismatch is a form-level banner: the confirmation field carries no
    // validator of its own and must render no message under the control.
    expect(page.getByTestId("reset-password-confirm-error").query()).toBeNull();
    expect(harness.calls).toHaveLength(0);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("clears the mismatch banner once the confirmation matches", async () => {
    // The mismatch guard sits BEFORE the request, so a screen that only clears
    // the banner in the mutation's own path leaves "Passwords do not match."
    // sitting above a reset that actually succeeded.
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user, PASSWORD, "something-else");
    await expect.element(page.getByTestId("reset-password-error")).toBeInTheDocument();

    await user.fill(page.getByTestId("reset-password-confirm"), PASSWORD);
    await user.click(page.getByTestId("reset-password-submit"));

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/login?message=password_reset" });
    });
    expect(page.getByTestId("reset-password-error").query()).toBeNull();
  });

  it("refuses to submit a link with no token", async () => {
    const user = userEvent.setup();
    renderForm({ token: undefined });

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(harness.calls).toHaveLength(0);
    // An early return out of the submit callback still RESOLVES the form's
    // internal mutation, so a naive screen fires `onSuccess` and sends the user
    // to the login banner as though the reset had gone through.
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("refuses to submit a link with no email", async () => {
    const user = userEvent.setup();
    renderForm({ email: undefined });

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(harness.calls).toHaveLength(0);
  });

  it("treats an empty-string token as a missing one", async () => {
    const user = userEvent.setup();
    renderForm({ token: "" });

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(harness.calls).toHaveLength(0);
  });

  it("requires a new password before calling the endpoint", async () => {
    // The check is deliberately local: an empty password that POSTed would come
    // back 400 invalid_token, telling the user their *link* expired when in fact
    // they typed nothing.
    const user = userEvent.setup();
    renderForm();

    await user.click(page.getByTestId("reset-password-submit"));

    expect(harness.calls).toHaveLength(0);
    const message = page.getByTestId("reset-password-new-password-error");
    await expect.element(message).toHaveTextContent("New password is required");

    // Associated with the input, not merely rendered beside it.
    const messageId: string = message.element().id;
    expect(messageId).not.toBe("");
    expect(describedByIds(newPasswordInput())).toContain(messageId);
    expect(newPasswordInput().getAttribute("aria-invalid")).toBe("true");
  });

  it("explains an expired or invalid reset link when the endpoint rejects it", async () => {
    harness.rejectJson(INVALID_TOKEN_BODY, BAD_REQUEST);
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    const error = page.getByTestId("reset-password-error");
    await expect.element(error).toHaveTextContent(/invalid or has expired/iu);
    await expect.element(error).toHaveTextContent(/request a new one/iu);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("falls back to the generic message for a non-400 failure", async () => {
    // The empty body is deliberate: a server fault carries no problem details of
    // its own, so nothing but the status is available to narrow on.
    harness.rejectJson({}, SERVER_ERROR);
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    const error = page.getByTestId("reset-password-error");
    await expect.element(error).toHaveTextContent(/failed to reset password/iu);
    await expect.element(error).not.toHaveTextContent(/expired/iu);
  });

  it("shows the generic message when the request fails without a status", async () => {
    // A network-level rejection has no status anywhere: the transport throws
    // before a response exists, so the narrowing must not assume one.
    harness.respond(() => {
      throw new TypeError("Failed to fetch");
    });
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/failed to reset password/iu);
  });

  it("never leaks the raw rejection into the page", async () => {
    // The seam hands the screen `title: "Unknown error"` for a body with no
    // problem details; neither that nor the machine token may reach the page.
    harness.rejectJson(INVALID_TOKEN_BODY, BAD_REQUEST);
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);

    await expect.element(page.getByTestId("reset-password-error")).toBeInTheDocument();
    expect(page.getByText(/unknown error/iu).query()).toBeNull();
    expect(page.getByText(/invalid_token/u).query()).toBeNull();
  });

  it("clears a previous error when the next attempt succeeds", async () => {
    let attempts = 0;
    harness.respond(() => {
      attempts += 1;
      return attempts === 1
        ? Response.json(INVALID_TOKEN_BODY, { status: BAD_REQUEST })
        : Response.json(SUCCESS_BODY, { status: OK });
    });
    const user = userEvent.setup();
    renderForm();

    await submitPasswords(user);
    await expect.element(page.getByTestId("reset-password-error")).toBeInTheDocument();

    await user.click(page.getByTestId("reset-password-submit"));

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/login?message=password_reset" });
    });
    expect(page.getByTestId("reset-password-error").query()).toBeNull();
  });

  it("disables submit and both password inputs while the request is in flight", async () => {
    // One click, one reset attempt — and leaving the inputs live would let an
    // edit race the request the values were already read into.
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
    renderForm();

    await submitPasswords(user);

    // Wait for the request to REACH the transport before releasing it: the
    // submit button goes disabled a tick or two before `fetch` is called, and
    // releasing into that gap leaves the never-settling responder installed.
    await vi.waitFor(() => {
      expect(harness.calls).toHaveLength(1);
    });
    await expect.element(page.getByTestId("reset-password-submit")).toBeDisabled();
    await expect
      .element(page.getByTestId("reset-password-submit"))
      .toHaveTextContent(/resetting/iu);
    await expect.poll(() => newPasswordInput().disabled).toBe(true);
    await expect.poll(() => confirmInput().disabled).toBe(true);

    release();
    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalled();
    });
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — email+token
 * read from the query string — only exists once a URL is parsed by a router. The
 * root here is a throwaway: the app's real `__root.tsx` renders `<html>`.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/reset-password", route: resetPasswordRoute }],
  });
}

describe("/reset-password route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    renderRouteAt(`/reset-password?email=${encodeURIComponent(EMAIL)}&token=${TOKEN}`);

    await expect.element(page.getByTestId("reset-password-new-password")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("threads the email and token out of the query string into the reset call", async () => {
    const user = userEvent.setup();
    renderRouteAt(`/reset-password?email=${encodeURIComponent(EMAIL)}&token=${TOKEN}`);

    await expect.element(page.getByTestId("reset-password-new-password")).toBeInTheDocument();
    await submitPasswords(user);

    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(ENDPOINT);
    });
    expect(harness.last?.body).toEqual({
      email: EMAIL,
      token: TOKEN,
      newPassword: PASSWORD,
    });
  });

  it("renders without throwing when the link carries no query at all", async () => {
    // `validateSearch` has to treat both params as optional rather than throw.
    const user = userEvent.setup();
    renderRouteAt("/reset-password");

    await expect.element(page.getByTestId("reset-password-new-password")).toBeInTheDocument();
    await submitPasswords(user);

    await expect
      .element(page.getByTestId("reset-password-error"))
      .toHaveTextContent(/invalid reset link/iu);
    expect(harness.calls).toHaveLength(0);
  });
});
