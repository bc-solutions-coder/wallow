import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { Route as loginRoute } from "@app/routes/login";
import { LoginScreen, type LoginScreenProps } from "./LoginScreen";

/**
 * Component spec for the Login screen's MAGIC-LINK tab (Wallow-vec7.3.12 / 2.8b).
 *
 * This bead adds a PANEL and ONE `TabPanel` branch to the shell Wallow-vec7.3.11
 * established. It does not re-derive the branch table: on a verify response the
 * panel calls `onAuthResult(body)` and STOPS, and the shell's single
 * `authDispositionOf` (`../auth-result`) owns the MFA branches, the open-redirect
 * guard and the ticket exchange. The tests below that assert a ticket exchange or
 * a refusal are therefore INTEGRATION tests through the real shell — which is the
 * point: they prove this panel hands its result to the one branch table.
 *
 * `LoginScreen.test.tsx` (.3.11's 52 tests) is NOT edited by this bead; it
 * deliberately says nothing about this panel's content beyond "selecting the tab
 * retires the password panel".
 *
 * ── TEST SEAM: THE REAL SDK OVER A FAKE TRANSPORT (Wallow-pu6a.5.1) ──────────
 *
 * The module mock of an app-level SDK facade is GONE, and `src/sdk-test-seam.test.ts`
 * now forbids it (along with mocking the SDK barrel or its `/query` entry) for every
 * spec under `src/features/**` and `src/routes/**`. A fake SDK object pins the
 * SHAPE THE TEST IMAGINED, not the one the SDK exports: it cannot notice a renamed
 * method, a moved endpoint, a changed request body or a rejection that stopped
 * being the shape the screen reads.
 *
 * So this file drives the REAL request-scoped SDK off the router context — real
 * generated operations, real error interceptor — over a fake `fetch` installed by
 * `createAuthHarness()` (`@bc-solutions-coder/testing/sdk-harness`).
 * `renderWithWallow` supplies that router context, and `createAuthHarness()` pins
 * the harness origin to this app's root-mounted API surface, which is why every
 * recorded `path` below is the bare endpoint path (Wallow-pu6a.5.5).
 *
 * The assertions therefore moved from "was this spy called with X" to "what
 * request actually reached the wire": `harness.calls` (filtered by path, because
 * the shell's `<ExternalProviders>` also issues a GET), `.method`, `.body` (decoded
 * JSON) and `.url` (which carries the query string — `verify` sends its token
 * there, not in a body). That is strictly stronger: it pins the endpoint, verb and
 * payload the API really receives.
 *
 * ── THE WIRE, VERIFIED IN THE CONTROLLER (not in a client DTO) ───────────────
 *
 * `AccountController` (api/.../Identity/Wallow.Identity.Api/Controllers/AccountController.cs):
 *
 *   POST /v1/identity/auth/passwordless/magic-link                        :824
 *     200 { succeeded: true }                       ALWAYS on the happy path — and
 *                                                   also for an address with NO
 *                                                   account (PasswordlessService.cs:63-67
 *                                                   returns success to defeat email
 *                                                   enumeration).
 *     400 { succeeded: false, error: "Rate limit exceeded. Please try again later." }
 *                                                   the ONLY failure the service
 *                                                   can produce (:56-60).
 *
 *   GET  /v1/identity/auth/passwordless/magic-link/verify                 :838
 *     200 { succeeded: true, email, signInTicket }                        :848
 *     401 { succeeded: false, error: "Invalid token format." }            PasswordlessService.cs:95
 *     401 { succeeded: false, error: "Invalid token." }                   PasswordlessService.cs:105
 *     401 { succeeded: false, error: "Token expired or already used." }   PasswordlessService.cs:112
 *
 * TWO CONSEQUENCES the oracle does not prepare you for:
 *
 * 1. THE FAILURES ARE REJECTIONS, NOT 200 BODIES. Unlike `auth.login` — where
 *    three of four outcomes ride inside a 200 — every magic-link failure is a
 *    non-2xx, and the generated operations are emitted with `throwOnError: true`,
 *    so the oracle's `if (result.Succeeded) … else` arms are reached through
 *    `onError`, not `onSuccess`. As of Wallow-vec7.7 `readCode` probes
 *    `extensions.code > code > error`, so the `error` member of the bare
 *    `{ succeeded, error }` body arrives as `WallowError.code` — which is why the
 *    fixtures below are the REAL WIRE BODY answered at the REAL STATUS
 *    (`Response.json({ succeeded: false, error: <token> }, { status })`) rather
 *    than a hand-built rejection object. The error interceptor
 *    (`packages/sdk/src/runtime-config.ts`) does that translation for real here.
 *
 * 2. THE TOKENS ARE ENGLISH SENTENCES, and one of the oracle's literals is DEAD.
 *    `HandleVerifyMagicLink` switches on `"invalid_token" or "Token expired or
 *    already used."`, but `ValidateMagicLinkAsync` NEVER returns `"invalid_token"` —
 *    its live spelling is `"Invalid token."` (PasswordlessService.cs:105). The
 *    dead literal is not ported and the LIVE one the author plainly meant is mapped
 *    in its place. `invalidTokenGetsTheExpiredCopy` pins that decision.
 *
 * ── CODE-KEYING IS BINDABLE HERE (unlike on `login` — see .3.11's note) ──────
 *
 * bd memory `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-bind`
 * asks for a test that a status-keyed map cannot pass. `.3.11` could not write one
 * honestly (each `login` failure status carries exactly ONE token). THIS endpoint
 * can: `verify` answers 401 with THREE tokens carrying TWO meanings — "your link is
 * spent, get a new one" vs "something went wrong". So `invalidTokenFormatIsNotThe-
 * ExpiredCopy` and `unrecognisedTokenOnTheSameStatus` bind the code map with REAL
 * inputs the API really produces, not fiction. A blanket `401 -> expired` rule
 * fails both.
 *
 * No status FALLBACK is kept on verify, for the same reason: 401 does not identify
 * a failure on its own here (bd memory `wallow-auth-screens-key-error-copy-on-
 * wallowerror-code-not-http-status` keeps a status fallback "only where status
 * identifies a failure alone").
 *
 * ── THE SENTENCES ARE NEVER RENDERED ─────────────────────────────────────────
 *
 * A server-authored English sentence is still a machine token: it is matched
 * against and never shown. `neverRendersTheRawServerSentence` (send and verify)
 * pins that the oracle's `_ => result.Error`-style leak is not ported.
 *
 * ── NAVIGATION SEAM (Wallow-xzha.3.1 browser migration, Wallow-pu6a.5.1) ─────
 *
 * `window.location` is `[Unforgeable]` in real Chromium, so the jsdom-only
 * `vi.stubGlobal("location", …)` hack is gone. The screen hands off with
 * `globalThis.location.href = buildExchangeTicketUrl(origin, ticket, returnUrl)`,
 * and the builder is now the REAL pure function (`packages/sdk/src/auth-oidc.ts`)
 * — there is no builder spy left to assert on, so the hand-off is observed where
 * it actually happens: a `navigate` listener on the Navigation API records
 * `destination.url` and `preventDefault()`s, capturing the URL the screen built
 * while the runner stays put (bd memory `full-navigation-seam-for-wallow-auth-
 * screens-that`). The recorded array stands in for the old settable
 * `location.href`: a hand-off appends exactly ONE absolute URL, and "no exchange
 * happened" (formerly `expect(buildExchangeTicketUrl).not.toHaveBeenCalled()`) is
 * the array staying EMPTY — that assignment being the screen's only writer of
 * `location.href`. Asserting the parsed URL is stronger than the old builder-args
 * check: it pins the origin, path and encoding the browser would really be sent to.
 *
 * The listener is armed in `beforeEach` for EVERY test, not per test: the default
 * verify fixture is a successful sign-in with a ticket, so any render carrying a
 * token could navigate, and an unarmed test would tear the runner down instead of
 * failing.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

// `importOriginal` MUST be spread: the route harness needs the real
// `createRouter`/`RouterProvider`/`Outlet`/`createRootRoute`.
vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const EMAIL = "user@example.com";
const CLIENT_ID = "web";
const TICKET = "sign-in-ticket-xyz";

/** `AccountController.SendMagicLink` (:824) — the tab's POST. */
const SEND_ENDPOINT = "/v1/identity/auth/passwordless/magic-link";

