import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { type SdkCall, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { Route as loginRoute } from "@app/routes/login";
import { LoginScreen, type LoginScreenProps } from "./LoginScreen";

/**
 * Component spec for the Login screen's OTP tab (Wallow-vec7.3.13 / 2.8c).
 *
 * This bead adds a PANEL and ONE `TabPanel` branch to the shell Wallow-vec7.3.11
 * established, exactly as `.3.12` did for magic-link. It does not re-derive the
 * branch table: on a verify response the panel calls `onAuthResult(body)` and
 * STOPS, and the shell's single `authDispositionOf` (`../auth-result`) owns the
 * MFA branches, the open-redirect guard and the ticket exchange. The tests below
 * that assert a ticket exchange or a refusal are therefore INTEGRATION tests
 * through the real shell — which is the point: they prove this panel hands its
 * result to the one branch table rather than growing a second one.
 *
 * Neither `LoginScreen.test.tsx` (.3.11) nor `MagicLinkLoginForm.test.tsx` (.3.12)
 * is edited by this bead, and `routes/login.tsx` needs no change: unlike
 * magic-link, the OTP tab has NO query parameter — both halves are driven entirely
 * by what the user types.
 *
 * ── THE WIRE, VERIFIED IN THE CONTROLLER (not in a client DTO) ───────────────
 *
 * `AccountController` (api/.../Identity/Wallow.Identity.Api/Controllers/AccountController.cs):
 *
 *   POST /v1/identity/auth/passwordless/otp                               :852
 *     200 { succeeded: true }                       ALWAYS on the happy path — and
 *                                                   also for an address with NO
 *                                                   account (PasswordlessService.cs:134-140
 *                                                   returns success to defeat email
 *                                                   enumeration).
 *     400 { succeeded: false, error: "Rate limit exceeded. Please try again later." }
 *                                                   the ONLY failure `SendOtpAsync`
 *                                                   can produce (:128-132).
 *
 *   POST /v1/identity/auth/passwordless/otp/verify                        :866
 *     200 { succeeded: true, email, signInTicket }                        :876
 *     401 { succeeded: false, error: "Code expired or not found." }       PasswordlessService.cs:166
 *     401 { succeeded: false, error: "Invalid code." }                    PasswordlessService.cs:174
 *
 * Those bodies are now sent VERBATIM over the harness transport (see TEST SEAM
 * below) rather than restated as hand-built rejection objects, so the SDK's own
 * error shaping is part of what these tests cover.
 *
 * As on magic-link, THE FAILURES ARE REJECTIONS, NOT 200 BODIES: both endpoints
 * answer non-2xx, so `unwrap()` THROWS and the oracle's `if (result.Succeeded) …
 * else` arms are reached through `onError`, not `onSuccess`. As of Wallow-vec7.7
 * `readCode` probes `extensions.code > code > error`, so the `error` member of the
 * bare `{ succeeded, error }` body arrives as `WallowError.code`.
 *
 * The oracle's `"invalid_code"` literal is DEAD: `ValidateOtpAsync` never returns
 * it. Its live spellings are `"Code expired or not found."` and `"Invalid code."`.
 * The dead literal is not ported and the oracle's COPY — which already reads
 * "Invalid or expired code", covering both — is kept.
 *
 * ── WHY THERE IS NO CODE MAP ON VERIFY (and .3.12 has one) ───────────────────
 *
 * bd memory `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-bind` asks
 * for a test a status-keyed map cannot pass. `.3.12` could write one honestly: its
 * 401 carries THREE tokens with TWO meanings. THIS endpoint cannot. Both of its 401
 * tokens mean the same thing — "that code did not work, try another" — so a
 * code-keyed and a status-keyed map are observationally IDENTICAL for every input
 * the API can produce, and a code map here would be unbindable fiction plus a
 * silent hazard: an unrecognised token on a 401 would fall to the generic tail and
 * tell a user with a mistyped code that "an error occurred". 401 identifies this
 * failure ALONE, which is exactly the condition bd memory
 * `wallow-auth-screens-key-error-copy-on-wallowerror-code-not-http-status` keeps a
 * status rule for. `unrecognisedTokenOnA401StillReadsAsABadCode` pins that.
 *
 * SEND is the other way round and DOES key on the token, following `.3.12`: its
 * copy is a rate-limit-specific divergence, so it must not be handed to some future
 * unrelated 400.
 *
 * ── TEST SEAM: the shared SDK harness (Wallow-pu6a.5.1) ──────────────────────
 *
 * `@bc-solutions-coder/testing/sdk-harness`. The SDK is the REAL one and only its
 * `fetch` is faked, so nothing here stubs the SDK barrel and there is no
 * app-level facade left to stub: the screen's whole pipeline — request-scoped SDK
 * -> generated operation -> CSRF interceptor -> request serialization -> error
 * shaping -> React Query — executes in the spec.
 * `renderWithWallow` supplies the router context the screen reads its SDK off,
 * and `createAuthHarness()` pins the harness origin to this app's root-mounted
 * API surface (Wallow-pu6a.5.5).
 *
 * Consequently the assertions below read the outgoing REQUEST (`callsTo(...)`,
 * `.body`, `.method`) instead of a spy on a stand-in double, and the
 * endpoint constants are the real generated URLs, not names. This screen talks to
 * FOUR endpoints (three tabs plus the external-provider list `ExternalProviders`
 * queries on mount), so every response is programmed per PATH — `defaultWire`
 * answers the rest — and every assertion filters by path rather than reading
 * `harness.calls` whole.
 *
 * The OIDC slice needs no stand-in either: `isSafeReturnUrl` and
 * `buildExchangeTicketUrl` are pure functions in `packages/sdk/src/auth-oidc.ts`,
 * so the REAL rule runs here. That is strictly stronger than the hand-restated
 * copy of the guard this file used to carry — a mirrored rule can drift from the
 * shipped one, and a mock returning a constant would let the guard tests pass for
 * the wrong reason.
 *
 * ── NAVIGATION SEAM (browser migration, Wallow-xzha.3.x) ─────────────────────
 *
 * These specs run in real Chromium, where `window.location` is `[Unforgeable]`:
 * the old jsdom `vi.stubGlobal("location", …)` hack cannot shadow it, and the
 * shell's `location.href = buildExchangeTicketUrl(origin, ticket, returnUrl)`
 * hand-off would navigate the runner's iframe and tear the run down.
 *
 * With the real builder in play there is no mock return value to make inert, so
 * the hand-off is observed the way the other wallow-auth screens observe theirs
 * (bd memory `full-navigation-seam-for-wallow-auth-screens-that`): listen for the
 * Navigation API `navigate` event the assignment fires, record `destination.url`,
 * and `preventDefault()` so the navigation is cancelled. The recorded array stands
 * in for the old settable `location.href` — a hand-off appends exactly one
 * absolute URL, a refusal appends nothing — and asserting the URL's ORIGIN,
 * pathname and query is a stronger claim than the old "the builder was called with
 * these three arguments": it pins the string the browser was actually handed.
 */

// Hoisted so the `vi.mock` factory and the test bodies share the same spy. Only
// `useNavigate` is mocked now — the SDK reaches the harness transport instead.
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

/**
 * A code shaped like the one the service really mints:
 * `RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6")`
 * (PasswordlessService.cs:141) — six digits, ZERO-PADDED, which is why it is a
 * string and never a number.
 */
const CODE = "042317";

/**
 * The generated operations' URLs (`packages/sdk/src/generated/sdk.gen.ts`), which
 * are what the recorded requests carry. Note the send path is a PREFIX of the
 * verify path, so every match here is on equality, never `startsWith`.
 */
const OTP_SEND_ENDPOINT = "/v1/identity/auth/passwordless/otp";
const OTP_VERIFY_ENDPOINT = "/v1/identity/auth/passwordless/otp/verify";
const LOGIN_ENDPOINT = "/v1/identity/auth/login";
const MAGIC_LINK_ENDPOINT = "/v1/identity/auth/passwordless/magic-link";

/** The path the shell's ticket hand-off navigates to, built by the real builder. */
const EXCHANGE_TICKET_PATH = "/v1/identity/auth/exchange-ticket";

/** Wire statuses, named so the failure tests read as the API's contract. */
const OK_STATUS = 200;
const BAD_REQUEST_STATUS = 400;
const UNAUTHORIZED_STATUS = 401;
const SERVER_ERROR_STATUS = 500;

/** `Array.prototype.at` index of the most recent element. */
const LAST_INDEX = -1;

/**
 * The returnUrl `/connect/authorize` really sends (AuthorizationController.cs:53,
 * :67): relative, and already past `Url.IsLocalUrl`. This is the REAL-TRAFFIC
 * pole — if the guard refuses this, every OTP sign-in is dead.
 */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/** An absolute returnUrl from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

/** The bail target for an unsafe returnUrl, matching the ConsentScreen port. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** This endpoint's machine tokens — matched against, NEVER rendered. */
const RATE_LIMITED_TOKEN = "Rate limit exceeded. Please try again later.";
const CODE_EXPIRED_TOKEN = "Code expired or not found.";
const INVALID_CODE_TOKEN = "Invalid code.";

/** The oracle's blank-input guards (Login.razor:436, :471). */
const BLANK_EMAIL_MESSAGE = "Please enter your email.";
const BLANK_CODE_MESSAGE = "Please enter the verification code.";

/** The oracle's `HandleVerifyOtp` switch copy (Login.razor:484). */
const INVALID_CODE_MESSAGE = "Invalid or expired code. Please try again.";

/**
 * DIVERGENCE, disclosed on the bead: the oracle shows its generic copy for every
 * send failure, but the ONLY send failure the service can produce is the rate
 * limit — and "An error occurred. Please try again." tells a rate-limited user to
 * do the one thing that cannot work. This is the same call `.3.11` made on the 423
 * lockout fallback and `.3.12` made on the magic-link send, for the same reason.
 */
const RATE_LIMITED_MESSAGE = "Too many code requests. Please wait a few minutes and try again.";

/** Shared with the other tabs (`../auth-result`), not re-invented here. */
const GENERIC_MESSAGE = "An error occurred. Please try again.";
const UNREACHABLE_MESSAGE = "Unable to reach the server. Please try again later.";

let harness: SdkHarness;

/**
 * The wire this screen sees unless a test says otherwise. Programmed per PATH
 * because the shell drives four endpoints at once: whichever tab is open, the
 * shared `ExternalProviders` child issues its own query on mount, so a single
 * blanket response would answer the wrong question somewhere.
 */
function defaultWire(call: SdkCall): Response {
  switch (call.path) {
    case OTP_VERIFY_ENDPOINT:
    case LOGIN_ENDPOINT: {
      return Response.json(
        { succeeded: true, email: EMAIL, signInTicket: TICKET },
        { status: OK_STATUS },
      );
    }
    case OTP_SEND_ENDPOINT:
    case MAGIC_LINK_ENDPOINT: {
      return Response.json({ succeeded: true }, { status: OK_STATUS });
    }
    default: {
      // `ExternalProviders`: an empty list renders nothing, which is this
      // screen's state in every test in this file.
      return Response.json([], { status: OK_STATUS });
    }
  }
}

/** Answer ONE endpoint differently, leaving every other one on {@link defaultWire}. */
function wireEndpoint(
  path: string,
  respond: (call: SdkCall) => Response | Promise<Response>,
): void {
  harness.respond((call: SdkCall) => (call.path === path ? respond(call) : defaultWire(call)));
}

/** A non-2xx from `path`, in this API's bare `{ succeeded, error }` shape. */
function rejectAt(path: string, status: number, token: string): void {
  wireEndpoint(path, () => Response.json({ succeeded: false, error: token }, { status }));
}

/** A 200 from `path` carrying exactly `body`. */
function resolveAt(path: string, body: unknown): void {
  wireEndpoint(path, () => Response.json(body, { status: OK_STATUS }));
}

/**
 * A TRANSPORT failure at `path` — the request never reaches a server, so the
 * rejection carries neither `status` nor `code`. That absence is what identifies
 * the oracle's `catch (HttpRequestException)` arm.
 */
function failNetworkAt(path: string): void {
  wireEndpoint(path, () => {
    throw new TypeError("Failed to fetch");
  });
}

/**
 * Hold `path` in flight. The returned function settles the request with `body`,
 * which is how the in-flight (double-submit) assertions get to observe the
 * pending state without the responder never settling.
 */
function holdAt(path: string): (body: unknown) => void {
  let settle: (body: unknown) => void = () => {};

  wireEndpoint(
    path,
    async () =>
      await new Promise<Response>((resolve) => {
        settle = (body: unknown) => {
          resolve(Response.json(body, { status: OK_STATUS }));
        };
      }),
  );

  return (body: unknown) => {
    settle(body);
  };
}

/** Every recorded request to one endpoint, in order. */
function callsTo(path: string): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === path);
}

