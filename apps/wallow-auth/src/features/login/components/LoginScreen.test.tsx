import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as loginRoute } from "../../../routes/login";
import { LoginScreen, type LoginScreenProps } from "./LoginScreen";

/**
 * Component spec for the Login screen's PASSWORD tab and the tab shell that
 * hosts it (Wallow-vec7.3.11 / 2.8a).
 *
 * This is the HEAD of a five-bead chain over one file: `.3.12` (magic-link),
 * `.3.13` (OTP), `.3.14` (external providers) and `.3.15` (MFA hand-off) all
 * extend `routes/login.tsx` + `features/login/*`. This spec therefore pins the
 * SHELL as a contract for them, and deliberately says NOTHING about the content
 * of the magic-link or OTP panels beyond "selecting that tab retires the
 * password panel".
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `login-error`, `login-tab-password`, `login-tab-magic-link`, `login-tab-otp`,
 * `login-email`, `login-forgot-password`, `login-password`, `login-remember-me`,
 * `login-submit`, `login-register-link`.
 *
 * TWO TESTIDS ARE INVENTED, because the oracle renders the elements but tags
 * neither (the scout's inventory documents this gap and mandates
 * `{page}-{element}` kebab names for it):
 *
 *   `login-signed-in`               the oracle's `_signedIn` success alert
 *   `login-mfa-enrollment-banner`   `<MfaEnrollmentBanner Visible=... />`
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly (that module is this app's only permitted
 * importer of the SDK). Per bd memories `vitest-resetmodules-breaks-instanceof-
 * across-graphs`, this file uses a plain `vi.mock` factory + `vi.hoisted` spies
 * and NEVER `vi.resetModules()`.
 *
 * ── THE FOUR BRANCHES ARE 200s, NOT REJECTIONS (verified in the controller) ───
 *
 * Unlike MfaChallenge, this endpoint reports THREE of its four outcomes inside a
 * SUCCESSFUL response body. `AccountController.Login`
 * (api/.../Controllers/AccountController.cs:65-165) returns:
 *
 *   200 { succeeded: false, mfaRequired: true }                       :100
 *   200 { succeeded: false, mfaEnrollmentRequired: true }             :125  (grace expired)
 *   200 { succeeded: true, mfaEnrollmentRequired: true,
 *         mfaGraceDeadline: <DateTimeOffset>, signInTicket: <t> }     :118  (in grace)
 *   200 { succeeded: true, signInTicket: <t> }                        :138
 *   401 { succeeded: false, error: "invalid_credentials" }            :83, :164
 *   423 { succeeded: false, error: "locked_out" }                     :149
 *   403 { succeeded: false, error: "email_not_confirmed" }            :154
 *
 * So `unwrap()` does NOT throw for the MFA branches — they arrive as a resolved
 * `Promise<unknown>` (the facade types `login` as `Promise<unknown>` because the
 * C# endpoint returns an anonymous `Ok(new { ... })` with no OpenAPI schema).
 * The SCREEN owns the narrowing at its own boundary, per bd memory
 * `untyped-sdk-response-fail-closed-pattern-wallow-auth`: narrow with the `in`
 * operator (no cast — the repo forbids `as any`), and reproduce C#'s STRICT
 * comparisons rather than JS truthiness. The `resultShapedLikeGarbage` test
 * below pins the fail-closed tail.
 *
 * Only the 401/423/403 arms reject. As of Wallow-vec7.7 `readCode` probes
 * `extensions.code > code > error`, so their token reaches the screen as
 * `WallowError.code` — hence the WallowError-SHAPED rejection fixtures.
 *
 * ── DISCLOSED: CODE-KEYING IS NOT BINDABLE ON THIS ENDPOINT ───────────────────
 *
 * bd memory `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-bind`
 * asks for an "unrecognised code on the SAME status falls back to generic" test.
 * That test CANNOT be written honestly here: unlike `mfa/verify` (two meanings
 * on one 401), each of this endpoint's failure statuses carries exactly ONE
 * token — 401 only ever means `invalid_credentials`, 423 only `locked_out`, 403
 * only `email_not_confirmed`. Code-keyed and status-keyed mappings are therefore
 * OBSERVATIONALLY IDENTICAL for every input the API can produce, and any test
 * claiming to bind one over the other would be pinning fiction.
 *
 * What IS bound instead, per the Wallow-vec7.7 rule ("match KNOWN tokens FIRST,
 * keep HTTP status as a FALLBACK"): `unrecognisedTokenOnKnownStatus` proves the
 * status fallback survives a token the screen has never heard of (a code-only
 * map would drop it to generic and mis-tell a locked-out user to retry), and
 * `unknownStatus` proves the generic tail exists. `code` is never rendered — the
 * oracle's `_ => result.Error` tails leak the raw machine token and that leak is
 * not ported.
 *
 * ── THE ORIGIN DIVERGENCE (inherited from Wallow-vec7.3.4/.3.6) ───────────────
 *
 * The oracle's `ApiBaseUrl` prepend (`BuildApiReturnUrl`, and the hand-rolled
 * exchange-ticket URL at L544-550) is deliberately NOT ported. This app's h3
 * server (`src/lib/auth-server.ts`) is a passthrough reverse proxy mounting
 * `/v1/**` and `/connect/**` at the ROOT, so this origin hosts them and the
 * origin argument is `""` (bd memory `wallow-auth-screens-must-pass-origin-same-
 * origin`). Prepending an absolute origin would send the browser cross-origin
 * and DROP the SameSite auth cookie the exchange-ticket endpoint just set —
 * which is the entire point of the ticket. `exchangesTheTicketAtThisOrigin`
 * pins it in both directions.
 *
 * ── THE GUARD PLACEMENT (bd memory `guard-where-the-client-picks-the-...`) ────
 *
 * `isSafeReturnUrl` is applied to the TICKET-EXCHANGE path ONLY, and this is not
 * a style choice — it is where the two poles land:
 *
 *   TICKET PATH — the client picks the destination (`location.href = <built from
 *     returnUrl>`), so the guard belongs here. Both poles are pinned:
 *     `passesTheRealAuthorizeReturnUrl` (the shape the server really sends) must
 *     PASS, and `refusesAnAbsoluteReturnUrl` must REFUSE. A guard tested only
 *     against attacks cannot tell "correct" from "refuses everything" — that is
 *     exactly the outage `.3.6` shipped and `.3.17` had to fix.
 *
 *   MFA PATH — the destination is the CONSTANT in-app path `/mfa/challenge`;
 *     `returnUrl` is inert query CARGO that MfaChallenge re-guards on arrival
 *     (shape-aware, post-`.3.17`). So NO guard here: `doesNotRefuseAnAbsolute-
 *     ReturnUrlOnTheMfaHandOff` pins that refusing would dead-end 100% of
 *     external-login users, and is the regression test for `.3.17`'s bug. What
 *     IS owed on a deferred guard is INJECTION, which
 *     `encodesTheReturnUrlAsASingleQueryValue` pins.
 *
 * Proof the password path's returnUrl really is relative (the premise the guard
 * rests on): `AuthorizationController.Authorize` builds it as `Request.PathBase +
 * Request.Path + Request.QueryString` (:53), rejects it unless `Url.IsLocalUrl`
 * (:62), and only then redirects to `{authUrl}/login?returnUrl=…` (:67). It is
 * relative by construction and pre-validated — disjoint from the ABSOLUTE,
 * allow-listed returnUrls `AccountController.ExternalLoginCallback` sends.
 *
 * ── THE BROWSER NAVIGATION SEAM (Wallow-xzha.3.1) ─────────────────────────────
 *
 * Migrated off jsdom onto vitest-browser-react. `window.location` is
 * `[Unforgeable]` in real Chromium, so the old `vi.stubGlobal("location", …)`
 * helper cannot shadow it and a live `location.href = <url>` would navigate the
 * runner iframe and tear it down (see vitest.config.ts NAVIGATION SEAM). The
 * ticket hand-off is therefore observed at the URL-BUILDER seam: every test that
 * used to assert `location.href` now asserts `buildExchangeTicketUrl` was called
 * with the exact `(origin, ticket, returnUrl)` — an equivalent, since the builder
 * is deterministic — and the builder mock returns a non-navigating hash sentinel
 * so the screen's assignment stays put. "No hand-off happened" (formerly
 * `location.href === ""`) is asserted as `buildExchangeTicketUrl` NOT called,
 * since that seam is the only writer of `location.href`.
 */

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  login: vi.fn(),
  isSafeReturnUrl: vi.fn(),
  buildExchangeTicketUrl: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {
      login: mocks.login,
    },
    oidc: {
      isSafeReturnUrl: mocks.isSafeReturnUrl,
      buildExchangeTicketUrl: mocks.buildExchangeTicketUrl,
    },
  }),
}));

