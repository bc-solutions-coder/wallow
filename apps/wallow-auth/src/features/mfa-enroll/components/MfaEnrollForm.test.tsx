import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { type SdkCall, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { AUTH_HARNESS_ORIGIN, createAuthHarness } from "@shared/testing/harness";
import { Route as mfaEnrollRoute } from "@app/routes/mfa/enroll";
import { MfaEnrollForm } from "./MfaEnrollForm";

/**
 * MFA enroll screen + its route, driven by a real SDK over a faked fetch (sdk-harness):
 * assertions read the recorded request, and each failure fixture is the body the controller
 * writes — a non-2xx whose bare `error` member becomes the screen's `code`. A 401 means the
 * enrollment session is gone, so it must NOT say "try again": no number of retries mints the
 * partial-auth cookie.
 *
 * `mfa-enroll-begin-setup` is a RETRY — enrollment fires on mount, so that branch is reachable
 * only once it has failed.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const SECRET = "JBSWY3DPEHPK3PXP";
const QR_URI = "otpauth://totp/Wallow:user@test.local?secret=JBSWY3DPEHPK3PXP&issuer=Wallow";
const CODE = "123456";
const ENROLL_TOKEN = "enroll-token-abc123";
const BACKUP_CODES = ["aaaa-1111", "bbbb-2222", "cccc-3333"];
const RETURN_URL = "/connect/authorize?client_id=web";

/** The bail target for an unsafe returnUrl, matching the ConsentScreen port. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** The three endpoints this screen touches. */
const TOTP_ENDPOINT = "/v1/identity/mfa/enroll/totp";
const CONFIRM_ENDPOINT = "/v1/identity/mfa/enroll/confirm";
const EXCHANGE_ENDPOINT = "/v1/identity/mfa/enroll/exchange-token";

const OK = 200;
const BAD_REQUEST = 400;
const UNAUTHORIZED = 401;
const SERVER_ERROR = 500;

/** How one endpoint answers a single call. */
type EndpointResponder = () => Response | Promise<Response>;

/** `200 { secret, qrUri }` — `enroll/totp`'s only success. */
const okTotp: EndpointResponder = () =>
  Response.json({ secret: SECRET, qrUri: QR_URI }, { status: OK });

/** `200 { succeeded: true, backupCodes }` — `enroll/confirm`'s only success. */
const okConfirm: EndpointResponder = () =>
  Response.json({ succeeded: true, backupCodes: BACKUP_CODES }, { status: OK });

/** `200 { succeeded: true }` — the token exchange's only success. */
const okExchange: EndpointResponder = () => Response.json({ succeeded: true }, { status: OK });

/**
 * A failure carrying NO reason at all, only the status — what binds the STATUS fallback, which
 * must survive because `.code` is not a guaranteed-stable token.
 */
function failWithStatus(status: number): EndpointResponder {
  return () => Response.json({}, { status });
}

/** A failure in the shape the endpoint really writes: a bare body whose `error` is the token. */
function failWithCode(status: number, code: string): EndpointResponder {
  return () => Response.json({ succeeded: false, error: code }, { status });
}

/** Answer `first` once, then `rest` forever. */
function once(first: EndpointResponder, rest: EndpointResponder): EndpointResponder {
  let used = false;
  return () => {
    if (!used) {
      used = true;
      return first();
    }
    return rest();
  };
}

/**
 * Program the wire per ENDPOINT: one transport serves all three calls, so responses are
 * dispatched on the recorded path. Anything unlisted answers the happy body.
 */
function program(
  overrides: {
    totp?: EndpointResponder;
    confirm?: EndpointResponder;
    exchange?: EndpointResponder;
  } = {},
): void {
  const totp: EndpointResponder = overrides.totp ?? okTotp;
  const confirm: EndpointResponder = overrides.confirm ?? okConfirm;
  const exchange: EndpointResponder = overrides.exchange ?? okExchange;

  harness.respond((call: SdkCall) => {
    switch (call.path) {
      case TOTP_ENDPOINT: {
        return totp();
      }
      case CONFIRM_ENDPOINT: {
        return confirm();
      }
      case EXCHANGE_ENDPOINT: {
        return exchange();
      }
      default: {
        return Response.json({}, { status: OK });
      }
    }
  });
}

/** Every recorded request to one endpoint, in order. */
function callsTo(path: string): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === path);
}

