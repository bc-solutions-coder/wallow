import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as verifyEmailConfirmRoute } from "../../../routes/verify-email/confirm";
import { VerifyEmailConfirm } from "./VerifyEmailConfirm";

/**
 * Component spec for the VerifyEmailConfirm screen (Wallow-vec7.3.3).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `verify-email-confirm-loading`, `verify-email-confirm-success`,
 * `verify-email-confirm-continue`, `verify-email-confirm-error`,
 * `verify-email-confirm-signin-link`.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly (that module is the only permitted importer
 * of the SDK). Per bd memories `vitest-resetmodules-breaks-instanceof-across-
 * graphs`, this file uses a plain `vi.mock` factory + `vi.hoisted` spies and
 * NEVER `vi.resetModules()`.
 *
 * ── THE THREE STATES ─────────────────────────────────────────────────────────
 *
 * This screen has no form: it fires one request on mount and is a pure function
 * of that request's outcome. `_loading` starts TRUE, so the loading state is the
 * screen's initial render, not a transient — except on the missing-parameter
 * path, which short-circuits to the error state without ever going to the
 * network. The three states are mutually exclusive (the oracle's
 * if/else-if/else), which the tests below assert as absences, not just presence.
 *
 * ── THE ERROR-BRANCH FINDING (verified against the source, not assumed) ───────
 *
 * The oracle switches its message on `result.Error`:
 *
 *     "invalid_token" => "The verification link is invalid or has expired."
 *     _               => "Failed to verify email. Please try again."
 *
 * That switch cannot be ported as written, for the same reason ResetPassword's
 * could not (Wallow-vec7.3.2). `AccountController.VerifyEmail`
 * (api/src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs
 * :796-822) returns its failures as **`BadRequest(new { succeeded = false,
 * error = "invalid_token" })`** — a 400 whose body is a bare anon object, NOT
 * RFC 7807 problem details. `unwrap()` THROWS on any non-2xx, and
 * `toWallowError()` (packages/sdk/src/auth-client.ts:257-280) builds its `code`
 * from `extensions.code` ?? `code` only — it never reads a top-level `error`
 * field. So `{ succeeded: false, error: "invalid_token" }` arrives as
 * `WallowError{ status: 400, code: "UNKNOWN", title: "Unknown error" }`. The
 * reason string is LOST at the seam (bd memory `wallow-auth-auth-client-ts-
 * wallowerror-code-loss`).
 *
 * What survives is the HTTP status, and that is enough: this endpoint has
 * exactly TWO failure returns (unknown email, and a rejected `ConfirmEmailAsync`)
 * and BOTH are `400 + error: "invalid_token"`. A 400 from this endpoint
 * therefore *means* invalid_token. The oracle's `_` arm is unreachable through
 * this endpoint, so the port maps onto status instead:
 *
 *     400           -> "The verification link is invalid or has expired."   (oracle's invalid_token arm)
 *     anything else -> "An error occurred while verifying your email..."     (oracle's catch arm)
 *
 * Non-400 rejections land on the generic `catch` message rather than the
 * unreachable `_` arm — a 500 with a non-JSON body throws during error parsing
 * and falls into `catch` too.
 *
 * The screen must narrow on `status` STRUCTURALLY (`error.status === 400`)
 * rather than with `instanceof WallowError`: `WallowError` is exported from the
 * SDK's `./server` entry, and screens may not import from the SDK at all.
 * Consequently the rejection fixtures below are WallowError-SHAPED objects,
 * including the `code: "UNKNOWN"` that proves the port is not secretly relying
 * on a code the seam never delivers.
 *
 * ── SUCCESS IS "RESOLVED", NOT "succeeded === true" ──────────────────────────
 *
 * The oracle reads `result.Succeeded` off the body. Through this seam that read
 * is redundant and is deliberately NOT ported: every 200 return from the
 * endpoint is `Ok(new { succeeded = true })` — there is no 200-with-false — and
 * every falsy case is a 400 that `unwrap()` has already turned into a throw. A
 * resolved promise IS success.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  verifyEmail: vi.fn(),
  isSafeReturnUrl: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: { verifyEmail: mocks.verifyEmail },
    oidc: { isSafeReturnUrl: mocks.isSafeReturnUrl },
  }),
}));

const EMAIL = "ada@example.com";
const TOKEN = "verification-token";

/**
 * The real `isSafeReturnUrl` rule (packages/sdk/src/auth-oidc.ts), restated here
 * because the screen reaches it through the mocked facade. Restating it rather
 * than stubbing `true`/`false` per test keeps these tests pinning the SCREEN's
 * use of the guard against the guard's actual semantics — a screen that passed
 * only because the stub said "safe" would be proving nothing.
 */
