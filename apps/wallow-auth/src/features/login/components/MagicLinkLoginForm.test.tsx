import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import type { SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { Route as loginRoute } from "@app/routes/login";
import { LoginScreen, type LoginScreenProps } from "./LoginScreen";

/**
 * The login screen's magic-link tab: sending, and auto-verify on load.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so assertions read the
 * recorded request. The panel reports its result up; the shell's
 * `authDispositionOf` owns the ticket exchange and the refusals.
 *
 * Every failure is a non-2xx whose `error` sentence arrives as `WallowError.code`.
 * Verify's 401 carries three tokens with TWO meanings, so copy is keyed on the
 * token, never on the status alone.
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

/** The tab's POST. */
const SEND_ENDPOINT = "/v1/identity/auth/passwordless/magic-link";

/** A GET, so its token rides the query string, not a body. */
const VERIFY_ENDPOINT = "/v1/identity/auth/passwordless/magic-link/verify";

/**
 * The shell mounts `<ExternalProviders>` next to the tab panels, so this GET
 * lands on the transport in EVERY test that renders the screen — which is why the
 * "was it called" assertions filter `harness.calls` by path.
 */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

/** The path `buildExchangeTicketUrl` targets. */
const EXCHANGE_PATH = "/v1/identity/auth/exchange-ticket";

const OK_STATUS = 200;
const NOT_FOUND_STATUS = 404;

/** The statuses this endpoint pair rejects with: send 400, verify 401. */
const BAD_REQUEST_STATUS = 400;
const UNAUTHORIZED_STATUS = 401;

/**
 * A token shaped like the one the service really mints: base64 of 32 bytes plus
 * an HMAC signature. It carries `+`, `/` and `=`, so it can never be JSON-parsed
 * into a number — which is what makes the route's scalar read safe.
 */
const MAGIC_LINK_TOKEN = "n2Vv3sQ+K1/aB9cd7EfGhIjKlMnOpQrStUvWxYz0123=.mZ8pQ7rS6tU5vW4xY3z=";

/**
 * The returnUrl `/connect/authorize` sends, copied verbatim onto the EMAILED
 * link: relative, and already past the server's own local-url check. The
 * REAL-TRAFFIC pole — if the guard refuses this, magic-link sign-in is dead.
 */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/** An absolute returnUrl from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

/** The bail target for an unsafe returnUrl. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** This endpoint's machine tokens — matched against, NEVER rendered. */
const RATE_LIMITED_TOKEN = "Rate limit exceeded. Please try again later.";
const EXPIRED_TOKEN = "Token expired or already used.";
const INVALID_TOKEN = "Invalid token.";
const INVALID_TOKEN_FORMAT_TOKEN = "Invalid token format.";

const BLANK_EMAIL_MESSAGE = "Please enter your email.";

const SENT_MESSAGE = "Check your email for a magic link.";

const EXPIRED_MESSAGE =
  "This magic link has expired or has already been used. Please request a new one.";
const VERIFY_FAILED_MESSAGE = "An error occurred verifying the magic link. Please try again.";

/**
 * The ONLY send failure the service can produce is the rate limit, so the copy is
 * specific: a generic "please try again" tells a rate-limited user to do the one
 * thing that cannot work.
 */
const RATE_LIMITED_MESSAGE =
  "Too many sign-in link requests. Please wait a few minutes and try again.";

/** Shared with the password tab, not re-invented here. */
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
 * The REAL failure body these endpoints ship: a bare
 * `{ succeeded: false, error: "<token>" }` at the real status. They emit no
 * problem details, so no human-readable title ever arrives and the screen must
 * supply its own copy; the sentence under `error` reaches it as
 * `WallowError.code`.
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
 * status nor a code, and it must stay DISTINCT from the generic tail.
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
 * The ticket hand-off is `globalThis.location.href = …`, which in real Chromium
 * would navigate the runner iframe away. `location` is `[Unforgeable]` and cannot
 * be stubbed, so the Navigation API's `navigate` event is the seam.
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
    // Cancel it: a live navigation tears the Chromium runner down.
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
 * Wait for the ticket hand-off and return its target, parsed. The real
 * `buildExchangeTicketUrl` runs against the `""` origin, so a correct screen
 * produces a SAME-ORIGIN absolute URL.
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
 * absent-`magicLinkToken` branches are themselves under test, and a `??` default
 * would silently substitute for an explicit `{ x: undefined }`. Same for `""`,
 * which is not nullish.
 */
function renderScreen(props: Partial<LoginScreenProps> = {}) {
  const returnUrl: string | undefined = "returnUrl" in props ? props.returnUrl : RETURN_URL;

  return renderWithClient(<LoginScreen {...props} returnUrl={returnUrl} />);
}

/** Open the magic-link tab. */
async function openMagicLinkTab(user: ReturnType<typeof userEvent.setup>) {
  await user.click(page.getByTestId("login-tab-magic-link"));
}

/** Fill in the magic-link tab and submit it. */
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
    // "no providers configured" answer and renders nothing.
    if (call.path === PROVIDERS_ENDPOINT) {
      return Response.json([], { status: OK_STATUS });
    }

    // The route-level tests carry `client_id=web`, so `/login` also asks for that
    // client's branding overlay. A bare 404 is "no branding configured".
    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

afterEach(() => {
  navDisposers.forEach((dispose) => {
    dispose();
  });
  navDisposers.length = 0;
});

describe("LoginScreen magic-link tab: sending", () => {
  it("shows the email field and send button in place of the password panel", async () => {
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);

    await expect.element(page.getByTestId("login-magic-link-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-magic-link-submit")).toBeInTheDocument();
    // One panel at a time.
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
    // A blank send cannot succeed and would spend the address's rate-limit
    // allowance.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user, "");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);
    expect(sendCalls()).toHaveLength(0);
  });

  it("refuses a whitespace-only email without calling the API", async () => {
    // Whitespace-only input is blank, not merely empty.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user, "   ");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);
    expect(sendCalls()).toHaveLength(0);
  });

  it("sends the typed email with the returnUrl and client_id the link carried", async () => {
    // returnUrl and clientId ride along so the EMAILED link can carry them back
    // and land the user in the same OIDC flow they left.
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
    // Read off the WIRE: `JSON.stringify` drops an `undefined` member, so the
    // address arriving alone is exactly the "no cargo" claim.
    expect(sendCalls()[0].body).toEqual({ email: EMAIL });
  });

  it("shows the sent confirmation and retires the form", async () => {
    // The link is in the user's inbox; a form still on screen invites a second
    // send that the rate limiter will refuse.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-magic-link-sent")).toHaveTextContent(SENT_MESSAGE);
    expect(page.getByTestId("login-magic-link-submit").query()).toBeNull();
  });

  it("does not name the address in the confirmation", async () => {
    // The API answers 200 { succeeded: true } for an address with NO account,
    // precisely so the screen cannot be used to enumerate users. The confirmation
    // is the one artefact both outcomes share, so it stays a constant.
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-magic-link-sent")).not.toHaveTextContent(EMAIL);
  });

  it("disables the send button while the request is in flight", async () => {
    // A double send burns the rate limit.
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
    // A stale banner hanging over an in-flight retry is a lie.
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
    // Switching tabs resets the sent state, so the tab is usable again.
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
    // The operation is typed `Promise<unknown>`, so the screen narrows at its own
    // boundary: a body it cannot read is NOT a sent link.
    respondWithSend({});
    const user = userEvent.setup();
    await renderScreen();

    await openMagicLinkTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(page.getByTestId("login-magic-link-sent").query()).toBeNull();
  });

  it("does not accept a stringly-typed succeeded flag", async () => {
    // JS truthiness would happily accept the STRING "false"; only a strict
    // `=== true` rejects it.
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
    // render — but it is still a machine token.
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
    // Kept DISTINCT from the generic tail: collapsing them tells a user with no
    // network to retype an address that was fine.
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

describe("LoginScreen magic-link auto-verify", () => {
  it("opens on the magic-link tab when the link carries a token", async () => {
    // The user clicked a link in their inbox and must land where the outcome will
    // be reported.
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
    // `path`, and must arrive as a single properly-encoded value.
    expect(verifyCalls()[0].method).toBe("GET");
    expect(
      new URL(verifyCalls()[0].url, globalThis.location.origin).searchParams.get("token"),
    ).toBe(MAGIC_LINK_TOKEN);
  });

  it("opens on the password tab and verifies nothing when there is no token", async () => {
    // Guards against an implementation that is always in the magic-link tab.
    await renderScreen({ magicLinkToken: undefined });

    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "true");
    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("treats an empty token as no token", async () => {
    // `""` is not nullish, and verifying it would spend a request to be told the
    // format is invalid.
    await renderScreen({ magicLinkToken: "" });

    await expect
      .element(page.getByTestId("login-tab-password"))
      .toHaveAttribute("aria-selected", "true");
    expect(verifyCalls()).toHaveLength(0);
  });

  it("verifies exactly once even though the failure re-renders the screen", async () => {
    // A magic-link token is ONE-TIME USE, so a second verify can only ever fail.
    // The failure sets the shell's banner, which re-renders this panel with fresh
    // `onAuthResult`/`onError` identities, so effect deps alone cannot hold the
    // line — only a ref latch can.
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
    // THE LEGITIMATE PATH: token in, ticket out, exchange, back into the OIDC
    // flow. The origin is `""` — the API surface is a passthrough proxy mounting
    // `/v1/**` at the ROOT, and an absolute origin would send the browser
    // cross-origin and DROP the SameSite cookie the exchange endpoint just set.
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
    // there is nowhere to send the user.
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN, returnUrl: undefined });

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    // The ticket exchange is the screen's only writer of `location.href`, so an
    // EMPTY recording is the browser-true "no exchange happened".
    expect(handoffs).toHaveLength(0);
  });

  it("refuses an absolute returnUrl before exchanging the ticket", async () => {
    // The CLIENT picks this destination, so the guard belongs here — and it is
    // the SHELL's, reached by handing the raw body up. REFUSE, don't sanitize.
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN, returnUrl: EVIL_RETURN_URL });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    // The ticket exchange is the screen's only writer of `location.href`, so an
    // EMPTY recording is the browser-true "no exchange happened".
    expect(handoffs).toHaveLength(0);
  });

  it("hands an MFA-required verify response to the shell's branch table", async () => {
    // This panel does NOT re-derive the navigation: it reports the RAW body up and
    // the shell's one `authDispositionOf` decides. `verify` cannot return an MFA
    // branch today, so what is pinned is the WIRING — which is what stops three
    // panels from disagreeing about where a half-authenticated user lands.
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
    // `"Invalid token."` is the live spelling for a failed HMAC comparison, and it
    // means the same thing to the user as an expired link.
    rejectVerify(UNAUTHORIZED_STATUS, INVALID_TOKEN);
    await renderScreen({ magicLinkToken: MAGIC_LINK_TOKEN });

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(EXPIRED_MESSAGE);
  });

  it("does not promise a new link will help when the token is malformed", async () => {
    // THE TEST THAT BINDS THE CODE MAP. `"Invalid token format."` rides the SAME
    // 401 as the two tokens above but means something else. A blanket
    // `401 -> expired` rule passes every other failure test here and fails THIS
    // one, which is the whole reason it exists.
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

/**
 * A real memory router, not a bare render of `Route.options.component`: a
 * search-reading route component always dies on `router.stores` outside a
 * `RouterProvider`.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/login", route: loginRoute }],
  });
}

/** The emailed link, exactly as the notification handler builds it. */
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
    // THE WHOLE FEATURE, THROUGH THE REAL ROUTE: a user clicks the link in their
    // inbox and lands back in the OIDC flow they started. Nothing here is a
    // hostile input — if this fails, the tab is an outage.
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
    // `validateSearch` sees it, so `?magicLinkToken=123` arrives as the NUMBER 123.
    // Unlike `error`, this one is NOT re-stringified: it is a credential the server
    // compares, and a real token's base64 padding means it can never parse into a
    // scalar. A junk link must still render a usable form.
    await renderRouteAt("/login?magicLinkToken=123");

    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(verifyCalls()).toHaveLength(0);
  });
});