/**
 * The screen hands off with a bare `globalThis.location.href = …`, and `window.location` is
 * `[Unforgeable]` in a real browser — it can be neither stubbed nor redefined. So the
 * assignment is observed at the one seam Chromium leaves open: the Navigation API's cancelable
 * `navigate` event, whose `preventDefault()` captures the target without letting the iframe
 * navigate and tear the runner down. `destination.url` is absolute, so `relative()` is what
 * compares against a this-origin expectation.
 */
interface NavigateEventLike {
  readonly destination: { readonly url: string };
  readonly cancelable: boolean;
  preventDefault: () => void;
}

interface NavigationLike {
  addEventListener: (type: "navigate", listener: (event: NavigateEventLike) => void) => void;
  removeEventListener: (type: "navigate", listener: (event: NavigateEventLike) => void) => void;
}

interface NavCapture {
  /** The full URL of the intercepted navigation, or null if none has fired. */
  absolute: () => string | null;
  /** `pathname + search` of that URL — the this-origin-relative form. */
  relative: () => string | null;
}

const navDisposers: Array<() => void> = [];

function interceptNavigation(): NavCapture {
  let target: string | null = null;
  const nav = (globalThis as unknown as { navigation: NavigationLike }).navigation;
  const listener = (event: NavigateEventLike): void => {
    if (!event.cancelable) {
      return;
    }
    target = event.destination.url;
    event.preventDefault();
  };
  nav.addEventListener("navigate", listener);
  navDisposers.push(() => {
    nav.removeEventListener("navigate", listener);
  });

  return {
    absolute: () => target,
    relative: () => {
      if (target === null) {
        return null;
      }
      const parsed = new URL(target);
      return parsed.pathname + parsed.search;
    },
  };
}

let harness: SdkHarness;

function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

function renderForm(props: { returnUrl?: string; enrollToken?: string } = {}) {
  return renderWithClient(<MfaEnrollForm {...props} />);
}

/** Wait for enrollment to land, i.e. for the confirm form to exist. */
async function waitForSecret(): Promise<void> {
  await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
}

/** Type the verification code and submit. */
async function submitCode(user: ReturnType<typeof userEvent.setup>, code: string = CODE) {
  if (code !== "") {
    await user.type(page.getByTestId("mfa-enroll-code"), code);
  }
  await user.click(page.getByTestId("mfa-enroll-submit"));
}

afterEach(() => {
  for (const dispose of navDisposers) {
    dispose();
  }
  navDisposers.length = 0;
});

beforeEach(() => {
  vi.clearAllMocks();
  harness = createAuthHarness();
  program();
});

