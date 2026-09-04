import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as verifyEmailConfirmRoute } from "@app/routes/verify-email/confirm";
import { VerifyEmailConfirm } from "./VerifyEmailConfirm";

/**
 * Verify-email confirmation screen: one request on mount, three mutually
 * exclusive states. Runs the real SDK over a faked fetch (sdk-harness). Every
 * 2xx from this endpoint means success, so there is no `succeeded` flag to read.
 *
 * Failures arrive as a bare 400 body, not problem details, so the reason string
 * is lost at the seam and the screen narrows on `status` alone — a 400 here
 * means an invalid or expired token, anything else is generic. It narrows
 * structurally, not with `instanceof ApiFailure`, on the wire shape alone.
 */

const EMAIL = "ada@example.com";
const TOKEN = "verification-token";

/**
 * `email` and `token` ride the QUERY STRING, so they are read off `call.url`
 * rather than `call.body`.
 */
const VERIFY_ENDPOINT = "/v1/identity/auth/verify-email";

const INVALID_TOKEN_STATUS = 400;
const SERVER_ERROR_STATUS = 500;

let harness: SdkHarness;

function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/**
 * `status` is what the screen narrows on; the code and title are filler carried
 * on the wire so the "never leaks the raw rejection" case has something real to
 * catch.
 */
function wallowErrorBody(status: number) {
  return { status, code: "Test.Filler", title: "Unknown error" };
}

function verifyParamsOf(call: SdkHarness["last"]) {
  const params: URLSearchParams = new URL(call?.url ?? "http://wallow.test/").searchParams;

  return { email: params.get("email"), token: params.get("token") };
}

async function expectOnlyState(state: "loading" | "success" | "error") {
  const states = ["loading", "success", "error"] as const;

  for (const candidate of states) {
    const testid = `verify-email-confirm-${candidate}`;

    if (candidate === state) {
      await expect.element(page.getByTestId(testid)).toBeInTheDocument();
    } else {
      expect(page.getByTestId(testid).query()).toBeNull();
    }
  }
}

beforeEach(() => {
  harness = createPassthroughHarness();
  harness.resolveJson({ succeeded: true });
});

describe("VerifyEmailConfirm — loading state", () => {
  it("shows only the spinner while the request is in flight", async () => {
    harness.pending();

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expectOnlyState("loading");
  });

  it("verifies the email with the token from the link", async () => {
    harness.pending();

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(VERIFY_ENDPOINT);
    });
    expect(harness.last?.method).toBe("GET");
    expect(verifyParamsOf(harness.last)).toEqual({ email: EMAIL, token: TOKEN });
  });

  it("fires the verification exactly once", async () => {
    // The token is single-use: a screen that re-fired on every render burns it.
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    expect(harness.calls.filter((call) => call.path === VERIFY_ENDPOINT)).toHaveLength(1);
  });
});

describe("VerifyEmailConfirm — success state", () => {
  it("replaces the spinner with the confirmation once the email is verified", async () => {
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    await expectOnlyState("success");
  });

  it("tells the user they can now sign in", async () => {
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    const success = page.getByTestId("verify-email-confirm-success");

    await expect.element(success).toHaveTextContent(/email verified/iu);
    await expect.element(success).toHaveTextContent(/you can now sign in/iu);
  });

  it("offers Continue to a safe returnUrl", async () => {
    await renderWithClient(
      <VerifyEmailConfirm email={EMAIL} token={TOKEN} returnUrl="/dashboard" />,
    );

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    // The testid may sit on the anchor or on a wrapper; that it is a link to the
    // returnUrl is the contract, so the href is asserted by role.
    await expect.element(page.getByTestId("verify-email-confirm-continue")).toBeInTheDocument();
    await expect
      .element(page.getByRole("link", { name: /continue/iu }))
      .toHaveAttribute("href", "/dashboard");
  });

  it("omits Continue when the link carries no returnUrl", async () => {
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    expect(page.getByTestId("verify-email-confirm-continue").query()).toBeNull();
  });

  it("omits Continue when the returnUrl is an off-origin absolute URL", async () => {
    // The open-redirect criterion: the button is not rendered at all.
    await renderWithClient(
      <VerifyEmailConfirm email={EMAIL} token={TOKEN} returnUrl="https://evil.example/steal" />,
    );

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    expect(page.getByTestId("verify-email-confirm-continue").query()).toBeNull();
  });

  it("omits Continue when the returnUrl is protocol-relative", async () => {
    // `//evil.example` is the guard's whole reason to exist: it looks relative
    // and resolves off-origin.
    await renderWithClient(
      <VerifyEmailConfirm email={EMAIL} token={TOKEN} returnUrl="//evil.example/steal" />,
    );

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    expect(page.getByTestId("verify-email-confirm-continue").query()).toBeNull();
  });
});

