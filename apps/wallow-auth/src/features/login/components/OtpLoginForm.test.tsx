import {
  expectNavigationEscape,
  navigationEscapes,
} from "@bc-solutions-coder/testing/navigation-escape";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as loginRoute } from "@app/routes/login";
import { LoginScreen, type LoginScreenProps } from "./LoginScreen";

/**
 * The login screen's OTP tab: sending a code, and verifying it.
 *
 * Runs the real SDK over a faked fetch (sdk-harness). The shell drives four
 * endpoints at once, so responses are programmed per PATH and every assertion
 * filters `harness.calls` by path.
 *
 * Verify keys its copy on the 401 STATUS, not on the token: both live tokens mean
 * "that code did not work", so a code map would drop an unknown token to a generic
 * error and hide the retry the user needs.
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
 * A code shaped like the one the service really mints: six digits, ZERO-PADDED,
 * which is why it is a string and never a number.
 */
const CODE = "042317";

/**
 * The generated operations' URLs, which are what the recorded requests carry. The
 * send path is a PREFIX of the verify path, so every match here is on equality,
 * never `startsWith`.
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
 * The returnUrl `/connect/authorize` really sends: relative, and already past the
 * server's own local-url check. The REAL-TRAFFIC pole — if the guard refuses
 * this, every OTP sign-in is dead.
 */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/** An absolute returnUrl from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

/** The bail target for an unsafe returnUrl. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** This endpoint's machine tokens — matched against, NEVER rendered. */
const RATE_LIMITED_TOKEN = "Rate limit exceeded. Please try again later.";
const CODE_EXPIRED_TOKEN = "Code expired or not found.";
const INVALID_CODE_TOKEN = "Invalid code.";

const BLANK_EMAIL_MESSAGE = "Please enter your email.";
const BLANK_CODE_MESSAGE = "Please enter the verification code.";

const INVALID_CODE_MESSAGE = "Invalid or expired code. Please try again.";

/**
 * The ONLY send failure the service can produce is the rate limit, so the copy is
 * specific: a generic "please try again" tells a rate-limited user to do the one
 * thing that cannot work.
 */
const RATE_LIMITED_MESSAGE = "Too many code requests. Please wait a few minutes and try again.";

/** Shared with the other tabs, not re-invented here. */
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
 * rejection carries neither `status` nor `code`. That absence is the signal.
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
 * The ticket-exchange URL the shell handed the browser with `location.href = …`,
 * recovered from the hand-off the project's navigation guard vetoed. Waits for it
 * first, so callers assert on the parts rather than on the timing.
 *
 * Every verified code ends here, so a test whose subject is the REQUEST awaits it
 * too and drops the result: the hand-off is deliberate, and one left unread fails
 * that test in the project's `afterEach`.
 */