describe("MfaEnrollForm — starting enrollment", () => {
  it("asks the API for a secret on mount, passing nothing with it", async () => {
    // The empty request is the point: nothing is relayed alongside it, because the
    // `Identity.MfaPartial` cookie rides the same-origin request itself.
    renderForm();

    await vi.waitFor(() => {
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });
    const enroll: SdkCall | undefined = callsTo(TOTP_ENDPOINT)[0];
    expect(enroll?.method).toBe("POST");
    expect(enroll?.body).toBeUndefined();
    expect(enroll?.url).toBe(`${AUTH_HARNESS_ORIGIN}${TOTP_ENDPOINT}`);
  });

  it("shows the QR code keyed to the otpauth uri the API returned", async () => {
    // The QR must encode the uri the API minted, not one the screen reassembled. `data-qr-uri`
    // is the seam for that; asserting on rendered SVG pixels would pin a library choice this
    // spec has no opinion about.
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-qr")).toHaveAttribute("data-qr-uri", QR_URI);
  });

  it("shows the secret for manual entry when the camera is not an option", async () => {
    // The fallback path for a user on the same device as their authenticator.
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-secret")).toHaveTextContent(SECRET);
  });

  it("shows the verification-code form once the secret arrives, with no error", async () => {
    renderForm();
    await waitForSecret();

    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-submit")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();
  });

  it("still shows the secret and the form when the response carries no qr uri", async () => {
    // The QR is optional — a missing qrUri degrades to manual entry rather than blanking the
    // form. The positive half is load-bearing: "no QR element" is trivially true of a page
    // that rendered nothing.
    program({ totp: () => Response.json({ secret: SECRET, qrUri: null }, { status: OK }) });
    renderForm();
    await waitForSecret();

    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-qr").query()).toBeNull();
  });

  it("withholds the code form until the secret is in hand", async () => {
    // A code field with no secret behind it cannot be confirmed: the confirm body is
    // `{ secret, code }`. The trailing positive assertion keeps this honest — an empty stub
    // also has no code field.
    let release!: () => void;
    program({
      totp: async () =>
        await new Promise<Response>((resolve) => {
          release = () => {
            resolve(okTotp() as Response);
          };
        }),
    });
    renderForm();

    // Wait for the request to REACH the transport before releasing: the responder is what
    // hands `release` back, so releasing earlier would call an unassigned binding.
    await vi.waitFor(() => {
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });
    expect(page.getByTestId("mfa-enroll-code").query()).toBeNull();
    expect(page.getByTestId("mfa-enroll-secret").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
  });

  it("shows no backup codes before the code is confirmed", async () => {
    // Backup codes are minted by `enroll/confirm`, not `enroll/totp`. Anchored against the
    // form that must be showing instead.
    renderForm();
    await waitForSecret();

    await expect.element(page.getByTestId("mfa-enroll-submit")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-backup-codes").query()).toBeNull();
  });

  it("offers a way out of enrollment", async () => {
    // A user who opened this by mistake must not be trapped in it.
    renderForm();
    await waitForSecret();

    await expect.element(page.getByTestId("mfa-enroll-cancel")).toHaveAttribute("href", "/");
  });
});

describe("MfaEnrollForm — the enrollment-token path", () => {
  it("exchanges the token for a session BEFORE asking for a secret", async () => {
    // Order is the entire contract: the exchange mints the `Identity.MfaPartial` cookie, so
    // `enroll/totp` fired first has no session to resolve and 401s. Per-endpoint call counts
    // cannot catch a reversed order — the recorded request LOG is the wire order itself.
    renderForm({ enrollToken: ENROLL_TOKEN });

    await vi.waitFor(() => {
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });
    expect(harness.calls.map((call: SdkCall) => call.path)).toEqual([
      EXCHANGE_ENDPOINT,
      TOTP_ENDPOINT,
    ]);
  });

  it("hands the token over verbatim", async () => {
    // The token is a data-protected blob, so any mangling fails to unprotect. It rides the
    // QUERY STRING, so it is read off `url`, not `path`.
    renderForm({ enrollToken: ENROLL_TOKEN });

    await vi.waitFor(() => {
      expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
    });
    const exchange: SdkCall | undefined = callsTo(EXCHANGE_ENDPOINT)[0];
    expect(new URL(exchange?.url ?? "").searchParams.get("token")).toBe(ENROLL_TOKEN);
  });

  it("skips the exchange entirely on the ordinary sign-in flow", async () => {
    // No token means the user arrived mid-login and already holds a partial-auth cookie.
    // Anchored on the enrollment that MUST still happen.
    renderForm();

    await vi.waitFor(() => {
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });
    expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(0);
  });

  it("surfaces an error and does not enroll when the token is expired", async () => {
    // The token's lifetime is seconds, so expiry is easy to hit. Calling `enroll/totp` anyway
    // would just 401 and blame the wrong thing.
    program({ exchange: failWithStatus(BAD_REQUEST) });
    renderForm({ enrollToken: ENROLL_TOKEN });

    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(0);
  });
});

describe("MfaEnrollForm — when enrollment cannot start", () => {
  it("explains that setup could not begin", async () => {
    program({ totp: failWithStatus(SERVER_ERROR) });
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/try again/iu);
  });

  it("says the session is gone rather than telling the user to retry", async () => {
    // `enroll/totp` has exactly one 401, `no_auth_session`, so the status is unambiguous.
    program({ totp: failWithCode(UNAUTHORIZED, "no_auth_session") });
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("offers begin-setup as the way back", async () => {
    program({ totp: failWithStatus(SERVER_ERROR) });
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
  });

  it("retries enrollment on begin-setup, clearing the standing error", async () => {
    // A stale error sitting above a freshly-minted QR code is a lie.
    program({ totp: once(failWithStatus(SERVER_ERROR), okTotp) });
    const user = userEvent.setup();
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
    await user.click(page.getByTestId("mfa-enroll-begin-setup"));

    await waitForSecret();
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(2);
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();
  });
});

describe("MfaEnrollForm — confirming the code", () => {
  it("requires a code before calling the endpoint", async () => {
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user, "");

    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .toHaveTextContent(/enter the verification code/iu);
    expect(callsTo(CONFIRM_ENDPOINT)).toHaveLength(0);
  });

  it("treats a whitespace-only code as blank", async () => {
    // Spaces are not a code: the guard trims before it decides.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user, "   ");

    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .toHaveTextContent(/enter the verification code/iu);
    expect(callsTo(CONFIRM_ENDPOINT)).toHaveLength(0);
  });

  it("sends the enrolled secret with the typed code, and nothing else", async () => {
    // The secret MUST be the one `enroll/totp` just minted: the server re-validates the TOTP
    // against the secret in the body before storing it. The exact-object assertion is also
    // what says nothing is relayed alongside those two fields.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(CONFIRM_ENDPOINT)).toHaveLength(1);
    });
    const confirm: SdkCall | undefined = callsTo(CONFIRM_ENDPOINT)[0];
    expect(confirm?.method).toBe("POST");
    expect(confirm?.body).toEqual({ secret: SECRET, code: CODE });
  });

  it("disables submit while the confirm call is in flight", async () => {
    // A double submit burns the TOTP window and mints backup codes twice.
    let release!: () => void;
    program({
      confirm: async () =>
        await new Promise<Response>((resolve) => {
          release = () => {
            resolve(okConfirm() as Response);
          };
        }),
    });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    // The button goes disabled a tick BEFORE `fetch` is reached, so wait for the request to
    // land before asserting and releasing.
    await vi.waitFor(() => {
      expect(callsTo(CONFIRM_ENDPOINT)).toHaveLength(1);
    });
    await expect.element(page.getByTestId("mfa-enroll-submit")).toBeDisabled();

    release();
    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
  });
});

