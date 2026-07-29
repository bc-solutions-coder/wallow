import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "../../../test/harness";
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
 * ── TEST SEAM: THE REAL SDK OVER A FAKE TRANSPORT (Wallow-pu6a.5.1) ───────────
 *
 * `@bc-solutions-coder/testing/sdk-harness` fakes `fetch` and NOTHING else, so
 * the screen's whole pipeline — the request-scoped SDK off the router context ->
 * the generated operation -> the CSRF interceptor -> request serialization ->
 * response parsing -> React Query — runs inside these tests. That replaces the
 * hand-written module mock of an app-level SDK facade this file used to carry,
 * now forbidden by `src/sdk-test-seam.test.ts`. Two consequences:
 *
 *   • "Was the endpoint called, and with what" is read off the RECORDED REQUEST
 *     (`harness.calls`), not off a spy — so it also pins the URL, method and
 *     serialized body the screen really puts on the wire.
 *   • The `oidc` slice is no longer stubbed. `isSafeReturnUrl` and
 *     `buildExchangeTicketUrl` are PURE functions (packages/sdk/src/auth-oidc.ts),
 *     so the real ones run and the guard's two poles below state the GENUINE
 *     verdict instead of a local re-write of the rule that could drift from it.
 *
 * `renderWithWallow` supplies the router context the screen reads its SDK off,
 * and `createAuthHarness()` pins the harness origin to this app's root-mounted
 * API surface (Wallow-pu6a.5.5). Because that surface is rooted at the origin, the
 * recorded `call.path` is the bare endpoint path, with no `/api` prefix.
 *
 * The screen mounts `<ExternalProviders>` unconditionally, so the transport sees
 * that endpoint's GET too — every "the API was/was not called" assertion here
 * therefore filters `harness.calls` by path rather than counting them all.
 *
 * The ONE module mock left is `@tanstack/react-router`'s `useNavigate`: an in-app
 * router hand-off, not an SDK seam.
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
 * So the facade does NOT reject for the MFA branches. Under the unified error
 * contract (Wallow-pu6a.5.3) the generated op is `responseStyle: "data"` +
 * `throwOnError: true`, so a 200 RESOLVES its parsed body and only a non-2xx
 * rejects — and the facade types `login` as `Promise<unknown>` because the C#
 * endpoint returns an anonymous `Ok(new { … })` with no OpenAPI schema.
 * The SCREEN owns the narrowing at its own boundary, per bd memory
 * `untyped-sdk-response-fail-closed-pattern-wallow-auth`: narrow with the `in`
 * operator (no cast — the repo forbids `as any`), and reproduce C#'s STRICT
 * comparisons rather than JS truthiness. The `resultShapedLikeGarbage` test
 * below pins the fail-closed tail.
 *
 * Only the 401/423/403 arms reject. The fixtures for those arms are RFC 7807
 * problem details answered AT THE MATCHING HTTP STATUS, which is the strongest
 * shape available: the machine token sits in `extensions.code` (where
 * Wallow-vec7.7's `readCode` probes for it) AND the status is on the wire, so the
 * copy assertions bind whether the screen keys off the token or off the status.
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
 * exchange-ticket URL at L544-550) is deliberately NOT ported. This app's API
 * surface (`src/lib/api-passthrough.ts`) is a passthrough reverse proxy mounting
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
 * runner iframe and tear it down (see vitest.config.ts NAVIGATION SEAM).
 *
 * Now that the REAL `buildExchangeTicketUrl` runs (see the test seam above),
 * there is no builder spy left to assert on and the hand-off is observed where it
 * actually happens: a `navigate` listener on the Navigation API records
 * `destination.url` and `preventDefault()`s, so the URL the screen built is
 * captured while the runner stays put (bd memory `full-navigation-seam-for-
 * wallow-auth-screens-that`). The recorded array stands in for the old settable
 * `location.href`: a hand-off appends exactly ONE absolute URL, and "no hand-off
 * happened" (formerly `location.href === ""`) is the array staying EMPTY — that
 * assignment being the screen's only writer of `location.href`. Assertions read
 * the parsed URL, which is strictly stronger than the old builder-args check: it
 * pins the origin, path and encoding the browser would really be sent to.
 *
 * The listener is armed in `beforeEach` for EVERY test, not per test: the default
 * fixture is a successful sign-in with a ticket, so any submit could navigate, and
 * an unarmed test would tear the runner down instead of failing.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
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

