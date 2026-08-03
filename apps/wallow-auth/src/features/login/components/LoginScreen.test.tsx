import {
  expectNavigationEscape,
  navigationEscapes,
} from "@bc-solutions-coder/testing/navigation-escape";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { createPassthroughHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as loginRoute } from "@app/routes/login";
import { LoginScreen, type LoginScreenProps } from "./LoginScreen";

/**
 * Login screen: password tab + tab shell.
 *
 * Runs the real SDK over a faked fetch (sdk-harness), so
 * assertions read the recorded request, not a spy.
 *
 * The login endpoint reports 3 of its 4 outcomes inside a
 * 200 body, so failure cases assert on body shape, not status.
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

const LOGIN_ENDPOINT = "/v1/identity/auth/login";

/**
 * The shell mounts `<ExternalProviders>` next to the tab panels, so this GET
 * lands on the transport in EVERY test that renders the screen.
 */
const PROVIDERS_ENDPOINT = "/v1/identity/auth/external-providers";

/** The path `buildExchangeTicketUrl` targets. */
const EXCHANGE_PATH = "/v1/identity/auth/exchange-ticket";

const OK_STATUS = 200;
const NOT_FOUND_STATUS = 404;

/** The three statuses the login endpoint rejects with, plus its generic tail. */
const UNAUTHORIZED_STATUS = 401;
const FORBIDDEN_STATUS = 403;
const LOCKED_STATUS = 423;
const SERVER_ERROR_STATUS = 500;

/**
 * The returnUrl `/connect/authorize` really sends: relative, and already past
 * the server's own local-url check. This is the REAL-TRAFFIC pole of the
 * open-redirect guard — if the guard refuses this, every direct login is dead.
 */
const RETURN_URL = "/connect/authorize?client_id=web&scope=openid";

/** An absolute returnUrl from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** An API origin the exchange URL must never be prepended with. */
const API_ORIGIN = "localhost:5001";

const BLANK_MESSAGE = "Please enter your email and password.";

const INVALID_CREDENTIALS_MESSAGE = "Invalid email or password.";
const LOCKED_OUT_MESSAGE = "Account locked. Try again later.";
const EMAIL_NOT_CONFIRMED_MESSAGE = "Please verify your email before signing in.";
const GENERIC_MESSAGE = "An error occurred. Please try again.";

const UNREACHABLE_MESSAGE = "Unable to reach the server. Please try again later.";

const EXTERNAL_LOGIN_FAILED_MESSAGE =
  "External sign-in failed. Please try again or use a different method.";
const SESSION_EXPIRED_MESSAGE = "Your session has expired. Please try again.";

/**
 * `ResetPasswordForm` navigates to `/login?message=password_reset`. The key
 * phrase is a regex so the banner reads as an acknowledgment without
 * over-constraining the wording.
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
 * The token goes in `extensions.code` — where ASP.NET Core puts it — AND the
 * status is the real transport status, so these fixtures bind the copy
 * assertions whether the screen keys off the machine token or falls back to the
 * status. `title` stays "Unknown error": these endpoints ship no human-readable
 * title, so the screen must supply its own copy rather than echoing the server's.
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
 * status nor a code. It must NOT collapse into the same copy as a 4xx — "the
 * server said no" and "the server never answered" are different instructions.
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
 * Wait for the ticket hand-off — `globalThis.location.href = …`, which the
 * project's navigation guard vetoes and records — and return its target, parsed.
 * The real `buildExchangeTicketUrl` runs against the `""` origin, so a correct
 * screen produces a SAME-ORIGIN absolute URL.
 *
 * A succeeded sign-in always ends here, so a test whose subject is the REQUEST
 * awaits it too and drops the result: the hand-off is deliberate, and one left
 * unread fails that test in the project's `afterEach`.
 */
async function awaitHandoff(): Promise<URL> {
  const escape = await expectNavigationEscape();

  return new URL(escape.url);
}

/** An ISO-8601 `DateTimeOffset` N days from now, as the grace deadline arrives. */
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
 * substitute the default for an explicit `{ returnUrl: undefined }`. Same for
 * `""`, which is NOT nullish and must reach the screen intact.
 */
function renderScreen(props: Partial<LoginScreenProps> = {}) {
  const returnUrl: string | undefined = "returnUrl" in props ? props.returnUrl : RETURN_URL;

  return renderWithClient(<LoginScreen {...props} returnUrl={returnUrl} />);
}

/** Fill in the password tab and submit it. */
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
  respondWithLogin({ succeeded: true, signInTicket: TICKET });
  harness.respond((call) => {
    if (call.path === LOGIN_ENDPOINT) {
      return loginReply();
    }

    // `<ExternalProviders>` mounts with the shell. An empty list is the
    // "no providers configured" answer and renders nothing.
    if (call.path === PROVIDERS_ENDPOINT) {
      return Response.json([], { status: OK_STATUS });
    }

    // The route-level tests carry `client_id=web`, so `/login` also asks for that
    // client's branding overlay. A bare 404 is "no branding configured" and
    // leaves the fork's chrome in place.
    return new Response(null, { status: NOT_FOUND_STATUS });
  });
});

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
    // One error banner is shared by all three tabs.
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