describe("VerifyEmailConfirm — error state", () => {
  it("shows the invalid-or-expired message when the endpoint rejects the token", async () => {
    harness.rejectJson(wallowErrorBody(INVALID_TOKEN_STATUS), INVALID_TOKEN_STATUS);

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/verification failed/iu);
    await expect.element(error).toHaveTextContent(/invalid or has expired/iu);
  });

  it("shows only the error surface once verification fails", async () => {
    harness.rejectJson(wallowErrorBody(INVALID_TOKEN_STATUS), INVALID_TOKEN_STATUS);

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    await expectOnlyState("error");
  });

  it("shows the generic message when the request fails for any other reason", async () => {
    // A 500 is not a bad link, and must not tell the user their link expired.
    harness.rejectJson(wallowErrorBody(SERVER_ERROR_STATUS), SERVER_ERROR_STATUS);

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/an error occurred/iu);
    await expect.element(error).not.toHaveTextContent(/expired/iu);
  });

  it("survives a rejection that is not ApiFailure-shaped at all", async () => {
    // A bare Error has no `status` and must land on the generic arm rather than
    // throwing inside the error branch. A transport that THROWS is the honest
    // way to produce one — `fetch` rejecting is exactly a network failure.
    harness.respond(() => {
      throw new Error("network down");
    });

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/an error occurred/iu);
  });

  it("never leaks the raw rejection into the page", async () => {
    harness.rejectJson(wallowErrorBody(INVALID_TOKEN_STATUS), INVALID_TOKEN_STATUS);

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    expect(document.body.textContent).not.toMatch(/unknown error|UNKNOWN/u);
  });
});

describe("VerifyEmailConfirm — missing parameters", () => {
  it("refuses a link with no token without calling the endpoint", async () => {
    // A screen that "helpfully" sent `token: undefined` would 400 and blame the
    // user's link for its own bug, so nothing may reach the transport.
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/missing required parameters/iu);
    expect(harness.calls).toHaveLength(0);
  });

  it("refuses a link with no email without calling the endpoint", async () => {
    await renderWithClient(<VerifyEmailConfirm token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/missing required parameters/iu);
    expect(harness.calls).toHaveLength(0);
  });

  it("treats an empty-string parameter as missing", async () => {
    // `?token=&email=x` is a malformed link, not a token to try.
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token="" />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    expect(harness.calls).toHaveLength(0);
  });

  it("never shows the spinner when the link is malformed", async () => {
    // Nothing is in flight to wait on, so the user must not be told we are
    // "verifying your email".
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    await expectOnlyState("error");
  });
});

describe("VerifyEmailConfirm — sign-in link", () => {
  it("links to sign in from the success state", async () => {
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    await expect
      .element(page.getByTestId("verify-email-confirm-signin-link"))
      .toHaveAttribute("href", "/login");
  });

  it("links to sign in from the error state too", async () => {
    // The card footer is the one way out of the error state.
    harness.rejectJson(wallowErrorBody(INVALID_TOKEN_STATUS), INVALID_TOKEN_STATUS);

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    await expect
      .element(page.getByTestId("verify-email-confirm-signin-link"))
      .toHaveAttribute("href", "/login");
  });

  it("carries a safe returnUrl through to sign in, URL-encoded", async () => {
    // The encoding matters: an unencoded `&` forges extra query parameters on
    // the login page.
    await renderWithClient(
      <VerifyEmailConfirm email={EMAIL} token={TOKEN} returnUrl="/apps?a=1&b=2" />,
    );

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    await expect
      .element(page.getByTestId("verify-email-confirm-signin-link"))
      .toHaveAttribute("href", `/login?returnUrl=${encodeURIComponent("/apps?a=1&b=2")}`);
  });

  it("drops an unsafe returnUrl from the sign-in link", async () => {
    // Nothing navigates here — the screen declines to forward a hostile value
    // into the next screen's query string.
    await renderWithClient(
      <VerifyEmailConfirm email={EMAIL} token={TOKEN} returnUrl="https://evil.example" />,
    );

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    await expect
      .element(page.getByTestId("verify-email-confirm-signin-link"))
      .toHaveAttribute("href", "/login");
  });
});

/**
 * A real memory router, not `Route.options.component`: the component reads its
 * search params through `Route.useSearch()`, and a router hook outside a
 * `RouterProvider` dereferences a `null` router (`useRouter` warns, `useMatch`
 * then throws), so a bare render cannot pass. The root is a throwaway — the
 * app's real `__root.tsx` renders `<html>`.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/verify-email/confirm", route: verifyEmailConfirmRoute }],
  });
}

describe("/verify-email/confirm route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    await renderRouteAt(
      `/verify-email/confirm?email=${encodeURIComponent(EMAIL)}&token=${TOKEN}` +
        `&returnUrl=${encodeURIComponent("/apps")}`,
    );

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
    // The query string must reach the screen, not merely be parsed: a route that
    // dropped a parameter fails here rather than rendering a green screen off an
    // empty search.
    expect(harness.last?.path).toBe(VERIFY_ENDPOINT);
    expect(verifyParamsOf(harness.last)).toEqual({ email: EMAIL, token: TOKEN });
    await expect
      .element(page.getByTestId("verify-email-confirm-continue"))
      .toHaveAttribute("href", "/apps");
  });

  it("reads token, email, and returnUrl off the query string", () => {
    // The route owns this read; the component takes them as props.
    const validateSearch = verifyEmailConfirmRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch).toBeDefined();
    expect(validateSearch?.({ token: TOKEN, email: EMAIL, returnUrl: "/dashboard" })).toEqual({
      token: TOKEN,
      email: EMAIL,
      returnUrl: "/dashboard",
    });
  });

  it("tolerates a query string with none of them", () => {
    const validateSearch = verifyEmailConfirmRoute.options.validateSearch as
      | ((search: Record<string, unknown>) => unknown)
      | undefined;

    expect(validateSearch?.({})).toEqual({
      token: undefined,
      email: undefined,
      returnUrl: undefined,
    });
  });
});