/** `AccountController.Login` — the only endpoint the password tab drives. */
const LOGIN_ENDPOINT = "/v1/identity/auth/login";

/**
 * `AccountController.GetExternalProviders`. The shell mounts `<ExternalProviders>`
 * next to the tab panels, so this GET lands on the transport in EVERY test that
 * renders the screen. Owned by `.3.14`; answered here only to host it.
 */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

/** The path `buildExchangeTicketUrl` targets (packages/sdk/src/auth-oidc.ts:163). */
const EXCHANGE_PATH = "/v1/identity/auth/exchange-ticket";

const OK_STATUS = 200;
const NOT_FOUND_STATUS = 404;

/** The three statuses `AccountController.Login` rejects with, plus its generic tail. */
const UNAUTHORIZED_STATUS = 401;
const FORBIDDEN_STATUS = 403;
const LOCKED_STATUS = 423;
const SERVER_ERROR_STATUS = 500;

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

/** The API origin the oracle prepends to the exchange URL and this port does not. */
const API_ORIGIN = "localhost:5001";

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

let harness: SdkHarness;

/**
 * How the fake transport answers the login POST. Reprogrammed per test — the
 * dispatcher installed in `beforeEach` reads it on every call, so a test can
 * change this endpoint's behaviour without re-stating the other endpoints the
 * screen touches.
 */
let loginReply: () => Response | Promise<Response>;

/** Answer the login POST with `body` at 200 — the three MFA branches and success. */
function respondWithLogin(body: unknown): void {
  loginReply = () => Response.json(body, { status: OK_STATUS });
}

/**
 * Answer the login POST with RFC 7807 problem details at `status`.
 *
 * The token goes in `extensions.code` — where ASP.NET Core puts it and where
 * `readCode` (Wallow-vec7.7) probes for it — AND the status is the real transport
 * status, so these fixtures bind the copy assertions whether the screen keys off
 * the machine token or falls back to the status. `title` stays "Unknown error":
 * these endpoints ship no human-readable title, so the screen must supply its own
 * copy rather than echoing the server's.
 */
function problemResponse(status: number, code: string): Response {
  return Response.json(
    {
      type: "about:blank",
      title: "Unknown error",
      status,
      extensions: { code },
    },
    { status },
  );
}

function rejectLogin(status: number, code: string): void {
  loginReply = () => problemResponse(status, code);
}

/**
 * A `fetch` failure: the request never lands, so the rejection carries neither a
 * status nor a code. This is the TS shape of the oracle's
 * `catch (HttpRequestException)` arm, and it must NOT collapse into the same copy
 * as a 4xx — "the server said no" and "the server never answered" are different
 * instructions to the user, and the oracle keeps them apart.
 */
function failLoginTransport(): void {
  loginReply = () => {
    throw new TypeError("Failed to fetch");
  };
}

/** Every recorded request to the login endpoint, in order. */
function loginCalls() {
  return harness.calls.filter((call) => call.path === LOGIN_ENDPOINT);
}

/**
 * NAVIGATION SEAM (Wallow-xzha.3.1). See the header. The ticket hand-off is
 * `globalThis.location.href = …`, which in real Chromium would navigate the
 * runner iframe; listening for the Navigation API's `navigate` event lets us
 * record the destination and cancel it.
 */
interface NavigateEvent extends Event {
  readonly destination: { readonly url: string };
}
interface NavigationLike {
  addEventListener: (type: "navigate", handler: (event: NavigateEvent) => void) => void;
  removeEventListener: (type: "navigate", handler: (event: NavigateEvent) => void) => void;
}
const navigationApi: NavigationLike = (globalThis as unknown as { navigation: NavigationLike })
  .navigation;

/** Listeners registered by `captureHandoff`, torn down in `afterEach`. */
const navDisposers: Array<() => void> = [];