// `importOriginal` MUST be spread: the route-level harness below needs the real
// `createRouter`/`RouterProvider`/`Outlet`/`createRootRoute`.
vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const EMAIL = "user@example.com";
const PASSWORD = "Sup3rSecret!";
const TICKET = "sign-in-ticket-xyz";

/**
 * A non-navigating hash sentinel the `buildExchangeTicketUrl` mock returns. The
 * screen assigns it to `location.href`; a hash-only assignment updates the
 * runner iframe's fragment WITHOUT reloading, so the browser test survives while
 * the real (navigable) URL is never emitted. The hand-off is asserted at the
 * builder's ARGS, not at this string.
 */
const EXCHANGE_SENTINEL = "#exchange-ticket-sentinel";

/**
 * The returnUrl `/connect/authorize` really sends (AuthorizationController.cs:53,
 * :67): relative, and already past `Url.IsLocalUrl`. This is the REAL-TRAFFIC
 * pole of the open-redirect guard — if the guard refuses this, every direct
 * login is dead.
 */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/** An absolute returnUrl from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

/** The bail target for an unsafe returnUrl, matching the ConsentScreen port. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** The oracle's blank-input guard (Login.razor L327). */
const BLANK_MESSAGE = "Please enter your email and password.";

/** The oracle's `result.Error` switch (Login.razor L345-350). */
const INVALID_CREDENTIALS_MESSAGE = "Invalid email or password.";
const LOCKED_OUT_MESSAGE = "Account locked. Try again later.";
const EMAIL_NOT_CONFIRMED_MESSAGE = "Please verify your email before signing in.";
const GENERIC_MESSAGE = "An error occurred. Please try again.";