/** The most recent recorded request to one endpoint. */
function lastCallTo(path: string): SdkCall | undefined {
  return callsTo(path).at(LAST_INDEX);
}

/**
 * NAVIGATION SEAM — see the module header. The `navigate` event the shell's
 * `location.href = …` assignment fires is recorded and CANCELLED, so the
 * hand-off URL is observable without navigating the Chromium runner away.
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

/** Every full-navigation hand-off this test's render attempted, in order. */
let handoffs: string[] = [];

function recordHandoff(event: NavigateEvent): void {
  handoffs.push(event.destination.url);
  // Cancel it: assigning `location.href` would otherwise tear the runner down.
  event.preventDefault();
}

/**
 * The ticket-exchange URL the shell handed the browser, recovered from the
 * cancelled navigation. Waits for the hand-off first, so callers assert on the
 * parts rather than on the timing.
 */
async function handoffTarget(): Promise<URL> {
  await vi.waitFor(() => {
    expect(handoffs).toHaveLength(1);
  });

  return new URL(handoffs[0] ?? "");
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/**
 * Render the screen as the OIDC hand-off would: a safe, relative returnUrl.
 *
 * `"returnUrl" in props` rather than `props.returnUrl ?? DEFAULT`: the
 * absent-`returnUrl` branch is itself under test, and a `??` helper would silently
 * substitute the default for an explicit `{ returnUrl: undefined }`, making that
 * test exercise the PRESENT path while still failing red for a right-looking
 * reason (bd memory `red-phase-render-helpers-must-distinguish-explicit-undefined`).
 */
function renderScreen(props: Partial<LoginScreenProps> = {}) {
  const returnUrl: string | undefined = "returnUrl" in props ? props.returnUrl : RETURN_URL;

  return renderWithClient(<LoginScreen {...props} returnUrl={returnUrl} />);
}

/** Open the OTP tab — the oracle's `SwitchTab(LoginTab.Otp)`. */
async function openOtpTab(user: ReturnType<typeof userEvent.setup>) {
  await user.click(page.getByTestId("login-tab-otp"));
}

/** Fill in the OTP email field and submit it — the oracle's `HandleSendOtp`. */
async function submitEmail(user: ReturnType<typeof userEvent.setup>, email: string = EMAIL) {
  if (email !== "") {
    await user.type(page.getByTestId("login-otp-email"), email);
  }
  await user.click(page.getByTestId("login-otp-send-submit"));
}

/** Fill in the code field and submit it — the oracle's `HandleVerifyOtp`. */
async function submitCode(user: ReturnType<typeof userEvent.setup>, code: string = CODE) {
  if (code !== "") {
    await user.type(page.getByTestId("login-otp-code"), code);
  }
  await user.click(page.getByTestId("login-otp-verify-submit"));
}

/** Get to the code form the way a real user does: open the tab and send a code. */
async function reachCodeForm(user: ReturnType<typeof userEvent.setup>) {
  await openOtpTab(user);
  await submitEmail(user);
  await expect.element(page.getByTestId("login-otp-code")).toBeInTheDocument();
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
  harness = createAuthHarness();
  harness.respond(defaultWire);
  handoffs = [];
  navigationApi.addEventListener("navigate", recordHandoff);
});