async function handoffTarget(): Promise<URL> {
  const escape = await expectNavigationEscape();

  return new URL(escape.url);
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/**
 * Render the screen as the OIDC hand-off would: a safe, relative returnUrl.
 *
 * `"returnUrl" in props` rather than `props.returnUrl ?? DEFAULT`: the
 * absent-`returnUrl` branch is itself under test, and a `??` default would
 * silently substitute for an explicit `{ returnUrl: undefined }`.
 */
function renderScreen(props: Partial<LoginScreenProps> = {}) {
  const returnUrl: string | undefined = "returnUrl" in props ? props.returnUrl : RETURN_URL;

  return renderWithClient(<LoginScreen {...props} returnUrl={returnUrl} />);
}

/** Open the OTP tab. */
async function openOtpTab(user: ReturnType<typeof userEvent.setup>) {
  await user.click(page.getByTestId("login-tab-otp"));
}

/** Fill in the OTP email field and submit it. */
async function submitEmail(user: ReturnType<typeof userEvent.setup>, email: string = EMAIL) {
  if (email !== "") {
    await user.type(page.getByTestId("login-otp-email"), email);
  }
  await user.click(page.getByTestId("login-otp-send-submit"));
}

/** Fill in the code field and submit it. */
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

/** Toggle a checkbox by CLICKING it, the way a pointer user does. */
async function toggleCheckbox(
  user: ReturnType<typeof userEvent.setup>,
  testId: string,
): Promise<void> {
  const box = page.getByTestId(testId);

  await expect.element(box).toBeInTheDocument();
  await user.click(box);
}

beforeEach(() => {
  vi.clearAllMocks();
  harness = createPassthroughHarness();
  harness.respond(defaultWire);
});

describe("LoginScreen OTP tab: sending", () => {
  it("shows the email field and send button in place of the password panel", async () => {
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);

    await expect.element(page.getByTestId("login-otp-email")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-otp-send-submit")).toBeInTheDocument();
    // One panel at a time.
    expect(page.getByTestId("login-password").query()).toBeNull();
    expect(page.getByTestId("login-magic-link-email").query()).toBeNull();
  });

  it("starts on the email form, not the code form", async () => {
    // A code box on arrival invites a user to hunt for a mail that was never sent.
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
    // A blank send cannot succeed and would spend the address's rate-limit
    // allowance.
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user, "");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(0);
  });

  it("refuses a whitespace-only email without calling the API", async () => {
    // Whitespace-only input is blank, not merely empty.
    const user = userEvent.setup();
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user, "   ");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_EMAIL_MESSAGE);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(0);
  });

  it("sends exactly the typed email, and no returnUrl or client_id", async () => {
    // The send request is `{ email }` ALONE — unlike magic-link, nothing is emailed
    // that has to resume the OIDC flow, because the user comes back to THIS live
    // form. Read off the REQUEST, so "no cargo" is a claim about the wire.
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
    // ANTI-ENUMERATION. The API answers `200 { succeeded: true }` for an unknown
    // address specifically so this screen cannot be used to discover who has an
    // account. The response is byte-identical to the happy path, so the screen
    // must be too.
    const user = userEvent.setup();
    resolveAt(OTP_SEND_ENDPOINT, { succeeded: true });
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user, "nobody@example.com");

    await expect.element(page.getByTestId("login-otp-sent")).toBeInTheDocument();
    expect(page.getByTestId("login-error").query()).toBeNull();
  });

  it("sends one code for a double-clicked send button", async () => {
    // One click, one code: a second send OVERWRITES the stored code, silently
    // invalidating the one already in the user's inbox, so the impatient user is
    // the one who gets locked out. Bound by the OUTCOME, not by the attribute.
    const user = userEvent.setup();
    const releaseSend = holdAt(OTP_SEND_ENDPOINT);
    renderScreen();

    await openOtpTab(user);
    await user.type(page.getByTestId("login-otp-email"), EMAIL);

    // `force` on the second click: the button is disabled in-flight, so the
    // actionability wait would never settle. Skipping it reproduces the impatient
    // double-click.
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
    // A banner hanging over an in-flight retry is a lie about the current attempt.
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
    // A generic "try again" is the one instruction guaranteed not to work here.
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
    // Kept DISTINCT from the generic tail: telling a user with no network that
    // "an error occurred" sends them to re-read an email that arrived fine.
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
    // A server-authored English sentence is still a machine token: matched
    // against, never shown.
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
    // JS truthiness would happily accept `"false"` and march the user to a code
    // form for a code that was never sent.
    const user = userEvent.setup();
    resolveAt(OTP_SEND_ENDPOINT, { succeeded: "false" });
    renderScreen();

    await openOtpTab(user);
    await submitEmail(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(page.getByTestId("login-otp-code").query()).toBeNull();
  });
});