/** `AccountController.VerifyMagicLink` (:838) — a GET; its token rides the query. */
const VERIFY_ENDPOINT = "/v1/identity/auth/passwordless/magic-link/verify";

/**
 * `AccountController.GetExternalProviders`. The shell mounts `<ExternalProviders>`
 * next to the tab panels, so this GET lands on the transport in EVERY test that
 * renders the screen — which is why the "was it called" assertions below filter
 * `harness.calls` by path instead of counting them. Owned by `.3.14`; answered
 * here only to host it.
 */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

/** The path `buildExchangeTicketUrl` targets (packages/sdk/src/auth-oidc.ts:163). */
const EXCHANGE_PATH = "/v1/identity/auth/exchange-ticket";

const OK_STATUS = 200;
const NOT_FOUND_STATUS = 404;

/** The statuses this endpoint pair rejects with: send 400, verify 401. */
const BAD_REQUEST_STATUS = 400;
const UNAUTHORIZED_STATUS = 401;

/**
 * A token shaped like the one the service really mints:
 * `Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))` + `"."` + an HMAC
 * signature (PasswordlessService.cs:70-72). Base64 of 32 bytes is 44 chars ending
 * in `=`, so a real token carries `+`, `/` and `=` and can never be JSON-parsed
 * into a number — which is what makes the route's `typeof === "string"` read safe.
 */