describe("MfaEnrollForm — a confirmed code", () => {
  it("shows the backup codes", async () => {
    // This is the only moment these codes are ever visible.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
  });

  it("shows every code the API returned", async () => {
    // A truncated list locks the user out later.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    const panel = page.getByTestId("mfa-enroll-backup-codes");
    for (const backupCode of BACKUP_CODES) {
      await expect.element(panel).toHaveTextContent(backupCode);
    }
  });

  it("retires the code form once enrollment succeeds", async () => {
    // A live code box under the success state invites a second submit, which would regenerate
    // the codes the user is mid-way through writing down.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-code").query()).toBeNull();
    expect(page.getByTestId("mfa-enroll-submit").query()).toBeNull();
  });

  it("shows the success state even when the API returns no codes", async () => {
    // An empty list is still a successful enrollment and MFA really is on, so falling back to
    // the error state would tell the user a lie about their account.
    program({ confirm: () => Response.json({ succeeded: true }, { status: OK }) });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();
  });

  it("hands off to the return url on THIS origin, not an API origin", async () => {
    // The proxy serves `/connect/**` from this origin, so prepending an API origin would drop
    // the cookie `enroll/confirm` just upgraded to full auth.
    const user = userEvent.setup();
    const nav = interceptNavigation();
    renderForm({ returnUrl: RETURN_URL });
    await waitForSecret();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-enroll-done")).toBeInTheDocument();
    await user.click(page.getByTestId("mfa-enroll-done"));

    await vi.waitFor(() => {
      expect(nav.absolute()).not.toBeNull();
    });
    expect(nav.relative()).toBe(RETURN_URL);
    expect(new URL(nav.absolute() as string).origin).toBe(globalThis.location.origin);
  });

  it("sends a user who arrived without a return url home", async () => {
    // A nullish returnUrl is a legitimate direct enrollment, not an attack, so it gets the "/"
    // fallback — only a PRESENT-but-unsafe value is refused.
    const user = userEvent.setup();
    const nav = interceptNavigation();
    renderForm();
    await waitForSecret();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-enroll-done")).toBeInTheDocument();
    await user.click(page.getByTestId("mfa-enroll-done"));

    await vi.waitFor(() => {
      expect(nav.absolute()).not.toBeNull();
    });
    expect(nav.relative()).toBe("/");
  });
});