/** The oracle's `catch (HttpRequestException)` arm (Login.razor L355). */
const UNREACHABLE_MESSAGE = "Unable to reach the server. Please try again later.";

/** The oracle's `Error` query-param switch (Login.razor L268-273). */
const EXTERNAL_LOGIN_FAILED_MESSAGE =
  "External sign-in failed. Please try again or use a different method.";
const SESSION_EXPIRED_MESSAGE = "Your session has expired. Please try again.";

/**
 * The success copy the login screen shows after a completed password reset
 * (Wallow-xzha.1.2). `ResetPasswordForm` navigates to `/login?message=password_reset`,
 * and the banner acknowledges the reset worked — the gap this bead closes. The key
 * phrase is pinned as a regex so the banner reads as an acknowledgment without
 * over-constraining the exact wording.
 */
const PASSWORD_RESET_NOTICE = /your password has been reset/iu;

/**
 * The real `isSafeReturnUrl` rule (packages/sdk/src/auth-oidc.ts), mirrored
 * rather than imported: screens may not import the SDK, so the seam is mocked,
 * and a mock that returned a constant would let the guard tests pass for the
 * wrong reason. Under 67 tests of its own in Wallow-vec7.2.2.
 */
function isSafeReturnUrlRule(url: string | null | undefined): boolean {
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  return url.startsWith("/") && !url.startsWith("//");
}

/**
 * What the facade really throws for this endpoint's failures. `title` stays
 * "Unknown error" — these endpoints emit no problem details, so no
 * human-readable title ever arrives and the screen must supply its own copy.
 */
function rejection(status: number, code: string): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status,
    code,
    title: "Unknown error",
  });
}

/**
 * A `fetch` failure: no `status`, no `code`. This is the TS shape of the
 * oracle's `catch (HttpRequestException)` arm, and it must NOT collapse into the
 * same copy as a 4xx — "the server said no" and "the server never answered" are
 * different instructions to the user, and the oracle keeps them apart.
 */
function networkRejection(): Error {
  return new TypeError("Failed to fetch");
}