function isSafeReturnUrlRule(url: string | null | undefined): boolean {
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  return url.startsWith("/") && !url.startsWith("//");
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/** A WallowError-shaped rejection, as the real facade's `unwrap()` throws. */
function wallowErrorShaped(status: number) {
  return { status, code: "UNKNOWN", title: "Unknown error", detail: undefined };
}

/** Assert exactly one of the three mutually-exclusive states is on screen. */
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
  vi.clearAllMocks();
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
  mocks.verifyEmail.mockResolvedValue({ succeeded: true });
});

describe("VerifyEmailConfirm — loading state", () => {
  it("shows only the spinner while the request is in flight", async () => {
    // Oracle: `_loading = true` is the field initialiser, so the very first
    // paint is the spinner — a never-resolving request pins it there.
    mocks.verifyEmail.mockReturnValue(new Promise(() => {}));

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expectOnlyState("loading");
  });

  it("verifies the email with the token from the link", async () => {
    mocks.verifyEmail.mockReturnValue(new Promise(() => {}));

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await vi.waitFor(() => {
      expect(mocks.verifyEmail).toHaveBeenCalledWith({ email: EMAIL, token: TOKEN });
    });
  });

  it("fires the verification exactly once", async () => {
    // The request is a side effect of mounting, not of rendering: a screen that
    // re-fired on every render would burn the single-use token.
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    expect(mocks.verifyEmail).toHaveBeenCalledTimes(1);
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

    // Asserted by role rather than by DOM nesting: the oracle wraps the button
    // in the testid'd div, but whether the port puts the testid on the anchor or
    // on a wrapper is the implementer's call — that it is a link to the
    // returnUrl is the contract.
    await expect.element(page.getByTestId("verify-email-confirm-continue")).toBeInTheDocument();
    await expect
      .element(page.getByRole("link", { name: /continue/iu }))
      .toHaveAttribute("href", "/dashboard");
  });

  it("omits Continue when the link carries no returnUrl", async () => {
    // Oracle: the Continue block is gated on `IsSafe(ReturnUrl)`, and a nullish
    // returnUrl is not safe — there is nowhere to continue TO.
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();

    expect(page.getByTestId("verify-email-confirm-continue").query()).toBeNull();
  });

  it("omits Continue when the returnUrl is an off-origin absolute URL", async () => {
    // The open-redirect criterion. `IsSafe` rejects it, so the button that would
    // have carried the user to evil.com is simply not rendered.
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
    // A 400 from this endpoint means invalid_token — it has no other 400. See
    // the error-branch finding in this file's header.
    mocks.verifyEmail.mockRejectedValue(wallowErrorShaped(400));

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/verification failed/iu);
    await expect.element(error).toHaveTextContent(/invalid or has expired/iu);
  });

  it("shows only the error surface once verification fails", async () => {
    mocks.verifyEmail.mockRejectedValue(wallowErrorShaped(400));

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    await expectOnlyState("error");
  });

  it("shows the generic message when the request fails for any other reason", async () => {
    // Oracle's `catch` arm: a 500 is not a bad link, and must not tell the user
    // their link expired when it did not.
    mocks.verifyEmail.mockRejectedValue(wallowErrorShaped(500));

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/an error occurred/iu);
    await expect.element(error).not.toHaveTextContent(/expired/iu);
  });

  it("survives a rejection that is not WallowError-shaped at all", async () => {
    // The screen narrows structurally on `status`; a bare Error (a network
    // failure, say) has none, and must land on the generic arm rather than
    // throwing inside the error branch.
    mocks.verifyEmail.mockRejectedValue(new Error("network down"));

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/an error occurred/iu);
  });

  it("never leaks the raw rejection into the page", async () => {
    // `code: "UNKNOWN"` / `title: "Unknown error"` are seam artefacts, not
    // user-facing copy. The oracle shows curated messages only.
    mocks.verifyEmail.mockRejectedValue(wallowErrorShaped(400));

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    expect(document.body.textContent).not.toMatch(/unknown error|UNKNOWN/u);
  });
});

