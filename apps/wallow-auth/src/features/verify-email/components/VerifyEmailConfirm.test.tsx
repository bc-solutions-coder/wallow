import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../../../test/harness";
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
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness` (Wallow-pu6a.5.1). The
 * SDK is the REAL one and only its `fetch` is faked, so the screen's whole
 * pipeline — generated `{op}Options()` -> request-scoped SDK -> generated
 * operation -> CSRF interceptor -> serialization -> error shaping -> React Query
 * — runs here. The assertions that used to read a `verifyEmail` spy now read the
 * outgoing REQUEST, which is strictly more: `?email=&token=` really are put on
 * the wire by the generated op rather than merely handed to a stand-in.
 * `renderWithWallow` supplies the router context the screen reads its SDK off,
 * and `createAuthHarness()` pins the harness origin to this app's root-mounted
 * API surface (Wallow-pu6a.5.5).
 *
 * The `isSafeReturnUrl` stub is gone too. It used to restate the real rule
 * (a stub answering a flat `true`/`false` would have proved nothing), and a
 * second copy of a security rule is a second copy to get wrong — the screen now
 * reaches the shipped guard in `packages/sdk/src/auth-oidc.ts`.
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
 * Consequently the rejection BODIES below ({@link wallowErrorBody}) carry the
 * exact members the seam hands the screen — `status`, plus the `code: "UNKNOWN"`
 * / `title: "Unknown error"` artefacts that prove the port is not secretly
 * relying on a reason string the seam never delivers, and that the no-leak test
 * further down has something real to catch.
 *
 * ── SUCCESS IS "RESOLVED", NOT "succeeded === true" ──────────────────────────
 *
 * The oracle reads `result.Succeeded` off the body. Through this seam that read
 * is redundant and is deliberately NOT ported: every 200 return from the
 * endpoint is `Ok(new { succeeded = true })` — there is no 200-with-false — and
 * every falsy case is a 400 that `unwrap()` has already turned into a throw. A
 * resolved promise IS success.
 */

const EMAIL = "ada@example.com";
const TOKEN = "verification-token";

/**
 * The endpoint behind `accountVerifyEmailOptions()` — `accountVerifyEmail` in the
 * generated client. `email` and `token` ride the QUERY STRING, so they are read
 * off `call.url` rather than `call.body`.
 */
const VERIFY_ENDPOINT = "/v1/identity/auth/verify-email";

const INVALID_TOKEN_STATUS = 400;
const SERVER_ERROR_STATUS = 500;

let harness: SdkHarness;

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/**
 * The failure body the endpoint's non-2xx returns hand the screen.
 *
 * `status` is what the screen narrows on; `code: "UNKNOWN"` / `title: "Unknown
 * error"` are the seam artefacts described in the header — carried on the wire
 * so the "never leaks the raw rejection" test has something real to catch.
 */
function wallowErrorBody(status: number) {
  return { status, code: "UNKNOWN", title: "Unknown error" };
}

/** The `email`/`token` a recorded call carried on its query string. */
function verifyParamsOf(call: SdkHarness["last"]) {
  const params: URLSearchParams = new URL(call?.url ?? "http://wallow.test/").searchParams;

  return { email: params.get("email"), token: params.get("token") };
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
  harness = createAuthHarness();
  harness.resolveJson({ succeeded: true });
});

describe("VerifyEmailConfirm — loading state", () => {
  it("shows only the spinner while the request is in flight", async () => {
    // Oracle: `_loading = true` is the field initialiser, so the very first
    // paint is the spinner — a never-settling request pins it there.
    harness.pending();

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expectOnlyState("loading");
  });

  it("verifies the email with the token from the link", async () => {
    harness.pending();

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    // Read off the RECORDED REQUEST rather than a spy: this proves the email and
    // token were serialised onto the wire by the real generated operation, which
    // a spy on a stand-in facade could never show.
    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(VERIFY_ENDPOINT);
    });
    expect(harness.last?.method).toBe("GET");
    expect(verifyParamsOf(harness.last)).toEqual({ email: EMAIL, token: TOKEN });
  });

  it("fires the verification exactly once", async () => {
    // The request is a side effect of mounting, not of rendering: a screen that
    // re-fired on every render would burn the single-use token.
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
    // Oracle's `catch` arm: a 500 is not a bad link, and must not tell the user
    // their link expired when it did not.
    harness.rejectJson(wallowErrorBody(SERVER_ERROR_STATUS), SERVER_ERROR_STATUS);

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    const error = page.getByTestId("verify-email-confirm-error");

    await expect.element(error).toHaveTextContent(/an error occurred/iu);
    await expect.element(error).not.toHaveTextContent(/expired/iu);
  });

  it("survives a rejection that is not WallowError-shaped at all", async () => {
    // The screen narrows structurally on `status`; a bare Error (a network
    // failure, say) has none, and must land on the generic arm rather than
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
    // `code: "UNKNOWN"` / `title: "Unknown error"` are seam artefacts, not
    // user-facing copy. The oracle shows curated messages only.
    harness.rejectJson(wallowErrorBody(INVALID_TOKEN_STATUS), INVALID_TOKEN_STATUS);

    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token={TOKEN} />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    expect(document.body.textContent).not.toMatch(/unknown error|UNKNOWN/u);
  });
});

describe("VerifyEmailConfirm — missing parameters", () => {
  it("refuses a link with no token without calling the endpoint", async () => {
    // Oracle: the guard runs before the try block — `_loading = false` and an
    // error message, no request. Pinning "nothing reached the transport" is the
    // point: a screen that "helpfully" sent `token: undefined` would 400 and
    // blame the user's link for the screen's own bug.
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
    // Oracle: `string.IsNullOrEmpty(Token)` — `?token=&email=x` is a malformed
    // link, not a token to try.
    await renderWithClient(<VerifyEmailConfirm email={EMAIL} token="" />);

    await expect.element(page.getByTestId("verify-email-confirm-error")).toBeInTheDocument();

    expect(harness.calls).toHaveLength(0);
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
    harness.rejectJson(wallowErrorBody(INVALID_TOKEN_STATUS), INVALID_TOKEN_STATUS);

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
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/verify-email/confirm", route: verifyEmailConfirmRoute }],
  });
}

describe("/verify-email/confirm route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path itself is the contract and is
    // not this task's to change (router.tsx is off-limits).
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
    expect(harness.last?.path).toBe(VERIFY_ENDPOINT);
    expect(verifyParamsOf(harness.last)).toEqual({ email: EMAIL, token: TOKEN });
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