afterEach(() => {
  navigationApi.removeEventListener("navigate", recordHandoff);
});

// ─────────────────────────────────────────────────────────────────────────────
// SENDING THE CODE — the oracle's `HandleSendOtp`.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen OTP tab: sending", () => {
  it("shows the email field and send button in place of the password panel", async () => {
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);

    await expect.element(page.getByTestId("login-otp-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-otp-send-submit")).toBeInTheDocument();
    // The oracle's tabs are an `else if` chain: one panel at a time.
    expect(page.getByTestId("login-password").query()).toBeNull();
    expect(page.getByTestId("login-magic-link-email").query()).toBeNull();
  });

  it("starts on the email form, not the code form", async () => {
    // The oracle's `_otpSent` starts false: there is no code to type until one has
    // been sent, and a code box on arrival invites a user to hunt for a mail that
    // was never sent.
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);

    await expect.element(page.getByTestId("login-otp-email")).toBeInTheDocument();
    expect(page.getByTestId("login-otp-code").query()).toBeNull();
    expect(page.getByTestId("login-otp-sent").query()).toBeNull();
  });

  it("does not send anything merely because the tab was opened", async () => {
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);

    await expect.element(page.getByTestId("login-otp-send-submit")).toBeInTheDocument();
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(0);
  });

  it("refuses a blank email without calling the API", async () => {
    // The oracle's `IsNullOrWhiteSpace(_email)` guard: a blank send cannot succeed
    // and would spend one of the address's rate-limit allowance.
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user, "");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(0);
  });

  it("refuses a whitespace-only email without calling the API", async () => {
    // WHITEspace, not just empty — `IsNullOrWhiteSpace`, so "   " is blank.
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user, "   ");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(0);
  });

  it("sends exactly the typed email, and no returnUrl or client_id", async () => {
    // `SendOtpRequest` is `{ email }` ALONE (types.gen.ts:834) — unlike
    // `SendMagicLinkRequest`, it carries no OIDC cargo, because nothing is emailed
    // that needs to resume the flow: the user comes back to THIS live form. Read
    // off the REQUEST, so "and no cargo" is a claim about the wire, not about a
    // SDK argument.
    const user = userEvent.setup();
    renderScreen({ clientId: CLIENT_ID });

    await openOtpTab(user);
    await submitEmail(user);

    await vi.waitFor(() => {
      expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);
    });
    expect(lastCallTo(OTP_SEND_ENDPOINT)?.method).toBe("POST");
    expect(lastCallTo(OTP_SEND_ENDPOINT)?.body).toEqual({ email: EMAIL });
  });

  it("swaps the email form for the code form once the code is sent", async () => {
    // The oracle's `_otpSent = true`, which flips :141 from the email form to the
    // code form.
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-otp-sent")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-otp-code")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-otp-verify-submit")).toBeInTheDocument();
    expect(page.getByTestId("login-otp-email").query()).toBeNull();
  });

  it("shows the code form for an address with no account, revealing nothing", async () => {
    // ── ANTI-ENUMERATION. ──
    // `SendOtpAsync` returns `200 { succeeded: true }` for an unknown address
    // (PasswordlessService.cs:134-140) SPECIFICALLY so this screen cannot be used to
    // discover who has an account. The response is byte-identical to the happy path,
    // so the screen must be too (bd memory
    // `anti-enumeration-pattern-for-endpoints-that-must-not`).
    const user = userEvent.setup();
    resolveAt(OTP_SEND_ENDPOINT, { succeeded: true });
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user, "nobody@example.com");

    await expect.element(page.getByTestId("login-otp-sent")).toBeInTheDocument();
    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("sends one code for a double-clicked send button", async () => {
    // The oracle's `Disabled="_isSubmitting"`. One click, one code: a second send
    // OVERWRITES the Redis key (PasswordlessService.cs:144), silently invalidating
    // the code already sitting in the user's inbox — so the impatient user is the one
    // who gets locked out. Bound by the OUTCOME (sends), not by the attribute.
    const user = userEvent.setup();
    const releaseSend = holdAt(OTP_SEND_ENDPOINT);
    renderScreen();

    await openOtpTab(user);
    await user.type(page.getByTestId("login-otp-email"), EMAIL);

    // `force` on the second click: after the first submit the button is disabled
    // in-flight, and Playwright would otherwise wait for it to re-enable. Skipping
    // the actionability wait reproduces the impatient double-click; the OUTCOME
    // (one send) is what this test binds.
    await user.click(page.getByTestId("login-otp-send-submit"));
    await user.click(page.getByTestId("login-otp-send-submit"), { force: true });

    // Let the request REACH the transport before settling it: the button goes
    // disabled a tick before `fetch` is called, and releasing into that gap would
    // leave the held responder installed forever.
    await vi.waitFor(() => {
      expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);
    });

    releaseSend({ succeeded: true });
    await expect.element(page.getByTestId("login-otp-sent")).toBeInTheDocument();
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);
  });

  it("clears a stale error banner when the send is retried", async () => {
    // The oracle's `_errorMessage = null` at the top of `HandleSendOtp`: a banner
    // hanging over an in-flight retry is a lie about the current attempt.
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user, "");
    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);

    await submitEmail(user);

    await vi.waitFor(() => {
      expect(page.getByTestId("login-error").query()).toBeNull();
    });
  });
});