const MAGIC_LINK_TOKEN = "n2Vv3sQ+K1/aB9cd7EfGhIjKlMnOpQrStUvWxYz0123=.mZ8pQ7rS6tU5vW4xY3z=";

/**
 * The returnUrl `/connect/authorize` really sends (AuthorizationController.cs:53,
 * :67), and which `MagicLinkRequestedNotificationHandler.cs:21-26` copies onto the
 * EMAILED link: relative, and already past `Url.IsLocalUrl`. This is the
 * REAL-TRAFFIC pole — if the guard refuses this, every magic-link sign-in is dead.
 */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/** An absolute returnUrl from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

/** The bail target for an unsafe returnUrl, matching the ConsentScreen port. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** This endpoint's machine tokens — matched against, NEVER rendered. */
const RATE_LIMITED_TOKEN = "Rate limit exceeded. Please try again later.";
const EXPIRED_TOKEN = "Token expired or already used.";
const INVALID_TOKEN = "Invalid token.";
const INVALID_TOKEN_FORMAT_TOKEN = "Invalid token format.";

/** The oracle's blank-email guard (Login.razor:376). */
const BLANK_EMAIL_MESSAGE = "Please enter your email.";

/** The oracle's sent alert (Login.razor:111). */
const SENT_MESSAGE = "Check your email for a magic link.";

/** The oracle's `HandleVerifyMagicLink` switch (Login.razor:419-420). */
const EXPIRED_MESSAGE =
  "This magic link has expired or has already been used. Please request a new one.";
const VERIFY_FAILED_MESSAGE = "An error occurred verifying the magic link. Please try again.";

/**
 * DIVERGENCE, disclosed on the bead: the oracle shows its generic copy for every
 * send failure, but the ONLY send failure the service can produce is the rate
 * limit — and "An error occurred. Please try again." tells a rate-limited user to
 * do the one thing that cannot work. This is the same call `.3.11` made on the 423
 * lockout fallback, for the same reason.
 */
const RATE_LIMITED_MESSAGE =
  "Too many sign-in link requests. Please wait a few minutes and try again.";

/** Shared with the password tab (`../auth-result`), not re-invented here. */
const GENERIC_MESSAGE = "An error occurred. Please try again.";
const UNREACHABLE_MESSAGE = "Unable to reach the server. Please try again later.";

let harness: SdkHarness;

/**
 * How the fake transport answers each of this tab's two endpoints. Reprogrammed
 * per test — the dispatcher installed in `beforeEach` reads them on every call, so
 * a test can change ONE endpoint's behaviour without re-stating the others the
 * screen touches.
 */
let sendReply: () => Response | Promise<Response>;
let verifyReply: () => Response | Promise<Response>;

/** Answer the send POST with `body` at 200. */
function respondWithSend(body: unknown): void {
  sendReply = () => Response.json(body, { status: OK_STATUS });
}

/** Answer the verify GET with `body` at 200. */
function respondWithVerify(body: unknown): void {
  verifyReply = () => Response.json(body, { status: OK_STATUS });
}

/**
 * The REAL failure body these two endpoints ship: a bare
 * `{ succeeded: false, error: "<token>" }` anonymous object, at the real status.
 * They emit no problem details at all, so no human-readable title ever arrives and
 * the screen must supply its own copy. `readCode` (Wallow-vec7.7) probes
 * `extensions.code > code > error`, so the sentence under `error` is what reaches
 * the screen as `WallowError.code` — the mapping under test.
 */
function failureResponse(status: number, token: string): Response {
  return Response.json({ succeeded: false, error: token }, { status });
}

function rejectSend(status: number, token: string): void {
  sendReply = () => failureResponse(status, token);
}

function rejectVerify(status: number, token: string): void {
  verifyReply = () => failureResponse(status, token);
}