/** An ISO-8601 `DateTimeOffset` (WallowUser.MfaGraceDeadline) N days from now. */
function deadlineInDays(days: number): string {
  return new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString();
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

/**
 * Render the screen as the OIDC hand-off would: a safe, relative returnUrl.
 *
 * `"returnUrl" in props` rather than `props.returnUrl ?? RETURN_URL`: the
 * no-returnUrl branch is itself under test, and a `??` helper would silently
 * substitute the default for an explicit `{ returnUrl: undefined }`, making
 * those tests exercise the PRESENT-returnUrl path while still failing red for a
 * right-looking reason (bd memory `red-phase-render-helpers-must-distinguish-
 * explicit-undefined`). Same for `""`, which is NOT nullish and must reach the
 * screen intact so the oracle's `IsNullOrEmpty` parity is observable.
 */
function renderScreen(props: Partial<LoginScreenProps> = {}) {
  const returnUrl: string | undefined = "returnUrl" in props ? props.returnUrl : RETURN_URL;

  return renderWithClient(<LoginScreen {...props} returnUrl={returnUrl} />);
}

/** Fill in the password tab and submit it — the oracle's `HandleLogin`. */
async function submitCredentials(
  user: ReturnType<typeof userEvent.setup>,
  email: string = EMAIL,
  password: string = PASSWORD,
) {
  if (email !== "") {
    await user.type(page.getByTestId("login-email"), email);
  }
  if (password !== "") {
    await user.type(page.getByTestId("login-password"), password);
  }
  await user.click(page.getByTestId("login-submit"));
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
  // Non-navigating sentinel: the screen assigns the builder's return value to
  // `location.href`, which would navigate the runner iframe in real Chromium.
  // Hand-off tests assert the builder's ARGS instead (see BROWSER NAVIGATION SEAM).
  mocks.buildExchangeTicketUrl.mockReturnValue(EXCHANGE_SENTINEL);
  mocks.login.mockResolvedValue({ succeeded: true, signInTicket: TICKET });
});