describe("LoginScreen OTP tab: send failures", () => {
  it("tells a rate-limited user to wait, not to try again", async () => {
    // DIVERGENCE (see RATE_LIMITED_MESSAGE above): the oracle's generic "try again"
    // is the one instruction guaranteed not to work here.
    const user = userEvent.setup();
    rejectAt(OTP_SEND_ENDPOINT, BAD_REQUEST_STATUS, RATE_LIMITED_TOKEN);
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(RATE_LIMITED_MESSAGE);
  });

  it("keeps the email form up after a send failure so the address can be fixed", async () => {
    const user = userEvent.setup();
    rejectAt(OTP_SEND_ENDPOINT, BAD_REQUEST_STATUS, RATE_LIMITED_TOKEN);
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-otp-email")).toBeInTheDocument();
    expect(page.getByTestId("login-otp-code").query()).toBeNull();
  });

  it("distinguishes a dead network from a server that said no", async () => {
    // The oracle's `catch (HttpRequestException)` arm, kept DISTINCT from its
    // generic tail: telling a user with no network that "an error occurred" sends
    // them to re-read an email that arrived fine.
    const user = userEvent.setup();
    failNetworkAt(OTP_SEND_ENDPOINT);
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(UNREACHABLE_MESSAGE);
  });

  it("falls back to the generic tail for a failure it has never heard of", async () => {
    const user = userEvent.setup();
    rejectAt(OTP_SEND_ENDPOINT, SERVER_ERROR_STATUS, "something_new");
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("never renders the raw server sentence on a send failure", async () => {
    // The oracle's `_ => result.Error` leak is NOT ported. A server-authored English
    // sentence is still a machine token: matched against, never shown.
    const user = userEvent.setup();
    rejectAt(OTP_SEND_ENDPOINT, BAD_REQUEST_STATUS, RATE_LIMITED_TOKEN);
    const { container } = await renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    expect(container.textContent).not.toContain(RATE_LIMITED_TOKEN);
  });

  it("fails closed on a 200 body it cannot read, rather than promising a code", async () => {
    // Sending the user to watch an inbox that will stay empty is worse than an error.
    const user = userEvent.setup();
    resolveAt(OTP_SEND_ENDPOINT, { unexpected: "shape" });
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(page.getByTestId("login-otp-code").query()).toBeNull();
  });

  it('does not accept the STRING "false" as success', async () => {
    // C#'s `if (result.Succeeded)` is a strict bool test; JS truthiness would
    // happily accept `"false"` and march the user to a code form for a code that
    // was never sent.
    const user = userEvent.setup();
    resolveAt(OTP_SEND_ENDPOINT, { succeeded: "false" });
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(page.getByTestId("login-otp-code").query()).toBeNull();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// VERIFYING THE CODE — the oracle's `HandleVerifyOtp`.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen OTP tab: verifying", () => {
  it("refuses a blank code without calling the API", async () => {
    // The oracle's `IsNullOrWhiteSpace(_otpCode)` guard (:471).
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await submitCode(user, "");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_CODE_MESSAGE);
    expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(0);
  });

  it("refuses a whitespace-only code without calling the API", async () => {
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await submitCode(user, "   ");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_CODE_MESSAGE);
    expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(0);
  });

  it("verifies the code against the address the code was sent to", async () => {
    // The oracle's `VerifyOtpAsync(_email, _otpCode, …)`. The email is NOT re-typed
    // on the code form — the code is bound to the address the send used
    // (`ValidateOtpAsync` keys Redis on it, PasswordlessService.cs:161), so
    // re-reading a field the user can no longer see would be the one way to get it
    // wrong.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
    });
    // `rememberMe: false` joins the payload as of Wallow-98st, which gives this tab
    // its OWN checkbox. It is sent EXPLICITLY rather than omitted: the endpoint's
    // `rememberMe` is optional (types.gen.ts:1044) and defaults false
    // (AccountController.cs:895), so omission and `false` are the same session — but
    // only one of them says on the wire which session the user asked for. Read off
    // the recorded BODY, which is where that distinction actually lives.
    expect(lastCallTo(OTP_VERIFY_ENDPOINT)?.body).toEqual({
      email: EMAIL,
      code: CODE,
      rememberMe: false,
    });
  });

  it("does not re-send a code when the code form is submitted", async () => {
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);

    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
    });
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);
  });

  it("redeems a ONE-TIME code exactly once even when the button is double-clicked", async () => {
    // ── THE ONE-TIME-USE HAZARD. ──
    // `ValidateOtpAsync` DELETES the Redis key on success (PasswordlessService.cs:178),
    // so a second submit redeems a SPENT code and paints "Invalid or expired code"
    // over a sign-in that just succeeded. This is the same hazard `.3.12` hit on the
    // magic-link token; the vector differs (there a re-fired effect, here an impatient
    // user), so the defence differs too — which is exactly why this binds the OUTCOME
    // (how many redemptions) and not the mechanism (bd memory
    // `exactly-once-server-mutations-in-react-need-a-ref-not-just-deps`). A test that
    // only asserted `toBeDisabled()` would pass for any implementation that merely
    // greys the button out.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    const releaseVerify = holdAt(OTP_VERIFY_ENDPOINT);
    await user.type(page.getByTestId("login-otp-code"), CODE);

    // `force` on the second click: the button is disabled in-flight after the first
    // submit, so Playwright would wait for it to re-enable. This reproduces the
    // impatient double-click and binds the OUTCOME (one redemption), not the disable.
    await user.click(page.getByTestId("login-otp-verify-submit"));
    await user.click(page.getByTestId("login-otp-verify-submit"), { force: true });

    await vi.waitFor(() => {
      expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
    });

    releaseVerify({ succeeded: true, email: EMAIL, signInTicket: TICKET });
    await handoffTarget();
    // ONE redemption, for two clicks.
    expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
  });

  it("clears a stale error banner when the code is resubmitted", async () => {
    // The oracle's `_errorMessage = null` at the top of `HandleVerifyOtp`.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await submitCode(user, "");
    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_CODE_MESSAGE);

    await submitCode(user);

    await vi.waitFor(() => {
      expect(page.getByTestId("login-error").query()).toBeNull();
    });
  });
});