describe("LoginScreen OTP tab: verifying", () => {
  it("refuses a blank code without calling the API", async () => {
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
    // The email is NOT re-typed on the code form: the code is stored against the
    // address the send used, so it has to be the address the send used.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(1);
    });
    // `rememberMe` is sent EXPLICITLY rather than omitted: the endpoint's parameter
    // is optional and defaults false, so omission and `false` are the same session
    // — but only one of them says on the wire which session the user asked for.
    expect(lastCallTo(OTP_VERIFY_ENDPOINT)?.body).toEqual({
      email: EMAIL,
      code: CODE,
      rememberMe: false,
    });
    await handoffTarget();
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
    await handoffTarget();
  });

  it("redeems a ONE-TIME code exactly once even when the button is double-clicked", async () => {
    // THE ONE-TIME-USE HAZARD. A verified code is DELETED server-side, so a second
    // submit redeems a SPENT code and paints "Invalid or expired code" over a
    // sign-in that just succeeded. Bound at the OUTCOME (how many redemptions):
    // `toBeDisabled()` would pass for anything that merely greys the button out.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    const releaseVerify = holdAt(OTP_VERIFY_ENDPOINT);
    await user.type(page.getByTestId("login-otp-code"), CODE);

    // `force` on the second click: the button is disabled in-flight, so the
    // actionability wait would never settle.
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
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await submitCode(user, "");
    await expect.element(page.getByTestId("login-error")).toHaveTextContent(BLANK_CODE_MESSAGE);

    await submitCode(user);

    await vi.waitFor(() => {
      expect(page.getByTestId("login-error").query()).toBeNull();
    });
    await handoffTarget();
  });
});