/**
 * A `fetch` failure: the request never lands, so the rejection carries neither a
 * status nor a code. The TS shape of the oracle's `catch (HttpRequestException)`
 * arm, which it keeps DISTINCT from its generic tail on both handlers.
 */
function failSendTransport(): void {
  sendReply = () => {
    throw new TypeError("Failed to fetch");
  };
}

function failVerifyTransport(): void {
  verifyReply = () => {
    throw new TypeError("Failed to fetch");
  };
}

/** Every recorded request to the send endpoint, in order. */
function sendCalls() {
  return harness.calls.filter((call) => call.path === SEND_ENDPOINT);
}

/** Every recorded request to the verify endpoint, in order. */
function verifyCalls() {
  return harness.calls.filter((call) => call.path === VERIFY_ENDPOINT);
}

/**
 * NAVIGATION SEAM (Wallow-xzha.3.1 / Wallow-pu6a.5.1). See the header. The ticket
 * hand-off is `globalThis.location.href = …`, which in real Chromium would
 * navigate the runner iframe; listening for the Navigation API's `navigate` event
 * lets us record the destination and cancel it.
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
 * The exchange URL is built by the REAL `buildExchangeTicketUrl` against the `""`
 * origin, so a correct screen produces a SAME-ORIGIN absolute URL whose pathname
 * is {@link EXCHANGE_PATH} and whose `ticket`/`returnUrl` are single,
 * properly-encoded query values.
 */