// ─────────────────────────────────────────────────────────────────────────────
// THE TAB SHELL — the contract .3.12/.3.13/.3.14 build on.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen tab shell", () => {
  it("renders all three tabs with password selected by default", async () => {
    await renderScreen();

    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "true");
    await expect
      .element(page.getByTestId("login-tab-magic-link"))
      .toHaveAttribute("aria-selected", "false");
    await expect
      .element(page.getByTestId("login-tab-otp"))
      .toHaveAttribute("aria-selected", "false");
    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
  });

  it("retires the password panel when another tab is selected", async () => {
    // The magic-link panel itself belongs to .3.12; all this bead owns is that
    // the shell can switch away from password and back.
    const user = userEvent.setup();
    await renderScreen();

    await user.click(page.getByTestId("login-tab-magic-link"));

    expect(page.getByTestId("login-password").query()).toBeNull();
    expect(page.getByTestId("login-submit").query()).toBeNull();
    await expect
      .element(page.getByTestId("login-tab-magic-link"))
      .toHaveAttribute("aria-selected", "true");
    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "false");
  });

  it("restores the password panel when its tab is selected again", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await user.click(page.getByTestId("login-tab-otp"));
    await user.click(page.getByTestId("login-tab-password"));

    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "true");
  });

  it("clears the error banner when switching tabs", async () => {
    // The oracle's `SwitchTab` resets `_errorMessage` — one error banner is
    // shared by all three tabs, so a password failure must not follow the user
    // into the magic-link tab.
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user, "", "");
    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();

    await user.click(page.getByTestId("login-tab-magic-link"));

    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("links to the register page, threading client_id and returnUrl as cargo", async () => {
    await renderScreen({ clientId: "web" });

    await expect
      .element(page.getByTestId("login-register-link"))
      .toHaveAttribute(
        "href",
        `/register?client_id=web&returnUrl=${encodeURIComponent(RETURN_URL)}`,
      );
  });

  it("links to a bare register page when there is no client_id or returnUrl", async () => {
    await renderScreen({ returnUrl: undefined });

    await expect
      .element(page.getByTestId("login-register-link"))
      .toHaveAttribute("href", "/register");
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// THE PASSWORD-RESET NOTICE — Wallow-xzha.1.2.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen password-reset notice", () => {
  it("shows a success banner when arriving with message=password_reset", async () => {
    // ResetPasswordForm sends the user here after a successful reset. The dead
    // param must now produce a visible acknowledgment.
    await renderScreen({ message: "password_reset" });

    await expect
      .element(page.getByTestId("login-password-reset-notice"))
      .toHaveTextContent(PASSWORD_RESET_NOTICE);
  });

  it("keeps the sign-in form usable beneath the notice", async () => {
    // Unlike the `signed-in` banner, this is an informational acknowledgment: the
    // user still has to sign in, so the password tab must NOT be retired.
    await renderScreen({ message: "password_reset" });

    await expect.element(page.getByTestId("login-password-reset-notice")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-submit")).toBeInTheDocument();
  });

  it("shows no notice when there is no message param", async () => {
    await renderScreen();

    expect(page.getByTestId("login-password-reset-notice").query()).toBeNull();
  });

  it("shows no notice for a message value it does not recognise", async () => {
    // `?message=` is attacker-constructable; only the known token renders a banner,
    // and no unrecognised value leaks into the DOM.
    await renderScreen({ message: "wat" });

    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(page.getByTestId("login-password-reset-notice").query()).toBeNull();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// THE PASSWORD TAB — fields, guards, submission.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen password tab", () => {
  it("links to the forgot-password screen", async () => {
    await renderScreen();

    await expect
      .element(page.getByTestId("login-forgot-password"))
      .toHaveAttribute("href", "/forgot-password");
  });

  it("refuses a blank email and password without calling the API", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user, "", "");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_MESSAGE);
    expect(mocks.login).not.toHaveBeenCalled();
  });

  it("refuses a whitespace-only password without calling the API", async () => {
    // `IsNullOrWhiteSpace`, not `IsNullOrEmpty`: "   " is blank to the oracle.
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user, EMAIL, "   ");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_MESSAGE);
    expect(mocks.login).not.toHaveBeenCalled();
  });

  it("submits the typed credentials with rememberMe false by default", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.login).toHaveBeenCalledWith({
        email: EMAIL,
        password: PASSWORD,
        rememberMe: false,
      });
    });
  });

  it("submits rememberMe true once the box is checked", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await user.click(page.getByTestId("login-remember-me"));
    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.login).toHaveBeenCalledWith({
        email: EMAIL,
        password: PASSWORD,
        rememberMe: true,
      });
    });
  });

  it("disables the submit button while the login is in flight", async () => {
    let release: () => void = () => undefined;
    mocks.login.mockReturnValue(
      new Promise((resolve) => {
        release = () => resolve({ succeeded: true, signInTicket: TICKET });
      }),
    );
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-submit")).toBeDisabled();

    release();
    await vi.waitFor(() => {
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalled();
    });
  });

  it("clears the previous error before retrying", async () => {
    // The oracle sets `_errorMessage = null` at the top of `HandleLogin`, so a
    // stale banner never overlaps an in-flight retry.
    mocks.login.mockRejectedValueOnce(rejection(401, "invalid_credentials"));
    let release: () => void = () => undefined;
    mocks.login.mockReturnValueOnce(
      new Promise((resolve) => {
        release = () => resolve({ succeeded: true, signInTicket: TICKET });
      }),
    );
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);
    await expect
      .element(page.getByTestId("login-error"))
      .toHaveTextContent(INVALID_CREDENTIALS_MESSAGE);

    await user.click(page.getByTestId("login-submit"));

    await vi.waitFor(() => {
      expect(page.getByTestId("login-error").query()).toBeNull();
    });
    release();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// FAILURE COPY — see "CODE-KEYING IS NOT BINDABLE" in the header.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen password failures", () => {
  it("maps 401 invalid_credentials to the oracle's credentials message", async () => {
    mocks.login.mockRejectedValue(rejection(401, "invalid_credentials"));
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect
      .element(page.getByTestId("login-error"))
      .toHaveTextContent(INVALID_CREDENTIALS_MESSAGE);
  });

  it("maps 423 locked_out to the oracle's lockout message", async () => {
    mocks.login.mockRejectedValue(rejection(423, "locked_out"));
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(LOCKED_OUT_MESSAGE);
  });

  it("maps 403 email_not_confirmed to the oracle's verify-email message", async () => {
    mocks.login.mockRejectedValue(rejection(403, "email_not_confirmed"));
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect
      .element(page.getByTestId("login-error"))
      .toHaveTextContent(EMAIL_NOT_CONFIRMED_MESSAGE);
  });

  it("falls back to the status when the token is unrecognised", async () => {
    // The Wallow-vec7.7 rule: known tokens first, HTTP status as a FALLBACK.
    // A code-only map would drop this to generic and stop telling a locked-out
    // user why retyping cannot help.
    mocks.login.mockRejectedValue(rejection(423, "some_new_token"));
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(LOCKED_OUT_MESSAGE);
  });

  it("falls back to the generic tail for a status this endpoint never documents", async () => {
    mocks.login.mockRejectedValue(rejection(500, "boom"));
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("never renders the raw machine token", async () => {
    // The oracle's `_ => result.Error` tail leaks it; that leak is not ported.
    mocks.login.mockRejectedValue(rejection(500, "some_new_token"));
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).not.toHaveTextContent("some_new_token");
  });

  it("tells the user the server is unreachable when the request never lands", async () => {
    // The oracle keeps `catch (HttpRequestException)` apart from its `_` tail:
    // "the server said no" and "the server never answered" are different
    // instructions. A network rejection carries neither code nor status.
    mocks.login.mockRejectedValue(networkRejection());
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(UNREACHABLE_MESSAGE);
  });

  it("fails closed when the 200 body is not a shape this screen understands", async () => {
    // `login` is typed `Promise<unknown>`; the screen narrows structurally. A
    // body with no `succeeded`/`mfaRequired`/`mfaEnrollmentRequired` must not be
    // mistaken for success, and must not navigate anywhere.
    mocks.login.mockResolvedValue("not an object at all");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("does not accept a stringly-typed succeeded flag", async () => {
    // C# compares `result.Succeeded` as a bool; JS truthiness would let the
    // non-empty string "false" through. Reproduce the strict comparison.
    mocks.login.mockResolvedValue({ succeeded: "false", signInTicket: TICKET });
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// BRANCH 1 — mfaRequired. 200 { succeeded: false, mfaRequired: true }.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen mfaRequired branch", () => {
  beforeEach(() => {
    mocks.login.mockResolvedValue({ succeeded: false, mfaRequired: true });
  });

  it("hands off to /mfa/challenge with the returnUrl as query cargo", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}`,
      });
    });
    // An in-app route reached through the client router, NOT a full page load:
    // the partial-auth cookie is already in the jar (the h3 proxy forwards
    // Set-Cookie verbatim), so there is nothing a reload would buy. The
    // ticket-exchange seam is the only writer of `location.href`, so its absence
    // is the browser-true equivalent of the old `location.href === ""`.
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("hands off to a bare /mfa/challenge when there is no returnUrl", async () => {
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: "/mfa/challenge" });
    });
  });

  it("does NOT refuse an absolute returnUrl on the MFA hand-off", async () => {
    // THE .3.17 REGRESSION TEST. `/mfa/challenge` is a CONSTANT same-origin
    // path and `returnUrl` is inert cargo the destination re-guards on arrival,
    // so the guard is DEFERRED here. Wiring `isSafeReturnUrl` in would refuse
    // 100% of external-login traffic — a total outage, not a security feature.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: "http://localhost:5002/login" });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/mfa/challenge?returnUrl=${encodeURIComponent("http://localhost:5002/login")}`,
      });
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("encodes the returnUrl so it cannot smuggle a second query key", async () => {
    // What a DEFERRED guard still owes: the cargo must land as ONE value. A
    // raw interpolation would let `&cookieRelay=…` split into its own key, and
    // ASP.NET binds a duplicated [FromQuery] as "a,b" -> parse failure ->
    // silently takes the wrong branch.
    const user = userEvent.setup();
    const smuggler = "/connect/authorize?client_id=web&cookieRelay=attacker";
    await renderScreen({ returnUrl: smuggler });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/mfa/challenge?returnUrl=${encodeURIComponent(smuggler)}`,
      });
    });
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// BRANCH 2 — mfaEnrollmentRequired, grace expired or absent.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen mfaEnrollmentRequired branch", () => {
  it("hands off to /mfa/enroll when enrollment is required with no grace deadline", async () => {
    // The wire shape of AccountController.cs:125 — grace expired server-side, so
    // no deadline is sent at all.
    mocks.login.mockResolvedValue({ succeeded: false, mfaEnrollmentRequired: true });
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/mfa/enroll?returnUrl=${encodeURIComponent(RETURN_URL)}`,
      });
    });
  });

  it("hands off to /mfa/enroll when the grace deadline has already passed", async () => {
    // The oracle re-checks `MfaGraceDeadline > UtcNow` client-side rather than
    // trusting the flag alone. This pins the COMPARISON: an implementation that
    // read the deadline as merely "present" would keep this user on the login
    // page with a banner instead of enrolling them.
    mocks.login.mockResolvedValue({
      succeeded: true,
      mfaEnrollmentRequired: true,
      mfaGraceDeadline: deadlineInDays(-1),
      signInTicket: TICKET,
    });
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/mfa/enroll?returnUrl=${encodeURIComponent(RETURN_URL)}`,
      });
    });
  });

  it("does not exchange the ticket when it sends the user to enroll", async () => {
    mocks.login.mockResolvedValue({
      succeeded: true,
      mfaEnrollmentRequired: true,
      mfaGraceDeadline: deadlineInDays(-1),
      signInTicket: TICKET,
    });
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalled();
    });
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// BRANCH 3 — grace-period messaging.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen MFA grace period", () => {
  const GRACE_DEADLINE = deadlineInDays(14);

  function graceResult() {
    return {
      succeeded: true,
      mfaEnrollmentRequired: true,
      mfaGraceDeadline: GRACE_DEADLINE,
      signInTicket: TICKET,
    };
  }

  it("shows the enrollment banner and signs the user in when there is no returnUrl", async () => {
    // The only configuration in which the oracle's banner is ever SEEN: with a
    // returnUrl the screen navigates away before it can be read (next test).
    mocks.login.mockResolvedValue(graceResult());
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-mfa-enrollment-banner")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("names the grace deadline in the banner", async () => {
    mocks.login.mockResolvedValue(graceResult());
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    const banner = page.getByTestId("login-mfa-enrollment-banner");
    await expect.element(banner).toHaveTextContent(/two-factor authentication/iu);
    await expect.element(banner).toHaveTextContent(
      new Date(GRACE_DEADLINE).toLocaleDateString("en-US", {
        month: "long",
        day: "numeric",
        year: "numeric",
      }),
    );
  });

  it("offers a route to enrollment from the banner", async () => {
    mocks.login.mockResolvedValue(graceResult());
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    const banner = page.getByTestId("login-mfa-enrollment-banner");
    await expect.element(banner).toBeInTheDocument();
    expect(banner.element().querySelector('a[href="/mfa/enroll"]')).not.toBeNull();
  });

  it("retires the tabs once the user is signed in", async () => {
    // The oracle renders the whole tab block inside `else` of `if (_signedIn)`.
    mocks.login.mockResolvedValue(graceResult());
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await expect.element(page.getByTestId("login-tab-password")).toBeInTheDocument();
    await submitCredentials(user);

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(page.getByTestId("login-tab-password").query()).toBeNull();
    expect(page.getByTestId("login-password").query()).toBeNull();
  });

  it("still exchanges the ticket during the grace period when a returnUrl is present", async () => {
    // Grace does NOT short-circuit the hand-off: the oracle sets the banner and
    // falls THROUGH to the returnUrl block, so the user keeps signing in.
    mocks.login.mockResolvedValue(graceResult());
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith({
      href: `/mfa/enroll?returnUrl=${encodeURIComponent(RETURN_URL)}`,
    });
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// BRANCH 4 — signInTicket, and the open-redirect guard's TWO poles.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen sign-in ticket exchange", () => {
  it("passes the real returnUrl the authorize endpoint sends", async () => {
    // POLE 1 — REAL TRAFFIC MUST PASS. AuthorizationController.cs:53 builds this
    // shape and :62 has already rejected it unless `Url.IsLocalUrl`. A guard
    // that refused this would dead-end every direct sign-in. Observed at the
    // builder seam: the assigned `location.href` is [Unforgeable] in a browser,
    // and the builder's args are the deterministic pre-image of that string.
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
  });

  it("exchanges the ticket at THIS origin, not at an absolute API origin", async () => {
    // SAME ORIGIN, NOT ApiBaseUrl. The h3 proxy mounts /v1/** at the root, so a
    // cross-origin exchange would drop the SameSite cookie the endpoint sets —
    // which is the entire purpose of the ticket. The `""` origin arg IS the
    // same-origin assertion: a relative exchange, never `localhost:5001`.
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
    });
  });

  it("refuses an absolute returnUrl before exchanging the ticket", async () => {
    // POLE 2 — THE ATTACK MUST BE REFUSED. Here the CLIENT picks the
    // destination, so the guard belongs on this path.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: EVIL_RETURN_URL });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("refuses a protocol-relative returnUrl", async () => {
    // `//evil.example.com` is the classic bypass of a naive `startsWith("/")`.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: "//evil.example.com/steal" });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("shows the signed-in state when there is no returnUrl", async () => {
    // The oracle's `else` arm: nowhere to send the user, so say so rather than
    // inventing a destination.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("treats an empty returnUrl as no returnUrl, not as an attack", async () => {
    // `IsNullOrEmpty(ReturnUrl)` parity. `""` is NOT nullish and IS unsafe by
    // `isSafeReturnUrl`, so a screen that guarded before checking emptiness
    // would send a perfectly ordinary user to /error. The oracle checks
    // emptiness FIRST; order is load-bearing.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: "" });

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("does not leave the sign-in button spinning after it refuses", async () => {
    // A refused login is terminal, but the form is still on screen; leaving it
    // disabled would strand the user with no way to retype the returnUrl-less
    // half of their journey.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: EVIL_RETURN_URL });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    await expect.element(page.getByTestId("login-submit")).toBeEnabled();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// THE ROUTE — the query string only exists once a URL is parsed by a router.
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Rendered through a real memory router rather than by poking at
 * `Route.options.component`: a bare render of a search-reading route component
 * ALWAYS dies on `router.stores` outside a `RouterProvider` (bd memory
 * `wallow-auth-route-tests-never-bare-render-a`). The root here is a throwaway —
 * the app's real `__root.tsx` renders `<html>`, and `src/router.tsx` is
 * off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    loginRoute.update({
      id: "/login",
      path: "/login",
      getParentRoute: () => rootRoute,
    } as any),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

describe("/login route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    await renderRouteAt("/login");

    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("renders without throwing when the link carries no query at all", async () => {
    // `/` redirects here with no query; every search param must be optional
    // rather than throwing a validation error at the user.
    await renderRouteAt("/login");

    await expect.element(page.getByTestId("login-submit")).toBeInTheDocument();
    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("threads returnUrl out of the query string into the ticket exchange", async () => {
    const user = userEvent.setup();
    await renderRouteAt(`/login?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=web`);

    await expect.element(page.getByTestId("login-email")).toBeInTheDocument();
    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
    });
  });

  it("threads client_id out of the query string into the register link", async () => {
    await renderRouteAt(`/login?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=web`);

    await expect
      .element(page.getByTestId("login-register-link"))
      .toHaveAttribute(
        "href",
        `/register?client_id=web&returnUrl=${encodeURIComponent(RETURN_URL)}`,
      );
  });

  it("surfaces external_login_failed from the error query param", async () => {
    await renderRouteAt("/login?error=external_login_failed");

    await expect
      .element(page.getByTestId("login-error"))
      .toHaveTextContent(EXTERNAL_LOGIN_FAILED_MESSAGE);
  });

  it("surfaces session_expired from the error query param", async () => {
    await renderRouteAt("/login?error=session_expired");

    await expect
      .element(page.getByTestId("login-error"))
      .toHaveTextContent(SESSION_EXPIRED_MESSAGE);
  });

  it("falls back to generic copy for an unrecognised error param", async () => {
    await renderRouteAt("/login?error=wat");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("does not resolve inherited Object keys from the error param", async () => {
    // `?error=` is a URL ANYONE can construct and send a victim. A plain
    // object/Record + bracket lookup resolves INHERITED keys, so `?error=toString`
    // hands `Object.prototype.toString` — a FUNCTION — to the renderer. Only a
    // `ReadonlyMap` + `.get()` sees just the keys explicitly put in it. The
    // benign `?error=wat` above survives a Record, so this is the test that
    // binds the Map: do not "simplify" it back to an object literal.
    await renderRouteAt("/login?error=toString");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("does not resolve the constructor key from the error param", async () => {
    await renderRouteAt("/login?error=constructor");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("survives an error param that the search parser turns into a boolean", async () => {
    // TanStack's default search parser JSON-parses EVERY query value before
    // `validateSearch` sees it, so `?error=true` arrives as the BOOLEAN true --
    // and the common `typeof x === "string" ? x : undefined` idiom would DROP it
    // to undefined, silently swallowing an error hand-back. `error` is compared
    // against literals, so re-stringify the scalar (bd memory
    // `tanstack-router-default-search-parser-json-parses-values`).
    await renderRouteAt("/login?error=true");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("ignores a non-scalar error param rather than throwing", async () => {
    // Arrays/objects reach the same false answer without a validation error --
    // a junk link must still render a usable login form.
    await renderRouteAt("/login?error=%5B1%2C2%5D");

    await expect.element(page.getByTestId("login-submit")).toBeInTheDocument();
  });

  it("surfaces the password-reset notice from the message query param", async () => {
    // The end-to-end assertion the bead asks for: the `message` param
    // ResetPasswordForm sends must survive `validateSearch` and reach the banner,
    // not be silently dropped.
    await renderRouteAt("/login?message=password_reset");

    await expect
      .element(page.getByTestId("login-password-reset-notice"))
      .toHaveTextContent(PASSWORD_RESET_NOTICE);
  });

  it("shows no notice for an unrecognised message param", async () => {
    await renderRouteAt("/login?message=wat");

    await expect.element(page.getByTestId("login-submit")).toBeInTheDocument();
    expect(page.getByTestId("login-password-reset-notice").query()).toBeNull();
  });
});