describe("MfaEnrollForm — a rejected code", () => {
  it("tells the user the verification code was wrong on a 400", async () => {
    // Reached by STATUS: this failure carries no reason token at all, which binds the fallback.
    program({ confirm: failWithStatus(BAD_REQUEST) });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .toHaveTextContent(/invalid verification code/iu);
  });

  it("says the session is gone on a 401 rather than blaming the code", async () => {
    // `no_auth_session` is the ONLY 401 `enroll/confirm` emits, so telling this user their code
    // was invalid sends them to retype a code that can never work.
    program({ confirm: failWithStatus(UNAUTHORIZED) });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("falls back to the generic message on an unrecognised status", async () => {
    program({ confirm: failWithStatus(SERVER_ERROR) });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/try again/iu);
    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .not.toHaveTextContent(/invalid verification code/iu);
  });

  it("falls back to the generic message when the failure names no status", async () => {
    // The transport throws before a response exists, so there is no status anywhere. Narrowing
    // has to be STRUCTURAL — a screen may not `instanceof WallowError`, since it need not
    // import the SDK.
    program({
      confirm: () => {
        throw new TypeError("Failed to fetch");
      },
    });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/try again/iu);
  });

  it("never leaks a raw rejection or a machine reason token into the page", async () => {
    // The screen holds the API's machine token; none of them is a message for a human.
    program({ confirm: failWithCode(BAD_REQUEST, "invalid_code") });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    const error = page.getByTestId("mfa-enroll-error");
    await expect.element(error).toBeInTheDocument();
    await expect
      .element(error)
      .not.toHaveTextContent(/invalid_code|no_auth_session|update_failed/u);
    await expect.element(error).not.toHaveTextContent(/UNKNOWN|Unknown error/u);
  });

  it("leaves the form up so the user can retype the code", async () => {
    // The TOTP window rolls every 30 seconds, so the common cause of a rejected code is a
    // stale one and the next attempt succeeds.
    program({ confirm: failWithCode(BAD_REQUEST, "invalid_code") });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-enroll-submit")).toBeEnabled();
    expect(page.getByTestId("mfa-enroll-backup-codes").query()).toBeNull();
  });

  it("keeps the same secret across a retry", async () => {
    // The QR the user already scanned is bound to THIS secret. Re-enrolling behind
    // their back would silently invalidate the authenticator entry they just made.
    program({ confirm: once(failWithCode(BAD_REQUEST, "invalid_code"), okConfirm) });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    await submitCode(user);

    await vi.waitFor(() => {
      expect(callsTo(CONFIRM_ENDPOINT)).toHaveLength(2);
    });
    // The SECOND attempt still carries the FIRST secret, and `enroll/totp` was hit once.
    expect(callsTo(CONFIRM_ENDPOINT)[1]?.body).toEqual({ secret: SECRET, code: CODE });
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
  });

  it("clears the error once a later attempt succeeds", async () => {
    program({ confirm: once(failWithCode(BAD_REQUEST, "invalid_code"), okConfirm) });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();
  });
});

/**
 * The token-keyed half of the error mapping, which the status-only fixtures above cannot reach:
 * `user_not_found` and `update_failed` are server-side write failures sharing their 400 with
 * `invalid_code`, so the status alone can only tell a user whose account write failed to retype
 * a code that was already correct. An unrecognised token still falls through to the status rule.
 */