async function awaitHandoff(): Promise<URL> {
  await vi.waitFor(() => {
    expect(handoffs).toHaveLength(1);
  });

  return new URL(handoffs[0]);
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/**
 * Render the screen as the OIDC hand-off would: a safe, relative returnUrl.
 *
 * `"x" in props` rather than `props.x ?? DEFAULT`: the absent-`returnUrl` and
 * absent-`magicLinkToken` branches are themselves under test, and a `??` helper
 * would silently substitute the default for an explicit `{ x: undefined }`,
 * making those tests exercise the PRESENT path while still failing red for a
 * right-looking reason (bd memory `red-phase-render-helpers-must-distinguish-
 * explicit-undefined`). Same for `""`, which is not nullish.
 */
function renderScreen(props: Partial<LoginScreenProps> = {}) {
  const returnUrl: string | undefined = "returnUrl" in props ? props.returnUrl : RETURN_URL;

  return renderWithClient(<LoginScreen {...props} returnUrl={returnUrl} />);
}

/** Open the magic-link tab — the oracle's `SwitchTab(LoginTab.MagicLink)`. */
async function openMagicLinkTab(user: ReturnType<typeof userEvent.setup>) {
  await user.click(page.getByTestId("login-tab-magic-link"));
}

/** Fill in the magic-link tab and submit it — the oracle's `HandleSendMagicLink`. */
async function submitEmail(user: ReturnType<typeof userEvent.setup>, email: string = EMAIL) {
  if (email !== "") {
    await user.type(page.getByTestId("login-magic-link-email"), email);
  }
  await user.click(page.getByTestId("login-magic-link-submit"));
}

beforeEach(() => {
  vi.clearAllMocks();
  handoffs = captureHandoff();
  harness = createAuthHarness();
  respondWithSend({ succeeded: true });
  respondWithVerify({ succeeded: true, email: EMAIL, signInTicket: TICKET });
  harness.respond((call) => {
    // The verify path EXTENDS the send path, so it is matched first — and both are
    // exact comparisons, never `startsWith`.
    if (call.path === VERIFY_ENDPOINT) {
      return verifyReply();
    }

    if (call.path === SEND_ENDPOINT) {
      return sendReply();
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
// SENDING THE LINK — the oracle's `HandleSendMagicLink`.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen magic-link tab: sending", () => {
  it("shows the email field and send button in place of the password panel", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);

    await expect.element(page.getByTestId("login-magic-link-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-magic-link-submit")).toBeInTheDocument();
    // The oracle's tabs are an `else if` chain: one panel at a time.
    expect(page.getByTestId("login-password").query()).toBeNull();
  });

  it("does not send anything merely because the tab was opened", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);

    await expect.element(page.getByTestId("login-magic-link-submit")).toBeInTheDocument();
    expect(sendCalls()).toHaveLength(0);
  });

  it("refuses a blank email without calling the API", async () => {
    // The oracle's `IsNullOrWhiteSpace(_email)` guard: a blank send cannot succeed
    // and would spend one of the address's rate-limit allowance.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user, "");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);
    expect(sendCalls()).toHaveLength(0);
  });

  it("refuses a whitespace-only email without calling the API", async () => {
    // WHITEspace, not just empty — `IsNullOrWhiteSpace`, so "   " is blank.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user, "   ");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);
    expect(sendCalls()).toHaveLength(0);
  });

  it("sends the typed email with the returnUrl and client_id the link carried", async () => {
    // The oracle's `SendMagicLinkAsync(_email, ReturnUrl, ClientId)`. Both ride
    // along so `MagicLinkRequestedNotificationHandler.cs:21-31` can put them back
    // on the EMAILED link and land the user in the same OIDC flow they left.
    const user = userEvent.setup();
    await renderScreen({ clientId: CLIENT_ID });

    await openMagicLinkTab(user);
    await submitEmail(user);

    await vi.waitFor(() => {
      expect(sendCalls()).toHaveLength(1);
    });
    expect(sendCalls()[0].method).toBe("POST");
    expect(sendCalls()[0].body).toEqual({
      email: EMAIL,
      returnUrl: RETURN_URL,
      clientId: CLIENT_ID,
    });
  });

  it("sends without cargo when the link carries no returnUrl or client_id", async () => {
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await openMagicLinkTab(user);
    await submitEmail(user);

    await vi.waitFor(() => {
      expect(sendCalls()).toHaveLength(1);
    });
    // Read off the WIRE, so `{ returnUrl: undefined }` is not a member that was
    // sent as `undefined` — `JSON.stringify` drops it, and the address arriving
    // alone is exactly the "no cargo" claim.
    expect(sendCalls()[0].body).toEqual({ email: EMAIL });
  });

  it("shows the sent confirmation and retires the form", async () => {
    // The oracle's `_magicLinkSent` swaps the form for the alert: the link is in
    // the user's inbox, and a form still on screen invites a second send that the
    // rate limiter will refuse.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-magic-link-sent")).toHaveTextContent(SENT_MESSAGE);
    expect(page.getByTestId("login-magic-link-submit").query()).toBeNull();
  });

  it("does not name the address in the confirmation", async () => {
    // The API answers 200 { succeeded: true } for an address with NO account
    // (PasswordlessService.cs:63-67) precisely so the screen cannot be used to
    // enumerate users. Echoing the typed address back is harmless on its own, but
    // the confirmation is the one artefact both backend outcomes share and it stays
    // a constant (bd memory `anti-enumeration-pattern-for-endpoints-that-must-not`).
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-magic-link-sent")).not.toHaveTextContent(EMAIL);
  });

  it("disables the send button while the request is in flight", async () => {
    // The oracle's `Disabled="_isSubmitting"`. A double send burns the rate limit.
    let release: (value: Response) => void = () => undefined;
    sendReply = () =>
      new Promise<Response>((resolve) => {
        release = resolve;
      });
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    // The button goes disabled a tick BEFORE the request reaches the transport, so
    // wait for the call to land or `release` is still the no-op initialiser.
    await vi.waitFor(() => {
      expect(sendCalls()).toHaveLength(1);
    });
    await expect.element(page.getByTestId("login-magic-link-submit")).toBeDisabled();

    release(Response.json({ succeeded: true }, { status: OK_STATUS }));
    await expect.element(page.getByTestId("login-magic-link-sent")).toBeInTheDocument();
  });

  it("clears a previous error before retrying", async () => {
    // The oracle's `_errorMessage = null` at the top of `HandleSendMagicLink`: a
    // stale banner hanging over an in-flight retry is a lie.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user, "");
    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);

    await submitEmail(user);

    await expect.element(page.getByTestId("login-magic-link-sent")).toBeInTheDocument();
    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("clears the sent confirmation when the user leaves the tab and returns", async () => {
    // The oracle's `SwitchTab` resets `_magicLinkSent`, so the tab is usable again.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);
    await expect.element(page.getByTestId("login-magic-link-sent")).toBeInTheDocument();

    await user.click(page.getByTestId("login-tab-password"));
    await openMagicLinkTab(user);

    await expect.element(page.getByTestId("login-magic-link-submit")).toBeInTheDocument();
    expect(page.getByTestId("login-magic-link-sent").query()).toBeNull();
  });

  it("fails closed when the send response is not a shape this screen understands", async () => {
    // The operation types this `Promise<unknown>` (the C# endpoint returns an
    // anonymous `Ok(new { … })` with no OpenAPI schema), so the screen narrows at
    // its own boundary and a body it cannot read is NOT a sent link.
    respondWithSend({});
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(page.getByTestId("login-magic-link-sent").query()).toBeNull();
  });

  it("does not accept a stringly-typed succeeded flag", async () => {
    // C#'s `result.Succeeded` is a `bool`; JS truthiness would happily accept the
    // STRING "false". Strict `=== true` is the only place that can be rejected.
    respondWithSend({ succeeded: "false" });
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(page.getByTestId("login-magic-link-sent").query()).toBeNull();
  });
});

describe("LoginScreen magic-link tab: send failures", () => {
  it("tells a rate-limited user to wait rather than to try again", async () => {
    rejectSend(BAD_REQUEST_STATUS, RATE_LIMITED_TOKEN);
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(RATE_LIMITED_MESSAGE);
    expect(page.getByTestId("login-magic-link-sent").query()).toBeNull();
  });

  it("never renders the raw server sentence on a failed send", async () => {
    // The token is a server-authored English sentence, which makes it TEMPTING to
    // render — but it is still a machine token, and the oracle's `_ => result.Error`
    // family of tails is what this port refuses to reproduce.
    rejectSend(BAD_REQUEST_STATUS, RATE_LIMITED_TOKEN);
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect
      .element(page.getByTestId("login-error"))
      .not.toHaveTextContent("Rate limit exceeded");
  });

  it("falls back to the generic tail for a send failure it has never heard of", async () => {
    rejectSend(BAD_REQUEST_STATUS, "some_new_token");
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("tells the user the server is unreachable when the send never lands", async () => {
    // The oracle's `catch (HttpRequestException)`, kept DISTINCT from the generic
    // tail: "the server said no" and "the server never answered" are different
    // instructions, and collapsing them tells a user with no network to retype an
    // address that was fine.
    failSendTransport();
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(UNREACHABLE_MESSAGE);
  });

  it("leaves the form up after a failed send so the user can retry", async () => {
    failSendTransport();
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-magic-link-submit")).toBeEnabled();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// AUTO-VERIFY ON LOAD — the oracle's `OnInitializedAsync` (:255-259).
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen magic-link auto-verify", () => {
  it("opens on the magic-link tab when the link carries a token", async () => {
    // The oracle's `HandleVerifyMagicLink` sets `_activeTab = LoginTab.MagicLink`
    // before it does anything else: the user clicked a link in their inbox and must
    // land where the outcome will be reported.
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect
      .element(page.getByTestId("login-tab-magic-link"))
      .toHaveAttribute("aria-selected", "true");
    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "false");
  });

  it("verifies the token on load without the user clicking anything", async () => {
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });
    // A GET, so the token rides the QUERY STRING — which lives on `url`, not
    // `path`, and must arrive as a single properly-encoded value (a real token
    // carries `+`, `/` and `=`).
    expect(verifyCalls()[0].method).toBe("GET");
    expect(
      new URL(verifyCalls()[0].url, globalThis.location.origin).searchParams.get("token"),
    ).toBe(MAGIC_LINK_TOKEN);
  });

  it("opens on the password tab and verifies nothing when there is no token", async () => {
    // The guard against an implementation that is always in the magic-link tab.
    await renderScreen({ magicLinkToken: undefined });

    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "true");
    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("treats an empty token as no token", async () => {
    // `IsNullOrEmpty(MagicLinkToken)` parity: `""` is not nullish, and verifying it
    // would spend a request to be told the format is invalid.
    await renderScreen({ magicLinkToken: "" });

    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "true");
    expect(verifyCalls()).toHaveLength(0);
  });

  it("verifies exactly once even though the failure re-renders the screen", async () => {
    // A magic-link token is ONE-TIME USE (PasswordlessService.cs:117 deletes the
    // Redis key), so a second verify can only ever fail. The failure sets the
    // shell's banner, which re-renders this panel and hands it a fresh
    // `onAuthResult`/`onError` identity — so effect-dep correctness alone cannot
    // hold the line and a `useRef` "started" latch must (bd memory
    // `exactly-once-server-mutations-in-react-need-a-ref-not-just-deps`; React
    // StrictMode double-invokes effects in dev for the same reason).
    rejectVerify(UNAUTHORIZED_STATUS, EXPIRED_TOKEN);
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    // Give a runaway effect the chance to fire again before counting.
    await new Promise((resolve) => {
      setTimeout(resolve, 50);
    });

    expect(verifyCalls()).toHaveLength(1);
  });

  it("exchanges the sign-in ticket at THIS origin for the returnUrl the email carried", async () => {
    // ── THE LEGITIMATE PATH. If this test fails, magic-link sign-in is DEAD ──
    //
    // The emailed link is `{authUrl}/login?magicLinkToken=…&returnUrl=…&client_id=…`
    // (MagicLinkRequestedNotificationHandler.cs:21-31), and its returnUrl is the
    // RELATIVE `/connect/authorize?…` the authorize endpoint minted. So the whole
    // point of the tab is: token in, ticket out, exchange, back into the OIDC flow.
    //
    // The origin is `""`. The oracle's `ApiBaseUrl` prepend is NOT ported: this
    // app's API surface is a passthrough reverse proxy mounting `/v1/**` at the ROOT,
    // and an absolute origin would send the browser cross-origin and DROP the
    // SameSite auth cookie the exchange endpoint just set — which is the entire
    // point of the ticket (bd memory `wallow-auth-screens-must-pass-origin-same-origin`).
    //
    // Browser seam: `location.href` is unforgeable in Chromium, so the hand-off is
    // recorded by the Navigation API listener and asserted as a PARSED URL — the
    // same-origin `origin` is exactly the "no localhost:5001" claim the old
    // `location.href` assertions made.
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN, clientId: CLIENT_ID });

    const target: URL = await awaitHandoff();

    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_PATH);
    expect(target.searchParams.get("ticket")).toBe(TICKET);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
  });

  it("signs the user in when the emailed link carried no returnUrl", async () => {
    // A magic link requested from a bare `/login` has no OIDC flow to resume, so
    // there is nowhere to send the user — say so rather than invent a destination.
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN, returnUrl: undefined });

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    // The ticket exchange is the screen's only writer of `location.href`, so an
    // EMPTY recording is the browser-true "no exchange happened".
    expect(handoffs).toHaveLength(0);
  });

  it("refuses an absolute returnUrl before exchanging the ticket", async () => {
    // The CLIENT picks this destination (`location.href` built from returnUrl), so
    // the guard belongs here — and it is the SHELL's, reached by handing the raw
    // body up. REFUSE, don't sanitize.
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN, returnUrl: EVIL_RETURN_URL });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    // The ticket exchange is the screen's only writer of `location.href`, so an
    // EMPTY recording is the browser-true "no exchange happened".
    expect(handoffs).toHaveLength(0);
  });

  it("hands an MFA-required verify response to the shell's branch table", async () => {
    // Proof this panel does NOT re-derive the navigation: it reports the RAW body
    // up and the shell's one `authDispositionOf` decides. `verify` cannot itself
    // return an MFA branch today (AccountController.cs:848 answers only
    // `{ succeeded, email, signInTicket }`) — what is pinned here is the WIRING,
    // which is what stops three panels from disagreeing about where a
    // half-authenticated user lands.
    respondWithVerify({ succeeded: false, mfaRequired: true });
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}`,
      });
    });
  });
});

describe("LoginScreen magic-link verify failures", () => {
  it("maps a spent token to the oracle's expired copy", async () => {
    rejectVerify(UNAUTHORIZED_STATUS, EXPIRED_TOKEN);
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(EXPIRED_MESSAGE);
  });

  it("maps a bad signature to the expired copy too", async () => {
    // The oracle names `"invalid_token"`, which the service NEVER returns: its live
    // spelling is `"Invalid token."` (PasswordlessService.cs:100-106, a failed HMAC
    // comparison). The dead literal is not ported; the live one the author plainly
    // meant is mapped in its place.
    rejectVerify(UNAUTHORIZED_STATUS, INVALID_TOKEN);
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(EXPIRED_MESSAGE);
  });

  it("does not promise a new link will help when the token is malformed", async () => {
    // ── THE TEST THAT BINDS THE CODE MAP ──
    //
    // `"Invalid token format."` (PasswordlessService.cs:91-95) rides the SAME 401 as
    // the two tokens above but is NOT in the oracle's expired list. A blanket
    // `401 -> expired` rule passes every other failure test in this file and fails
    // THIS one — which is the whole reason it exists (bd memory `code-keyed-error-
    // mapping-needs-an-unrecognised-code-test-to-bind`). Do not "simplify" it away.
    rejectVerify(UNAUTHORIZED_STATUS, INVALID_TOKEN_FORMAT_TOKEN);
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(VERIFY_FAILED_MESSAGE);
  });

  it("falls back to the verify tail for a token on the same status it has never heard of", async () => {
    rejectVerify(UNAUTHORIZED_STATUS, "some_new_token");
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(VERIFY_FAILED_MESSAGE);
  });

  it("never renders the raw server sentence on a failed verify", async () => {
    rejectVerify(UNAUTHORIZED_STATUS, EXPIRED_TOKEN);
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect
      .element(page.getByTestId("login-error"))
      .not.toHaveTextContent("Token expired or already used");
  });

  it("never renders the token itself", async () => {
    // The token is a live credential until it is redeemed. It is in the URL, but
    // that is not a reason to paint it into the page.
    rejectVerify(UNAUTHORIZED_STATUS, EXPIRED_TOKEN);
    const { container } = await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    expect(container.textContent).not.toContain(MAGIC_LINK_TOKEN);
  });

  it("tells the user the server is unreachable when the verify never lands", async () => {
    failVerifyTransport();
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(UNREACHABLE_MESSAGE);
  });

  it("offers the send form again after a failed verify", async () => {
    // "Please request a new one" is only advice if the user can act on it.
    rejectVerify(UNAUTHORIZED_STATUS, EXPIRED_TOKEN);
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-magic-link-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-magic-link-submit")).toBeEnabled();
  });

  it("does not exchange a ticket when the verify fails", async () => {
    rejectVerify(UNAUTHORIZED_STATUS, EXPIRED_TOKEN);
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    // The ticket exchange is the screen's only writer of `location.href`, so an
    // EMPTY recording is the browser-true "no exchange happened".
    expect(handoffs).toHaveLength(0);
    expect(page.getByTestId("login-signed-in").query()).toBeNull();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// THE ROUTE — the query string only exists once a URL is parsed by a router.
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Rendered through a real memory router rather than by poking at
 * `Route.options.component`: a bare render of a search-reading route component
 * ALWAYS dies on `router.stores` outside a `RouterProvider` (bd memory
 * `wallow-auth-route-tests-never-bare-render-a`). Mirrors `LoginScreen.test.tsx`.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/login", route: loginRoute }],
  });
}