/** Arm the navigation seam and return the array the hand-off URL lands in. */
function captureHandoff(): string[] {
  const urls: string[] = [];
  const handler = (event: NavigateEvent): void => {
    urls.push(event.destination.url);
    // Cancel the navigation so assigning `location.href` does not tear the
    // Chromium runner down; the recorded URL is what we assert on.
    event.preventDefault();
  };
  navigationApi.addEventListener("navigate", handler);
  navDisposers.push(() => {
    navigationApi.removeEventListener("navigate", handler);
  });
  return urls;
}

/** Every hand-off URL this test recorded, newest last. Armed in `beforeEach`. */
let handoffs: string[];

/**
 * Wait for the ticket hand-off and return its target, parsed.
 *
 * The exchange URL is built by the REAL `buildExchangeTicketUrl` against the
 * `""` origin, so a correct screen produces a SAME-ORIGIN absolute URL whose
 * pathname is {@link EXCHANGE_PATH} and whose `ticket`/`returnUrl` are single,
 * properly-encoded query values.
 */
async function awaitHandoff(): Promise<URL> {
  await vi.waitFor(() => {
    expect(handoffs).toHaveLength(1);
  });

  return new URL(handoffs[0]);
}

/** An ISO-8601 `DateTimeOffset` (WallowUser.MfaGraceDeadline) N days from now. */
function deadlineInDays(days: number): string {
  return new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString();
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
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

/**
 * Toggle a checkbox the way a keyboard user does — focus it, press Space —
 * rather than by clicking the box (Wallow-m5aq.5.2).
 *
 * A click ON THE BOX is not a stable way to say this. The catalog's `Checkbox`
 * renders its root as a `<span role="checkbox">` sized purely by Tailwind
 * utilities, and the browser vitest project compiles no Tailwind, so that root
 * measures ZERO wide here: Playwright's actionability check never settles and
 * the click times out. Space on the focused root is the same user intent,
 * depends on no layout, and behaves identically on a raw `<input
 * type="checkbox">` — so every assertion written through this helper reads the
 * same before and after the migration onto the catalog.
 */
async function toggleCheckbox(
  user: ReturnType<typeof userEvent.setup>,
  testId: string,
): Promise<void> {
  const box = page.getByTestId(testId);

  await expect.element(box).toBeInTheDocument();
  (box.element() as HTMLElement).focus();
  await user.keyboard(" ");
}

beforeEach(() => {
  vi.clearAllMocks();
  handoffs = captureHandoff();
  harness = createAuthHarness();
  respondWithLogin({ succeeded: true, signInTicket: TICKET });
  harness.respond((call) => {
    if (call.path === LOGIN_ENDPOINT) {
      return loginReply();
    }

    // `<ExternalProviders>` mounts with the shell. An empty list is the
    // "no providers configured" answer and renders nothing — this file says
    // nothing about that section (it is `.3.14`'s).
    if (call.path === PROVIDERS_ENDPOINT) {
      return Response.json([], { status: OK_STATUS });
    }

    // The route-level tests carry `client_id=web`, so `/login` also asks for that
    // client's branding overlay. A bare 404 is the API's "no branding configured"
    // and leaves the fork's chrome in place — nothing this file looks at.
    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

afterEach(() => {
  navDisposers.forEach((dispose) => {
    dispose();
  });
  navDisposers.length = 0;
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
// THE CATALOG SWEEP — Wallow-m5aq.5.2.
//
// Two hand-rolled primitives live on this screen: a tab strip of raw
// `<button role="tab">` with NO panel wired to any of them, and a raw
// `<input type="checkbox">` for remember-me. Both are covered by the ui catalog
// (`Tabs`, `Checkbox`), and the tests below ask for the sweep in the only terms
// a user — or a screen reader — can observe, so they keep meaning whatever the
// markup ends up being.
//
// The strip already says WHICH tab is selected (`aria-selected`, pinned by the
// tab-shell block above). What it cannot say today is what that selection
// CONTROLS: there is no `role="tabpanel"`, so nothing associates the fields
// below the strip with the tab that produced them, and nothing takes the two
// unselected tabs out of the tab sequence.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen tab shell: the WAI-ARIA tabs contract", () => {
  it("wires the visible panel to the tab that owns it", async () => {
    await renderScreen();

    const panel = page.getByRole("tabpanel");
    await expect.element(panel).toBeInTheDocument();

    const panelElement: HTMLElement = panel.element() as HTMLElement;
    const selectedTab: HTMLElement = page
      .getByTestId("login-tab-password")
      .element() as HTMLElement;

    // The association runs BOTH ways — the panel names its tab, the tab names its
    // panel — which is what lets a screen reader move between the two.
    expect(panelElement.getAttribute("aria-labelledby")).toBe(selectedTab.id);
    expect(selectedTab.getAttribute("aria-controls")).toBe(panelElement.id);

    // And it is the REAL panel, not an empty shell beside the fields.
    expect(panelElement.querySelector('[data-testid="login-password"]')).not.toBeNull();
  });

  it("mounts only the selected tab's panel", async () => {
    // One panel at a time, as the hand-rolled `if` chain does today: a second,
    // hidden password form left in the DOM is a second form users can tab into.
    await renderScreen();

    await expect.element(page.getByRole("tabpanel")).toBeInTheDocument();

    expect(page.getByRole("tabpanel").all()).toHaveLength(1);
  });

  it("keeps only the selected tab in the tab sequence", async () => {
    // The ARIA tab pattern: Tab reaches the STRIP once, then the arrow keys move
    // within it. Three separately tabbable buttons make the user press Tab three
    // times to get past a control they have already answered.
    await renderScreen();

    await expect.element(page.getByTestId("login-tab-password")).toHaveAttribute("tabindex", "0");
    await expect
      .element(page.getByTestId("login-tab-magic-link"))
      .toHaveAttribute("tabindex", "-1");
    await expect.element(page.getByTestId("login-tab-otp")).toHaveAttribute("tabindex", "-1");
  });

  it("moves focus along the strip with the arrow keys and activates on Enter", async () => {
    const user = userEvent.setup();
    await renderScreen();

    const passwordTab = page.getByTestId("login-tab-password");
    await expect.element(passwordTab).toBeInTheDocument();
    (passwordTab.element() as HTMLElement).focus();

    await user.keyboard("{ArrowRight}");

    // Focus MOVES; the selection does not follow it. A user browsing the strip
    // with the keyboard has not yet chosen anything, and switching tabs under
    // them would throw away whatever they had typed in the panel below.
    await expect.element(page.getByTestId("login-tab-magic-link")).toHaveAttribute("tabindex", "0");
    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "true");
    expect(document.activeElement).toBe(page.getByTestId("login-tab-magic-link").element());

    await user.keyboard("{Enter}");

    await expect
      .element(page.getByTestId("login-tab-magic-link"))
      .toHaveAttribute("aria-selected", "true");
    await expect.element(page.getByTestId("login-magic-link-email")).toBeInTheDocument();
  });
});

describe("LoginScreen password tab: the remember-me box", () => {
  it("is reachable as a checkbox named by its label", async () => {
    // The `htmlFor`/`id` pairing asserted through what it buys: a box whose name
    // is its label. A migration that drops the pairing leaves an unnamed control.
    await renderScreen();

    await expect.element(page.getByRole("checkbox", { name: "Remember me" })).toBeInTheDocument();
  });

  it("publishes its checked state as aria-checked", async () => {
    // A raw `<input type="checkbox">` keeps its state in the `checked` PROPERTY,
    // which no attribute reflects; the catalog's Checkbox publishes it as
    // `aria-checked`, the same way this screen's tabs publish `aria-selected`.
    const user = userEvent.setup();
    await renderScreen();

    const box = page.getByTestId("login-remember-me");
    await expect.element(box).toHaveAttribute("aria-checked", "false");

    await toggleCheckbox(user, "login-remember-me");

    await expect.element(box).toHaveAttribute("aria-checked", "true");
    await expect.element(box).toBeChecked();
  });

  it("toggles when its label is clicked", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await expect.element(page.getByTestId("login-remember-me")).toBeInTheDocument();
    await user.click(page.getByText("Remember me"));

    await expect.element(page.getByTestId("login-remember-me")).toBeChecked();
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
    expect(loginCalls()).toHaveLength(0);
  });

  it("refuses a whitespace-only password without calling the API", async () => {
    // `IsNullOrWhiteSpace`, not `IsNullOrEmpty`: "   " is blank to the oracle.
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user, EMAIL, "   ");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_MESSAGE);
    expect(loginCalls()).toHaveLength(0);
  });

  it("submits the typed credentials with rememberMe false by default", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(loginCalls()).toHaveLength(1);
    });
    expect(loginCalls()[0].method).toBe("POST");
    expect(loginCalls()[0].body).toEqual({
      email: EMAIL,
      password: PASSWORD,
      rememberMe: false,
    });
  });

  it("submits rememberMe true once the box is checked", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await toggleCheckbox(user, "login-remember-me");
    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(loginCalls()).toHaveLength(1);
    });
    expect(loginCalls()[0].body).toEqual({
      email: EMAIL,
      password: PASSWORD,
      rememberMe: true,
    });
  });

  it("disables the submit button while the login is in flight", async () => {
    let release: () => void = () => undefined;
    loginReply = async () =>
      await new Promise<Response>((resolve) => {
        release = () => {
          resolve(Response.json({ succeeded: true, signInTicket: TICKET }, { status: OK_STATUS }));
        };
      });
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    // Wait for the request to REACH the transport before asserting: the submit
    // button goes disabled the moment the form starts submitting, which is a tick
    // or two before `fetch` is called, and releasing into that gap would leave the
    // never-settling responder installed forever.
    await vi.waitFor(() => {
      expect(loginCalls()).toHaveLength(1);
    });
    await expect.element(page.getByTestId("login-submit")).toBeDisabled();

    release();
    await awaitHandoff();
  });

  it("clears the previous error before retrying", async () => {
    // The oracle sets `_errorMessage = null` at the top of `HandleLogin`, so a
    // stale banner never overlaps an in-flight retry.
    let release: () => void = () => undefined;
    let attempt = 0;
    loginReply = () => {
      attempt += 1;
      if (attempt === 1) {
        return problemResponse(UNAUTHORIZED_STATUS, "invalid_credentials");
      }

      return new Promise<Response>((resolve) => {
        release = () => {
          resolve(Response.json({ succeeded: true, signInTicket: TICKET }, { status: OK_STATUS }));
        };
      });
    };
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
    rejectLogin(UNAUTHORIZED_STATUS, "invalid_credentials");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect
      .element(page.getByTestId("login-error"))
      .toHaveTextContent(INVALID_CREDENTIALS_MESSAGE);
  });

  it("maps 423 locked_out to the oracle's lockout message", async () => {
    rejectLogin(LOCKED_STATUS, "locked_out");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(LOCKED_OUT_MESSAGE);
  });

  it("maps 403 email_not_confirmed to the oracle's verify-email message", async () => {
    rejectLogin(FORBIDDEN_STATUS, "email_not_confirmed");
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
    rejectLogin(LOCKED_STATUS, "some_new_token");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(LOCKED_OUT_MESSAGE);
  });

  it("falls back to the generic tail for a status this endpoint never documents", async () => {
    rejectLogin(SERVER_ERROR_STATUS, "boom");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("never renders the raw machine token", async () => {
    // The oracle's `_ => result.Error` tail leaks it; that leak is not ported.
    rejectLogin(SERVER_ERROR_STATUS, "some_new_token");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).not.toHaveTextContent("some_new_token");
  });

  it("tells the user the server is unreachable when the request never lands", async () => {
    // The oracle keeps `catch (HttpRequestException)` apart from its `_` tail:
    // "the server said no" and "the server never answered" are different
    // instructions. A network rejection carries neither code nor status.
    failLoginTransport();
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(UNREACHABLE_MESSAGE);
  });

  it("fails closed when the 200 body is not a shape this screen understands", async () => {
    // `login` is typed `Promise<unknown>`; the screen narrows structurally. A
    // body with no `succeeded`/`mfaRequired`/`mfaEnrollmentRequired` must not be
    // mistaken for success, and must not navigate anywhere.
    respondWithLogin("not an object at all");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(handoffs).toHaveLength(0);
  });

  it("does not accept a stringly-typed succeeded flag", async () => {
    // C# compares `result.Succeeded` as a bool; JS truthiness would let the
    // non-empty string "false" through. Reproduce the strict comparison.
    respondWithLogin({ succeeded: "false", signInTicket: TICKET });
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(handoffs).toHaveLength(0);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// BRANCH 1 — mfaRequired. 200 { succeeded: false, mfaRequired: true }.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen mfaRequired branch", () => {
  beforeEach(() => {
    respondWithLogin({ succeeded: false, mfaRequired: true });
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
    // the partial-auth cookie is already in the jar (the passthrough proxy forwards
    // Set-Cookie verbatim), so there is nothing a reload would buy. The
    // ticket-exchange seam is the only writer of `location.href`, so its absence
    // is the browser-true equivalent of the old `location.href === ""`.
    expect(handoffs).toHaveLength(0);
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
    expect(handoffs).toHaveLength(0);
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
    respondWithLogin({ succeeded: false, mfaEnrollmentRequired: true });
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
    respondWithLogin({
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
    respondWithLogin({
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
    expect(handoffs).toHaveLength(0);
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
    respondWithLogin(graceResult());
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-mfa-enrollment-banner")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(handoffs).toHaveLength(0);
  });

  it("names the grace deadline in the banner", async () => {
    respondWithLogin(graceResult());
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
    respondWithLogin(graceResult());
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    const banner = page.getByTestId("login-mfa-enrollment-banner");
    await expect.element(banner).toBeInTheDocument();
    expect(banner.element().querySelector('a[href="/mfa/enroll"]')).not.toBeNull();
  });

  it("retires the tabs once the user is signed in", async () => {
    // The oracle renders the whole tab block inside `else` of `if (_signedIn)`.
    respondWithLogin(graceResult());
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
    respondWithLogin(graceResult());
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    const target: URL = await awaitHandoff();
    expect(target.pathname).toBe(EXCHANGE_PATH);
    expect(target.searchParams.get("ticket")).toBe(TICKET);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
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
    // NAVIGATION seam: `location.href` is [Unforgeable] in a browser, so the
    // cancelled `navigate` event carries the URL the screen really built. The
    // returnUrl is read back through a real URL parser, so this also pins that it
    // survives as ONE properly-encoded query value.
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    const target: URL = await awaitHandoff();
    expect(target.pathname).toBe(EXCHANGE_PATH);
    expect(target.searchParams.get("ticket")).toBe(TICKET);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
  });

  it("exchanges the ticket at THIS origin, not at an absolute API origin", async () => {
    // SAME ORIGIN, NOT ApiBaseUrl. The passthrough proxy mounts /v1/** at the root, so a
    // cross-origin exchange would drop the SameSite cookie the endpoint sets —
    // which is the entire purpose of the ticket. The screen passes `""` as the
    // origin, so the URL the browser is handed must resolve against THIS document's
    // origin and never name the API's.
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    const target: URL = await awaitHandoff();
    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_PATH);
    expect(handoffs[0]).not.toContain(API_ORIGIN);
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
    expect(handoffs).toHaveLength(0);
  });

  it("refuses a protocol-relative returnUrl", async () => {
    // `//evil.example.com` is the classic bypass of a naive `startsWith("/")`.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: "//evil.example.com/steal" });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(handoffs).toHaveLength(0);
  });

  it("shows the signed-in state when there is no returnUrl", async () => {
    // The oracle's `else` arm: nowhere to send the user, so say so rather than
    // inventing a destination.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(handoffs).toHaveLength(0);
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
    expect(handoffs).toHaveLength(0);
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
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/login", route: loginRoute }],
  });
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

    const target: URL = await awaitHandoff();
    expect(target.pathname).toBe(EXCHANGE_PATH);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
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