describe("MfaEnrollForm — the reason token the API sends", () => {
  it("blames the code when the API says invalid_code", async () => {
    program({ confirm: failWithCode(BAD_REQUEST, "invalid_code") });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .toHaveTextContent(/invalid verification code/iu);
  });

  it("does NOT blame the code when the write failed rather than the code", async () => {
    // The user's code was fine, so telling them to retype it is a loop they cannot escape.
    program({ confirm: failWithCode(BAD_REQUEST, "update_failed") });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    const error = page.getByTestId("mfa-enroll-error");
    await expect.element(error).toHaveTextContent(/try again/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("does NOT blame the code when the user vanished mid-flow", async () => {
    // The other should-never-happen 400.
    program({ confirm: failWithCode(BAD_REQUEST, "user_not_found") });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    const error = page.getByTestId("mfa-enroll-error");
    await expect.element(error).toHaveTextContent(/try again/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("names the session on no_auth_session even when no status rides along", async () => {
    // Keyed on the TOKEN, not the status: every response over a real transport has one, so the
    // way to isolate the token is a status whose fallback says something ELSE. A 400 maps to
    // "invalid verification code" on status alone, so only the token yields this message.
    program({ confirm: failWithCode(BAD_REQUEST, "no_auth_session") });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("names the expired LINK, not the session, when the token exchange is refused", async () => {
    // The user's fix is to start setup again from the app that linked them here, which a
    // generic "try again" would not tell them.
    program({ exchange: failWithCode(BAD_REQUEST, "invalid_or_expired_token") });
    renderForm({ enrollToken: ENROLL_TOKEN });

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/expired/iu);
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(0);
  });

  it("still never renders the raw token, whatever the API sends", async () => {
    program({ confirm: failWithCode(BAD_REQUEST, "update_failed") });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    const error = page.getByTestId("mfa-enroll-error");
    await expect.element(error).toBeInTheDocument();
    await expect
      .element(error)
      .not.toHaveTextContent(
        /invalid_code|no_auth_session|update_failed|user_not_found|invalid_or_expired_token/u,
      );
  });
});

describe("MfaEnrollForm — the open-redirect guard", () => {
  it("refuses a protocol-relative return url before enrolling", async () => {
    // REFUSE to /error, do not rewrite to "/" — silently sanitizing an unsafe returnUrl
    // swallows the attempt. Refused on MOUNT: do not make a user set up a second factor for a
    // destination already decided against. The guard is the SDK's real `isSafeReturnUrl`.
    renderForm({ returnUrl: "//evil.example.com/steal" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(harness.calls).toHaveLength(0);
  });

  it("refuses an absolute return url", async () => {
    renderForm({ returnUrl: "https://evil.example.com/steal" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(harness.calls).toHaveLength(0);
  });

  it("refuses an empty-string return url", async () => {
    // `""` is present-but-unsafe, not absent — only a NULLISH value earns the "/" fallback.
    renderForm({ returnUrl: "" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(harness.calls).toHaveLength(0);
  });

  it("does not refuse a missing return url", async () => {
    // A direct/non-OIDC enrollment is legitimate and must proceed.
    renderForm();

    await vi.waitFor(() => {
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
  });

  it("enrolls normally behind a safe return url", async () => {
    renderForm({ returnUrl: RETURN_URL });

    await waitForSecret();
    expect(mocks.navigate).not.toHaveBeenCalledWith({ href: ERROR_HREF });
  });
});

describe("MfaEnrollForm — the relay is gone", () => {
  it("enrolls exactly once per mount", async () => {
    // A second call mints a SECOND secret and invalidates the QR the user has already
    // scanned, so this is what a stray effect dependency would break.
    renderForm();

    await waitForSecret();
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
  });

  it("exchanges the enrollment token exactly once per mount", async () => {
    // The token is single-purpose and short-lived; a second exchange is a wasted round trip.
    renderForm({ enrollToken: ENROLL_TOKEN });

    await waitForSecret();
    expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
  });
});

/**
 * Rendered through a real memory router rather than by poking at `Route.options.component`:
 * the criteria under test — returnUrl and enrollToken read off the query string — only exist
 * once a router has parsed a URL.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/mfa/enroll", route: mfaEnrollRoute }],
  });
}

describe("/mfa/enroll route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    renderRouteAt("/mfa/enroll");

    await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("threads the returnUrl from the query string into the hand-off", async () => {
    const user = userEvent.setup();
    const nav = interceptNavigation();
    renderRouteAt(`/mfa/enroll?returnUrl=${encodeURIComponent(RETURN_URL)}`);
    await waitForSecret();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-enroll-done")).toBeInTheDocument();
    await user.click(page.getByTestId("mfa-enroll-done"));

    await vi.waitFor(() => {
      expect(nav.absolute()).not.toBeNull();
    });
    expect(nav.relative()).toBe(RETURN_URL);
  });

  it("threads the enrollToken from the query string into the exchange", async () => {
    // The settings-triggered flow: the Web app links here with `?enrollToken=…`.
    renderRouteAt(`/mfa/enroll?enrollToken=${ENROLL_TOKEN}`);

    await vi.waitFor(() => {
      expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
    });
    expect(new URL(callsTo(EXCHANGE_ENDPOINT)[0]?.url ?? "").searchParams.get("token")).toBe(
      ENROLL_TOKEN,
    );
  });

  it("renders a bare /mfa/enroll with no query string at all", async () => {
    // Both params are optional — a user sent here mid-login carries neither, and
    // `validateSearch` must not throw at them.
    renderRouteAt("/mfa/enroll");

    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(0);
  });
});