/** The emailed link, exactly as `MagicLinkRequestedNotificationHandler.cs:21-31` builds it. */
function emailedLink(token: string, returnUrl: string, clientId: string): string {
  return (
    `/login?magicLinkToken=${encodeURIComponent(token)}` +
    `&returnUrl=${encodeURIComponent(returnUrl)}` +
    `&client_id=${encodeURIComponent(clientId)}`
  );
}

describe("/login route: magic link", () => {
  it("threads magicLinkToken out of the query string into the verify call", async () => {
    await renderRouteAt(emailedLink(MAGIC_LINK_TOKEN, RETURN_URL, CLIENT_ID));

    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });
    expect(
      new URL(verifyCalls()[0].url, globalThis.location.origin).searchParams.get("token"),
    ).toBe(MAGIC_LINK_TOKEN);
  });

  it("completes the emailed link end to end: token in, ticket exchanged", async () => {
    // ── THE WHOLE FEATURE, THROUGH THE REAL ROUTE. ──
    // A user clicks the link in their inbox and lands back in the OIDC flow they
    // started. Nothing here is a hostile input: if this fails, the tab is an outage.
    // Browser seam: the recorded hand-off URL IS the old `location.href`
    // contains-returnUrl assertion, parsed.
    await renderRouteAt(emailedLink(MAGIC_LINK_TOKEN, RETURN_URL, CLIENT_ID));

    const target: URL = await awaitHandoff();

    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_PATH);
    expect(target.searchParams.get("ticket")).toBe(TICKET);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
  });

  it("renders the password tab for a bare /login and verifies nothing", async () => {
    await renderRouteAt("/login");

    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("threads returnUrl and client_id from the query string into a send", async () => {
    const user = userEvent.setup();
    await renderRouteAt(
      `/login?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=${CLIENT_ID}`,
    );

    await openMagicLinkTab(user);
    await submitEmail(user);

    await vi.waitFor(() => {
      expect(sendCalls()).toHaveLength(1);
    });
    expect(sendCalls()[0].body).toEqual({
      email: EMAIL,
      returnUrl: RETURN_URL,
      clientId: CLIENT_ID,
    });
  });

  it("ignores a magicLinkToken the search parser turned into a number", async () => {
    // TanStack's default parser JSON-parses EVERY query value before
    // `validateSearch` sees it (bd memory `tanstack-router-default-search-parser-
    // json-parses-values`), so `?magicLinkToken=123` arrives as the NUMBER 123.
    // Unlike `error`, this one is NOT re-stringified: it is a credential compared
    // by the server, not matched against literals, and a real token always carries
    // base64 padding (`=`) so it can never be parsed into a scalar. A junk link
    // must still render a usable form rather than throw a validation error.
    await renderRouteAt("/login?magicLinkToken=123");

    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(verifyCalls()).toHaveLength(0);
  });
});