describe("LoginScreen OTP tab: verify success hands off to the shell", () => {
  it("exchanges the ticket for the returnUrl the OIDC flow supplied", async () => {
    // THE LEGITIMATE PATH. Nothing here is hostile: if this fails, the tab is an
    // outage, and a suite that only tested guards would call that a pass.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await submitCode(user);

    const target: URL = await handoffTarget();

    // SAME-ORIGIN. The passthrough proxy mounts `/v1/**` at the root, and an
    // absolute API origin would send the browser cross-origin and DROP the
    // SameSite cookie the exchange just set.
    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_TICKET_PATH);
    expect(target.searchParams.get("ticket")).toBe(TICKET);
    expect(target.searchParams.get("returnUrl")).toBe(RETURN_URL);
  });

  it("reports being signed in when there is no returnUrl to go back to", async () => {
    // Nowhere to send the user, so say so rather than invent a destination.
    const user = userEvent.setup();
    renderScreen({ returnUrl: undefined });

    await reachCodeForm(user);
    await submitCode(user);

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(navigationEscapes()).toEqual([]);
  });

  it("refuses an unsafe returnUrl instead of exchanging the ticket to it", async () => {
    // The CLIENT picks the destination on the ticket path, so the guard applies —
    // and it is the SHELL's, reached by handing the RAW body up. This panel never
    // navigates. REFUSE, don't sanitize.
    const user = userEvent.setup();
    renderScreen({ returnUrl: EVIL_RETURN_URL });

    await reachCodeForm(user);
    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(navigationEscapes()).toEqual([]);
  });

  it("defers an mfaRequired response to the shell's one branch table", async () => {
    // `otp/verify` cannot itself answer `mfaRequired` today. What is pinned is that
    // the panel hands the RAW body up rather than narrowing it: a panel that grew
    // its own `succeeded` check would swallow this body.
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
    // The copy covers both live tokens, which is why no code map earns its place.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    rejectAt(OTP_VERIFY_ENDPOINT, UNAUTHORIZED_STATUS, CODE_EXPIRED_TOKEN);
    await submitCode(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(INVALID_CODE_MESSAGE);
  });

  it("reads an unrecognised token on a 401 as a bad code, not a generic error", async () => {
    // 401 identifies this failure ALONE, so the status carries the meaning. A token
    // this screen has never heard of still means the code did not work, and
    // dropping the user to "an error occurred" hides the retry they need.
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
    // A 500 is not a bad code and must not be reported as one — that has the user
    // retyping a perfectly good code at a dead server.
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

describe("LoginScreen OTP tab: tab switching", () => {
  it("clears another tab's error banner when the OTP tab is opened", async () => {
    // One banner is shared by all three tabs, so a magic-link failure must not
    // follow the user into the OTP tab and blame it for something it did not do.
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
    // Both are panel-local state and switching tabs unmounts the panel, so the
    // shell needs no reset of its own.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);

    await user.click(page.getByTestId("login-tab-password"));
    await openOtpTab(user);

    await expect.element(page.getByTestId("login-otp-email")).toBeInTheDocument();
    expect(page.getByTestId("login-otp-code").query()).toBeNull();
  });
});

/** The OTP tab's own remember-me box — panel-local, and NOT `login-remember-me`. */
function otpRememberMe() {
  return page.getByTestId("login-otp-remember-me");
}

describe("LoginScreen OTP tab: remember me", () => {
  it("offers an unchecked remember-me box on the code form", async () => {
    // Unchecked by DEFAULT: a long-lived session is a choice the user makes, never
    // one a screen makes on their behalf.
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
    // Asserted through the behaviour the `htmlFor`/`id` pairing buys: a label wired
    // to nothing still renders perfectly.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    await user.click(page.getByText("Remember me"));

    await expect.element(otpRememberMe()).toBeChecked();
  });

  it("publishes its checked state as aria-checked", async () => {
    // A raw `<input type="checkbox">` keeps its state in the `checked` PROPERTY,
    // which no attribute reflects; the catalog's Checkbox publishes `aria-checked`.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);

    await expect.element(otpRememberMe()).toHaveAttribute("aria-checked", "false");

    await toggleCheckbox(user, "login-otp-remember-me");

    await expect.element(otpRememberMe()).toHaveAttribute("aria-checked", "true");
    await expect.element(otpRememberMe()).toBeChecked();
  });

  it("is reachable as a checkbox named by its label", async () => {
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
    await handoffTarget();
  });

  it("sends rememberMe false when the box is checked and then unchecked", async () => {
    // A one-way latch would pass the test above and still be broken.
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
    await handoffTarget();
  });

  it("does not verify or re-send merely because the box was toggled", async () => {
    // A checkbox that spends the user's one-time code on a click is worse than no
    // checkbox.
    const user = userEvent.setup();
    renderScreen();

    await reachCodeForm(user);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);

    await toggleCheckbox(user, "login-otp-remember-me");

    expect(callsTo(OTP_VERIFY_ENDPOINT)).toHaveLength(0);
    expect(callsTo(OTP_SEND_ENDPOINT)).toHaveLength(1);
  });

  it("ignores the password tab's remember-me box", async () => {
    // Ticking the password tab's box and then wandering to the OTP tab must not
    // buy a persistent session from a control that is no longer on screen: the
    // panels' states are disjoint, so the OTP request answers to the OTP box alone.
    const user = userEvent.setup();
    renderScreen();

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
    await handoffTarget();
  });

  it("does not leak its own box into the password tab", async () => {
    // The same leak in reverse: shared state fails this even if the panels merely
    // read one variable in the shell.
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
    await handoffTarget();
  });

  it("resets to unchecked when the tab is left and re-entered", async () => {
    // PANEL-LOCAL is the whole claim: switching tabs unmounts the panel, so the box
    // resets for free. A user returning to a fresh-looking form must not be
    // carrying a stale hidden answer.
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
    await handoffTarget();
  });
});

/**
 * `loginRoute` cannot be rendered bare: its component calls `Route.useSearch()`,
 * which throws outside a router. The real route object is grafted onto a memory
 * router at the URL under test.
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
    // THE WHOLE FEATURE, THROUGH THE REAL ROUTE: a user picks the OTP tab, gets a
    // code, types it, and lands back in the OIDC flow they started.
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