describe("VerifyEmailConfirm — missing parameters", () => {
  it("refuses a link with no token without calling the endpoint", async () => {
    // Oracle: the guard runs before the try block — `_loading = false` and an
    // error message, no request. Pinning `not.toHaveBeenCalled` is the point:
    // a screen that "helpfully" sent `token: undefined` would 400 and blame the
    // user's link for the screen's own bug.
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/missing required parameters/iu);
    expect(mocks.verifyEmail).not.toHaveBeenCalled();
  });

  it("refuses a link with no email without calling the endpoint", async () => {
    await renderWithClient(<VerifyEmailConfirm token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/missing required parameters/iu);
    expect(mocks.verifyEmail).not.toHaveBeenCalled();
  });

  it("treats an empty-string parameter as missing", async () => {
    // Oracle: `string.IsNullOrEmpty(Token)` — `?token=&email=x` is a malformed
    // link, not a token to try.
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token="" />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    expect(mocks.verifyEmail).not.toHaveBeenCalled();
  });

  it("never shows the spinner when the link is malformed", async () => {
    // The missing-parameter path short-circuits: there is no request to wait on,
    // so the user must not be told we are "verifying your email".
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
    // The oracle's card FOOTER, outside the if/else — it is the one way out of
    // the error state, so it must survive the failure branch.
    mocks.verifyEmail.mockRejectedValue(wallowErrorShaped(400));

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    await expect
      .element(page.getByTestId("verify-email-confirm-signin-link"))
      .toHaveAttribute("href", "/login");
  });

  it("carries a safe returnUrl through to sign in, URL-encoded", async () => {
    // Oracle: `$"/login?returnUrl={Uri.EscapeDataString(ReturnUrl!)}"`. The
    // encoding matters — an unencoded `&` would forge extra query parameters on
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
    // Oracle: `IsSafe(ReturnUrl) ? "/login?returnUrl=..." : "/login"`. Note this
    // is NOT the sanitize-vs-refuse case from bd memory `returnurl-guard-refuse-
    // dont-sanitize`: nothing navigates anywhere here, the screen just declines
    // to forward a hostile value into the next screen's query string.
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
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`: this route's component reads `email`, `token`,
 * and `returnUrl` through `Route.useSearch()`, and every router hook
 * dereferences a `null` router outside a `RouterProvider` (`useRouter` only
 * warns; `useMatch` then throws on `router.stores`), so a bare render is
 * unsatisfiable by any correct implementation. Mirrors the harness
 * `ResetPasswordForm.test.tsx` established for the same reason.
 *
 * The root here is a throwaway: the app's real `__root.tsx` renders `<html>`,
 * and `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    verifyEmailConfirmRoute.update({
      id: "/verify-email/confirm",
      path: "/verify-email/confirm",
      getParentRoute: () => rootRoute,
    } as any),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

describe("/verify-email/confirm route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path itself is the contract and is
    // not this task's to change (router.tsx is off-limits).
    mocks.verifyEmail.mockResolvedValue(null);

    await renderRouteAt(
      `/verify-email/confirm?email=${encodeURIComponent(EMAIL)}&token=${TOKEN}` +
        `&returnUrl=${encodeURIComponent("/apps")}`,
    );

    await expect.element(page.getByTestId("verify-email-confirm-success")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
    // The query string must actually reach the screen, not merely be parsed:
    // email+token thread as far as the request, and returnUrl as far as the
    // Continue link. A route that dropped any of them fails here rather than
    // rendering a green screen off an empty search.
    expect(mocks.verifyEmail).toHaveBeenCalledWith({ email: EMAIL, token: TOKEN });
    await expect
      .element(page.getByTestId("verify-email-confirm-continue"))
      .toHaveAttribute("href", "/apps");
  });

  it("reads token, email, and returnUrl off the query string", () => {
    // The oracle's three `[SupplyParameterFromQuery]` properties. The route owns
    // this read; the component takes them as props.
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