describe("LoginScreen OTP tab: verify success hands off to the shell", () => {
  it("exchanges the ticket for the returnUrl the OIDC flow supplied", async () => {
    // ── THE LEGITIMATE PATH. ──
    // `RETURN_URL` is the relative value `/connect/authorize` really mints. Nothing
    // here is hostile: if this fails, the tab is an OUTAGE.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await submitCode(user);

    const target: URL = await handoffTarget();

    // SAME-ORIGIN. The shell passes origin "" to the real `buildExchangeTicketUrl`,
    // so the URL the browser is handed resolves against THIS document's origin: the
    // passthrough proxy mounts `/v1/**` at the root, and the oracle's `ApiBaseUrl`
    // prepend would send the browser cross-origin and DROP the SameSite cookie the
    // exchange just set (bd memory `wallow-auth-screens-must-pass-origin-same-origin`).
    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_TICKET_PATH);
    expect(target.searchParams.get("ticket")).toBe(TICKET);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
  });

  it("reports being signed in when there is no returnUrl to go back to", async () => {
    // The oracle's trailing `else`: nowhere to send the user, so say so rather than
    // invent a destination.
    const user = userEvent.setup();
    renderScreen({ returnUrl: undefined });

    await reachCodeForm(user);
    await submitCode(user);

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(handoffs).toEqual([]);
  });

  it("refuses an unsafe returnUrl instead of exchanging the ticket to it", async () => {
    // The CLIENT picks the destination on the ticket path, so the guard applies —
    // and it is the SHELL's existing one, reached by handing the RAW body up. This
    // panel never navigates. REFUSE, don't sanitize (bd memory
    // `returnurl-guard-refuse-dont-sanitize`).
    const user = userEvent.setup();
    renderScreen({ returnUrl: EVIL_RETURN_URL });

    await reachCodeForm(user);
    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(handoffs).toEqual([]);
  });

  it("defers an mfaRequired response to the shell's one branch table", async () => {
    // `otp/verify` cannot itself answer `mfaRequired` today (AccountController.cs:866-877
    // mints a ticket unconditionally). This test does NOT claim it can — it pins that
    // the panel hands the RAW body up rather than narrowing it here. A panel that
    // grew its own `succeeded` check would swallow this body; the shell must see it.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    resolveAt(OTP_VERIFY_ENDPOINT, { succeeded: false, mfaRequired: true });
    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}`,
      });
    });
  });
});

describe("LoginScreen OTP tab: verify failures", () => {
  it("maps a mistyped code onto the oracle's invalid-or-expired copy", async () => {
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    rejectAt(OTP_VERIFY_ENDPOINT, UNAUTHORIZED_STATUS, INVALID_CODE_TOKEN);
    await submitCode(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(INVALID_CODE_MESSAGE);
  });

  it("maps an expired code onto the same copy", async () => {
    // The oracle's copy already reads "Invalid OR EXPIRED code", covering both live
    // tokens — which is exactly why no code map earns its place here.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    rejectAt(OTP_VERIFY_ENDPOINT, UNAUTHORIZED_STATUS, CODE_EXPIRED_TOKEN);
    await submitCode(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(INVALID_CODE_MESSAGE);
  });

  it("reads an unrecognised token on a 401 as a bad code, not a generic error", async () => {
    // 401 identifies this failure ALONE (both live tokens mean the same thing), so
    // the status rule is what carries the meaning. A future token this screen has
    // never heard of still means the code did not work, and dropping the user to
    // "an error occurred" would hide the retry they actually need.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    rejectAt(OTP_VERIFY_ENDPOINT, UNAUTHORIZED_STATUS, "some_future_token");
    await submitCode(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(INVALID_CODE_MESSAGE);
  });

  it("keeps the code form up after a bad code so it can be retyped", async () => {
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    rejectAt(OTP_VERIFY_ENDPOINT, UNAUTHORIZED_STATUS, INVALID_CODE_TOKEN);
    await submitCode(user);

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-otp-code")).toBeInTheDocument();
  });

  it("distinguishes a dead network from a rejected code", async () => {
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    failNetworkAt(OTP_VERIFY_ENDPOINT);
    await submitCode(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(UNREACHABLE_MESSAGE);
  });

  it("falls back to the generic tail for a non-401 failure", async () => {
    // The oracle's `_ =>` tail. A 500 is not a bad code and must not be reported as
    // one — that would have the user retyping a perfectly good code at a dead server.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    rejectAt(OTP_VERIFY_ENDPOINT, SERVER_ERROR_STATUS, "server_exploded");
    await submitCode(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("never renders the raw server sentence, nor the code itself", async () => {
    const user = userEvent.setup();
    const { container } = await renderScreen();

    await reachCodeForm(user);
    rejectAt(OTP_VERIFY_ENDPOINT, UNAUTHORIZED_STATUS, INVALID_CODE_TOKEN);
    await submitCode(user);

    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();
    expect(container.textContent).not.toContain(INVALID_CODE_TOKEN);
    expect(container.textContent).not.toContain(CODE_EXPIRED_TOKEN);
    // The banner must not echo the credential back as prose.
    expect(page.getByTestId("login-error").element().textContent).not.toContain(CODE);
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// THE SHARED SHELL — the oracle's `SwitchTab`.
// ─────────────────────────────────────────────────────────────────────────────

describe("LoginScreen OTP tab: tab switching", () => {
  it("clears another tab's error banner when the OTP tab is opened", async () => {
    // The oracle's `SwitchTab` resets `_errorMessage`: one banner is shared by all
    // three tabs, so a magic-link failure must not follow the user into the OTP tab
    // and blame it for something it did not do.
    const user = userEvent.setup();
    rejectAt(MAGIC_LINK_ENDPOINT, BAD_REQUEST_STATUS, RATE_LIMITED_TOKEN);
    renderScreen();

    await user.click(page.getByTestId("login-tab-magic-link"));
    await user.type(page.getByTestId("login-magic-link-email"), EMAIL);
    await user.click(page.getByTestId("login-magic-link-submit"));
    await expect.element(page.getByTestId("login-error")).toBeInTheDocument();

    await openOtpTab(user);

    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("returns to the email form when the tab is left and re-entered", async () => {
    // The oracle's `SwitchTab` resets `_otpSent` and `_otpCode`. Here that is free:
    // both are panel-local state and switching tabs unmounts the panel, so the shell
    // needs no reset it would otherwise have to grow.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);

    await user.click(page.getByTestId("login-tab-password"));
    await openOtpTab(user);

    await expect.element(page.getByTestId("login-otp-email")).toBeInTheDocument();
    expect(page.getByTestId("login-otp-code").query()).toBeNull();
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// REMEMBER ME — Wallow-98st, a DELIBERATE divergence from the oracle.
//
// `.3.13` shipped this tab with NO `rememberMe` and disclosed why: the oracle
// passes `_rememberMe` to `VerifyOtpAsync`, but its checkbox is rendered ONLY
// inside the password tab (Login.razor:87-92) and `SwitchTab` never resets it. So
// on the oracle's OTP tab the flag is whatever a detour through the password tab
// happened to leave behind — an INVISIBLE control setting a session lifetime. The
// only user `.3.13` diverged from was one obeying that invisible control, and it
// diverged fail-SAFE (a shorter session).
//
// This bead does not "restore" the oracle's behaviour — that behaviour is the bug.
// It gives the tab its own VISIBLE, PANEL-LOCAL checkbox, so the flag on the wire
// is one the user could see and set. Two claims carry that, and both are pinned
// below as behaviour rather than as wiring:
//
//   1. The OTP box drives the OTP request  (it is real, not decorative).
//   2. NOTHING ELSE drives the OTP request (the password tab's box does not leak
//      in, and the OTP box does not leak out). This is the oracle's actual defect,
//      so it gets a test in BOTH directions.
//
// PLACEMENT: the checkbox lives on the CODE form, not the email form. `rememberMe`
// is consumed by `otp/verify` alone — `SendOtpRequest` is `{ email }` (types.gen.ts
// :834) and has nowhere to put it. A box on the email form would vanish at the very
// moment it took effect, which is how you get a control users cannot trust.
//
// TESTID: `login-otp-remember-me`, this tab's `login-otp-*` prefix — NOT the
// password tab's `login-remember-me`. Two controls with two independent states must
// not share one name, or the leak tests below could not tell them apart.
// ─────────────────────────────────────────────────────────────────────────────

/** The OTP tab's own remember-me box — panel-local, and NOT `login-remember-me`. */
function otpRememberMe() {
  return page.getByTestId("login-otp-remember-me");
}

describe("LoginScreen OTP tab: remember me", () => {
  it("offers an unchecked remember-me box on the code form", async () => {
    // Unchecked by DEFAULT: a long-lived session is a choice the user makes, never
    // one a screen makes on their behalf. This is also the value `.3.13` shipped, so
    // the default is not a behaviour change — only the visibility is.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);

    await expect.element(otpRememberMe()).toBeInTheDocument();
    await expect.element(otpRememberMe()).not.toBeChecked();
  });

  it("does not offer remember-me before a code has been sent", async () => {
    // The email form's request has nowhere to put the flag, so a box there would be
    // a control that does nothing where it stands.
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);

    await expect.element(page.getByTestId("login-otp-email")).toBeInTheDocument();
    expect(page.getByTestId("login-otp-remember-me").query()).toBeNull();
  });

  it("does not answer to the password tab's testid", async () => {
    // Two independent states may not share one name. If this fails, every leak test
    // below is querying whichever box the DOM happened to hand back first.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);

    expect(page.getByTestId("login-remember-me").query()).toBeNull();
  });

  it("toggles when its label is clicked", async () => {
    // The `htmlFor`/`id` pairing, asserted through the behaviour it buys rather than
    // by reading the attributes: a label wired to nothing still renders perfectly.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await user.click(page.getByText("Remember me"));

    await expect.element(otpRememberMe()).toBeChecked();
  });

  it("publishes its checked state as aria-checked", async () => {
    // Wallow-m5aq.5.2 — the catalog sweep. A raw `<input type="checkbox">` keeps
    // its state in the `checked` PROPERTY, which no attribute reflects; the
    // catalog's Checkbox publishes it as `aria-checked`, the same way this
    // screen's tabs publish `aria-selected`.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);

    await expect.element(otpRememberMe()).toHaveAttribute("aria-checked", "false");

    await toggleCheckbox(user, "login-otp-remember-me");

    await expect.element(otpRememberMe()).toHaveAttribute("aria-checked", "true");
    await expect.element(otpRememberMe()).toBeChecked();
  });

  it("is reachable as a checkbox named by its label", async () => {
    // The `htmlFor`/`id` pairing asserted through what it buys: a box whose name
    // is its label. A migration that drops the pairing leaves an unnamed control.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);

    await expect.element(page.getByRole("checkbox", { name: "Remember me" })).toBeInTheDocument();
  });

  it("sends rememberMe true when the user checks the box", async () => {
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await toggleCheckbox(user, "login-otp-remember-me");
    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
    });
    expect(lastCallTo(OTP_VERIFY_ENDPOINT)?.body).toEqual({
      email: EMAIL,
      code: CODE,
      rememberMe: true,
    });
  });

  it("sends rememberMe false when the box is checked and then unchecked", async () => {
    // The box must track the user's LAST answer, not merely record that they once
    // touched it. A one-way latch would pass the test above and still be broken.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await toggleCheckbox(user, "login-otp-remember-me");
    await toggleCheckbox(user, "login-otp-remember-me");

    await expect.element(otpRememberMe()).not.toBeChecked();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
    });
    expect(lastCallTo(OTP_VERIFY_ENDPOINT)?.body).toEqual({
      email: EMAIL,
      code: CODE,
      rememberMe: false,
    });
  });

  it("does not verify or re-send merely because the box was toggled", async () => {
    // The box is bound to state, not wired to the form's submit. A checkbox that
    // spends the user's one-time code on a click is worse than no checkbox.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);

    await toggleCheckbox(user, "login-otp-remember-me");

    expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(0);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);
  });

  // ── THE ORACLE'S DEFECT, PINNED IN BOTH DIRECTIONS ─────────────────────────

  it("ignores the password tab's remember-me box", async () => {
    // THE BUG THIS BEAD EXISTS FOR. In the oracle, ticking the password tab's box
    // and then wandering to the OTP tab silently buys a persistent session from a
    // control that is no longer on screen. Here the panels' states are disjoint, so
    // the OTP request answers to the OTP box alone.
    const user = userEvent.setup();
    renderScreen();

    // The password tab is the landing tab, and its box is the invisible one.
    await toggleCheckbox(user, "login-remember-me");
    await expect.element(page.getByTestId("login-remember-me")).toBeChecked();

    await reachCodeForm(user);
    await expect.element(otpRememberMe()).not.toBeChecked();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
    });
    expect(lastCallTo(OTP_VERIFY_ENDPOINT)?.body).toEqual({
      email: EMAIL,
      code: CODE,
      rememberMe: false,
    });
  });

  it("does not leak its own box into the password tab", async () => {
    // The same defect in reverse. Shared state would fail this even if the panels
    // were merely reading one variable in the shell — which is precisely the shape
    // this bead is refusing.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await toggleCheckbox(user, "login-otp-remember-me");

    await user.click(page.getByTestId("login-tab-password"));
    await user.type(page.getByTestId("login-email"), EMAIL);
    await user.type(page.getByTestId("login-password"), "correct-horse");

    await expect.element(page.getByTestId("login-remember-me")).not.toBeChecked();

    await user.click(page.getByTestId("login-submit"));

    await vi.waitFor(() => {
      expect(callsTo(LOGIN_ENDPOINT)).toHaveLength(1);
    });
    expect(lastCallTo(LOGIN_ENDPOINT)?.body).toEqual({
      email: EMAIL,
      password: "correct-horse",
      rememberMe: false,
    });
  });

  it("resets to unchecked when the tab is left and re-entered", async () => {
    // PANEL-LOCAL is the whole claim. Switching tabs unmounts the panel, so the box
    // resets for free — the same way `_otpSent` and the code field already do. A user
    // returning to a fresh-looking form must not be carrying a stale hidden answer:
    // that is the oracle's defect wearing this bead's clothes.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await toggleCheckbox(user, "login-otp-remember-me");
    await expect.element(otpRememberMe()).toBeChecked();

    await user.click(page.getByTestId("login-tab-password"));
    await reachCodeForm(user);

    await expect.element(otpRememberMe()).not.toBeChecked();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
    });
    expect(lastCallTo(OTP_VERIFY_ENDPOINT)?.body).toEqual({
      email: EMAIL,
      code: CODE,
      rememberMe: false,
    });
  });
});

// ─────────────────────────────────────────────────────────────────────────────
// THROUGH THE REAL ROUTE.
// ─────────────────────────────────────────────────────────────────────────────

/**
 * `loginRoute` cannot be rendered bare: its component calls `Route.useSearch()`,
 * which throws outside a router (bd memory
 * `tanstack-route-component-cannot-be-render-ed-bare-if-it-reads-search`). So the
 * real route object is grafted onto a memory router at the URL under test.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/login", route: loginRoute }],
  });
}

describe("/login route: OTP", () => {
  it("signs a user in end to end: email in, code in, ticket exchanged", async () => {
    // ── THE WHOLE FEATURE, THROUGH THE REAL ROUTE. ──
    // A user picks the OTP tab, gets a code, types it, and lands back in the OIDC
    // flow they started. Nothing here is a hostile input: if this fails, the tab is
    // an outage — and a suite that only tested guards would call that a pass.
    const user = userEvent.setup();
    renderRouteAt(`/login?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=${CLIENT_ID}`);

    await reachCodeForm(user);
    await submitCode(user);

    const target: URL = await handoffTarget();

    // Same-origin, with the OIDC returnUrl carried all the way through the route.
    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_TICKET_PATH);
    expect(target.searchParams.get("ticket")).toBe(TICKET);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
  });

  it("sends nothing on load for a bare /login", async () => {
    renderRouteAt("/login");

    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(0);
    expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(0);
  });
});