describe("LoginScreen tab shell: the WAI-ARIA tabs contract", () => {
  it("wires the visible panel to the tab that owns it", async () => {
    await renderScreen();

    const panel = page.getByRole("tabpanel");
    await expect.element(panel).toBeInTheDocument();

    const panelElement: HTMLElement = panel.element() as HTMLElement;
    const selectedTab: HTMLElement = page
      .getByTestId("login-tab-password")
      .element() as HTMLElement;

    expect(panelElement.getAttribute("aria-labelledby")).toBe(selectedTab.id);
    expect(selectedTab.getAttribute("aria-controls")).toBe(panelElement.id);

    expect(panelElement.querySelector('[data-testid="login-password"]')).not.toBeNull();
  });

  it("mounts only the selected tab's panel", async () => {
    // A second, hidden password form left in the DOM is a second form users
    // can tab into.
    await renderScreen();

    await expect.element(page.getByRole("tabpanel")).toBeInTheDocument();

    expect(page.getByRole("tabpanel").all()).toHaveLength(1);
  });

  it("keeps only the selected tab in the tab sequence", async () => {
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

    // Switching tabs under a user browsing the strip would throw away
    // whatever they had typed in the panel below.
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
    await renderScreen();

    await expect.element(page.getByRole("checkbox", { name: "Remember me" })).toBeInTheDocument();
  });

  it("publishes its checked state as aria-checked", async () => {
    // A raw `<input type="checkbox">` keeps its state in the `checked` PROPERTY,
    // which no attribute reflects; the catalog's Checkbox publishes `aria-checked`.
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

describe("LoginScreen password-reset notice", () => {
  it("shows a success banner when arriving with message=password_reset", async () => {
    await renderScreen({ message: "password_reset" });

    await expect
      .element(page.getByTestId("login-password-reset-notice"))
      .toHaveTextContent(PASSWORD_RESET_NOTICE);
  });

  it("keeps the sign-in form usable beneath the notice", async () => {
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
    // `?message=` is attacker-constructable, so nothing unrecognised may
    // reach the DOM.
    await renderScreen({ message: "wat" });

    await expect.element(page.getByTestId("login-password")).toBeInTheDocument();
    expect(page.getByTestId("login-password-reset-notice").query()).toBeNull();
  });
});

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
    // Whitespace-only input is blank, not merely empty.
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
    await awaitHandoff();
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
    await awaitHandoff();
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

    // Wait for the request to REACH the transport: the button disables a tick
    // or two before `fetch` is called, and releasing into that gap would leave
    // the never-settling responder installed forever.
    await vi.waitFor(() => {
      expect(loginCalls()).toHaveLength(1);
    });
    await expect.element(page.getByTestId("login-submit")).toBeDisabled();

    release();
    await awaitHandoff();
  });

  it("clears the previous error before retrying", async () => {
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
    // Known tokens first, HTTP status as a FALLBACK. A code-only map would drop
    // this to generic and stop telling a locked-out user why retyping cannot help.
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
    rejectLogin(SERVER_ERROR_STATUS, "some_new_token");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).not.toHaveTextContent("some_new_token");
  });

  it("tells the user the server is unreachable when the request never lands", async () => {
    // A network rejection carries neither code nor status.
    failLoginTransport();
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(UNREACHABLE_MESSAGE);
  });

  it("fails closed when the 200 body is not a shape this screen understands", async () => {
    // `login` is typed `Promise<unknown>`; the screen narrows structurally.
    respondWithLogin("not an object at all");
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(navigationEscapes()).toEqual([]);
  });

  it("does not accept a stringly-typed succeeded flag", async () => {
    // JS truthiness would let the non-empty string "false" through, so the
    // comparison has to be strict.
    respondWithLogin({ succeeded: "false", signInTicket: TICKET });
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
    expect(navigationEscapes()).toEqual([]);
  });
});

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
    // The ticket exchange is the screen's only writer of `location.href`, so an
    // empty escape record is the browser-true "no full page load happened".
    expect(navigationEscapes()).toEqual([]);
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
    // `/mfa/challenge` is a CONSTANT same-origin path and `returnUrl` is inert
    // cargo the destination re-guards on arrival, so the guard is DEFERRED here:
    // wiring `isSafeReturnUrl` in would refuse all external-login traffic.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: "http://localhost:5002/login" });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({
        href: `/mfa/challenge?returnUrl=${encodeURIComponent("http://localhost:5002/login")}`,
      });
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
    expect(navigationEscapes()).toEqual([]);
  });

  it("encodes the returnUrl so it cannot smuggle a second query key", async () => {
    // What a DEFERRED guard still owes: the cargo must land as ONE value. A raw
    // interpolation would let `&cookieRelay=…` split into its own key, and ASP.NET
    // binds a duplicated [FromQuery] as "a,b" — a silently wrong branch.
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

describe("LoginScreen mfaEnrollmentRequired branch", () => {
  it("hands off to /mfa/enroll when enrollment is required with no grace deadline", async () => {
    // Grace expired server-side, so no deadline is sent at all.
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
    // The deadline is COMPARED, not merely checked for presence: reading it as
    // "present" would keep this user on the login page instead of enrolling them.
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
    expect(navigationEscapes()).toEqual([]);
  });
});

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
    // The only configuration in which the banner is ever SEEN: with a returnUrl
    // the screen navigates away before it can be read.
    respondWithLogin(graceResult());
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-mfa-enrollment-banner")).toBeInTheDocument();
    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(navigationEscapes()).toEqual([]);
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
    // Grace does NOT short-circuit the hand-off; the user keeps signing in.
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

describe("LoginScreen sign-in ticket exchange", () => {
  it("passes the real returnUrl the authorize endpoint sends", async () => {
    // POLE 1 — REAL TRAFFIC MUST PASS. This is the shape the server really
    // sends, already past its own local-url check; a guard that refused it would
    // dead-end every direct sign-in.
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
    // The passthrough proxy mounts /v1/** at the root, so a cross-origin exchange
    // would drop the SameSite cookie the endpoint sets — the point of the ticket.
    const user = userEvent.setup();
    await renderScreen();

    await submitCredentials(user);

    const target: URL = await awaitHandoff();
    expect(target.origin).toBe(globalThis.location.origin);
    expect(target.pathname).toBe(EXCHANGE_PATH);
    expect(target.href).not.toContain(API_ORIGIN);
  });

  it("refuses an absolute returnUrl before exchanging the ticket", async () => {
    // POLE 2 — here the CLIENT picks the destination, so the guard belongs on
    // this path.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: EVIL_RETURN_URL });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(navigationEscapes()).toEqual([]);
  });

  it("refuses a protocol-relative returnUrl", async () => {
    // `//evil.example.com` is the classic bypass of a naive `startsWith("/")`.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: "//evil.example.com/steal" });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(navigationEscapes()).toEqual([]);
  });

  it("shows the signed-in state when there is no returnUrl", async () => {
    const user = userEvent.setup();
    await renderScreen({ returnUrl: undefined });

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
    expect(navigationEscapes()).toEqual([]);
  });

  it("treats an empty returnUrl as no returnUrl, not as an attack", async () => {
    // `""` is NOT nullish and IS unsafe by `isSafeReturnUrl`, so a screen that
    // guarded before checking emptiness would send an ordinary user to /error.
    // Emptiness is checked FIRST; order is load-bearing.
    const user = userEvent.setup();
    await renderScreen({ returnUrl: "" });

    await submitCredentials(user);

    await expect.element(page.getByTestId("login-signed-in")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
    expect(navigationEscapes()).toEqual([]);
  });

  it("does not leave the sign-in button spinning after it refuses", async () => {
    const user = userEvent.setup();
    await renderScreen({ returnUrl: EVIL_RETURN_URL });

    await submitCredentials(user);

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    await expect.element(page.getByTestId("login-submit")).toBeEnabled();
  });
});

/**
 * A real memory router, not a bare render of `Route.options.component`: a
 * search-reading route component always dies on `router.stores` outside a
 * `RouterProvider`. The root is a throwaway — the app's real `__root.tsx`
 * renders `<html>`.
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
    // A Record + bracket lookup resolves INHERITED keys, so `?error=toString`
    // hands `Object.prototype.toString` — a FUNCTION — to the renderer. Only a
    // `ReadonlyMap` + `.get()` sees just the keys put in it: do not "simplify"
    // the lookup back to an object literal.
    await renderRouteAt("/login?error=toString");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("does not resolve the constructor key from the error param", async () => {
    await renderRouteAt("/login?error=constructor");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("survives an error param that the search parser turns into a boolean", async () => {
    // TanStack's default search parser JSON-parses EVERY query value before
    // `validateSearch` sees it, so `?error=true` arrives as the BOOLEAN true, and
    // `typeof x === "string" ? x : undefined` would silently swallow it.
    await renderRouteAt("/login?error=true");

    await expect.element(page.getByTestId("login-error")).toHaveTextContent(GENERIC_MESSAGE);
  });

  it("ignores a non-scalar error param rather than throwing", async () => {
    await renderRouteAt("/login?error=%5B1%2C2%5D");

    await expect.element(page.getByTestId("login-submit")).toBeInTheDocument();
  });

  it("surfaces the password-reset notice from the message query param", async () => {
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
