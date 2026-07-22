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
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { Route as mfaEnrollRoute } from "../../../routes/mfa/enroll";
import { MfaEnrollForm } from "./MfaEnrollForm";

/**
 * Component spec for the MfaEnroll screen (Wallow-vec7.3.7).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `mfa-enroll-error`, `mfa-enroll-backup-codes`, `mfa-enroll-qr`,
 * `mfa-enroll-secret`, `mfa-enroll-code`, `mfa-enroll-submit`,
 * `mfa-enroll-begin-setup`, `mfa-enroll-cancel`.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly (that module is the only permitted importer
 * of the SDK). Per bd memories `vitest-resetmodules-breaks-instanceof-across-
 * graphs`, this file uses a plain `vi.mock` factory + `vi.hoisted` spies and
 * NEVER `vi.resetModules()`.
 *
 * ── THE RELAY IS GONE (this screen's whole reason to exist) ───────────────────
 *
 * The oracle is built around a cookie-smuggling hack. Enrollment is called during
 * PRERENDER (the only place `HttpContext` supplies the partial-auth cookie), and
 * the secret, the QR uri AND THE RAW COOKIE HEADER are stashed into
 * `PersistentComponentState` so the interactive circuit — where `HttpContext` is
 * null — can restore them and re-inject the cookie on the confirm call
 * (`PersistedEnrollment(Secret, QrUri, CookieHeader)`, `ApiCookieJar`,
 * `SeedFromBrowserCookies`).
 *
 * None of that is ported. wallow-auth's h3 server is a passthrough reverse proxy
 * and the client sends `credentials: "include"`, so the `Identity.MfaPartial`
 * cookie rides ordinary same-origin requests. The absence of the relay is pinned
 * as a behaviour, not left to inspection: `enrollTotp` is called with NO
 * arguments and `confirmEnrollment` receives ONLY `{ secret, code }` — no cookie
 * header threads through either seam, because there is nothing to thread.
 *
 * ── THE ERROR-BRANCH FINDING (read off the controller, not assumed) ───────────
 *
 * The oracle switches on `result.Error` after reading `result.Succeeded`:
 *
 *     "invalid_code" => "Invalid verification code. Please try again."
 *     _              => "Failed to confirm MFA enrollment. Please try again."
 *
 * That cannot be ported as written. `MfaController`
 * (api/.../Controllers/MfaController.cs:57-120) returns:
 *
 *   enroll/totp
 *     401 { succeeded: false, error: "no_auth_session" }   no full auth, no partial cookie
 *     200 { secret, qrUri }                                the ONLY success
 *
 *   enroll/confirm
 *     401 { succeeded: false, error: "no_auth_session" }   no full auth, no partial cookie
 *     400 { succeeded: false, error: "invalid_code" }      TOTP rejected
 *     400 { succeeded: false, error: "user_not_found" }    user vanished mid-flow
 *     400 { succeeded: false, error: "update_failed" }     persistence failed
 *     200 { succeeded: true, backupCodes }                 the ONLY success
 *
 * EVERY failure is non-2xx, so `unwrap()` THROWS and a `succeeded: false` body
 * NEVER ARRIVES AS DATA. The oracle's `if (result.Succeeded) … else` is therefore
 * unreachable through this seam — a resolved `confirmEnrollment` always means
 * success — and `toWallowError()` (packages/sdk/src/auth-client.ts:257-280)
 * builds `code` from `extensions.code` ?? `code` only, never a top-level `error`.
 * A bare anon body carries neither, so the screen receives
 * `WallowError{ code: "UNKNOWN", title: "Unknown error" }` and the reason string
 * is LOST (bd memory `wallow-auth-auth-client-ts-wallowerror-code-loss`).
 *
 * What survives is the status — and here, unlike MfaChallenge (whose 401 is
 * ambiguous between `invalid_code` and `no_mfa_session`), it is CLEAN:
 *
 *     confirm 400 -> invalid_code (or the two should-never-happen writes)
 *     confirm 401 -> unambiguously no_auth_session: the ONLY 401 either
 *                    endpoint can produce (`ResolveEnrollmentUserIdAsync`
 *                    returned null)
 *
 * So the ports are:
 *
 *     400           -> the oracle's `invalid_code` message. The dominant 400 by
 *                      far; `user_not_found`/`update_failed` are unreachable
 *                      absent a race, and "your code was wrong" is the right
 *                      guess when the user's next move is to retype it anyway.
 *     401           -> a session message, NOT "try again". This is a divergence
 *                      the status EARNS: the oracle's `_` tail tells a user whose
 *                      enrollment session is gone to "try again", which loops
 *                      them forever — no number of retries mints a cookie. 401
 *                      is unambiguous here, so the port says so.
 *     otherwise     -> the oracle's generic `_` tail.
 *
 * ── THE ORIGIN DIVERGENCE (inherited from Wallow-vec7.3.4) ────────────────────
 *
 * The oracle's `BuildApiReturnUrl` prepends an absolute API origin
 * (`Configuration["ApiBaseUrl"] ?? "http://localhost:5001"`) to the Done button's
 * target. That prepend is deliberately NOT ported: this origin hosts `/v1/**` and
 * `/connect/**` through the proxy, so the origin argument is `""` (bd memory
 * `wallow-auth-same-origin-baseurl-apps-wallow-auth`). Pinned by "hands off to
 * the return url on THIS origin".
 *
 * ── THE ORACLE WART THIS PORT KEEPS ──────────────────────────────────────────
 *
 * `OnInitializedAsync` calls `HandleStartEnroll()` unconditionally, so the intro
 * copy and its `mfa-enroll-begin-setup` button are NOT a first screen — enrollment
 * has already been fired by the time anything renders, and that branch
 * (`_secret` still null) is reachable only once enrollment has FAILED. The button
 * is a RETRY in all but name. That is the oracle's real behaviour and it is
 * ported as-is: an extra "click to begin" gate would be invention, and the
 * testid's existence in the oracle is not evidence of a happy-path step.
 */

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  enrollTotp: vi.fn(),
  confirmEnrollment: vi.fn(),
  exchangeEnrollmentToken: vi.fn(),
  isSafeReturnUrl: vi.fn(),
  navigate: vi.fn(),
  /** Records cross-spy ordering, which per-spy call counts cannot express. */
  calls: [] as string[],
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {
      enrollTotp: mocks.enrollTotp,
      confirmEnrollment: mocks.confirmEnrollment,
      exchangeEnrollmentToken: mocks.exchangeEnrollmentToken,
    },
    oidc: { isSafeReturnUrl: mocks.isSafeReturnUrl },
  }),
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

/**
 * The real `isSafeReturnUrl` rule (packages/sdk/src/auth-oidc.ts), mirrored
 * rather than imported: screens may not import the SDK, so the seam is mocked,
 * and a mock that returned a constant would let an unsafe-returnUrl test pass
 * for the wrong reason. Under 67 tests of its own in Wallow-vec7.2.2.
 */
function isSafeReturnUrlRule(url: string | null | undefined): boolean {
  if (url === null || url === undefined || url.trim() === "") {
    return false;
  }

  return url.startsWith("/") && !url.startsWith("//");
}

/**
 * What the facade really throws for these endpoints' failures — reason string
 * already lost at the seam (see the file header). `code: "UNKNOWN"` is not
 * incidental: it proves the port is not secretly relying on a code the seam
 * never delivers.
 */
function rejectionWithStatus(status: number): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status,
    code: "UNKNOWN",
    title: "Unknown error",
  });
}

/**
 * A rejection carrying the API's machine token, which is what the seam really
 * delivers as of Wallow-vec7.7: `toWallowError()`'s `readCode` now probes
 * `extensions.code > code > error`, so the `error` member of these endpoints'
 * bare `{ succeeded: false, error }` bodies reaches the screen as
 * `WallowError.code`. The `code: "UNKNOWN"` fixtures above are NOT obsolete —
 * they are what binds the HTTP-status fallback, which must survive because
 * `.code` is not a guaranteed-stable token (bd memory
 * `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-bind`).
 */
function rejectionWithCode(status: number, code: string): Error & { status: number; code: string } {
  return Object.assign(rejectionWithStatus(status), { code });
}

/** 400: invalid_code — the confirm failure a user can actually fix. */
function invalidCodeRejection(): Error & { status: number; code: string } {
  return rejectionWithStatus(400);
}

/** 401: no_auth_session — the only 401 either enrollment endpoint emits. */
function noSessionRejection(): Error & { status: number; code: string } {
  return rejectionWithStatus(401);
}

/**
 * NAVIGATION SEAM (Wallow-xzha.3.1). The screen hands off with a bare
 * `globalThis.location.href = returnUrl ?? "/"` — no URL-builder to mock — and in
 * a real browser `window.location` is `[Unforgeable]`: `vi.stubGlobal("location",
 * …)` cannot shadow it and redefining `location`/`location.href` throws ("Cannot
 * redefine property"). So the assignment is observed at the ONLY seam Chromium
 * leaves open: the Navigation API. The `navigate` event fires with the full
 * destination URL and is `cancelable`, so `preventDefault()` captures the target
 * without letting the iframe navigate (which would tear the runner down). The
 * jsdom stub held the raw relative string; here `destination.url` is absolute, so
 * `relative()` (`pathname + search`) reconstructs the this-origin form the old
 * assertion compared against — intent identical, no weakening.
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

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
}

function renderForm(props: { returnUrl?: string; enrollToken?: string } = {}) {
  return renderWithClient(<MfaEnrollForm {...props} />);
}

/** Wait for enrollment to land, i.e. for the confirm form to exist. */
async function waitForSecret(): Promise<void> {
  await expect.element(page.getByTestId("mfa-enroll-secret")).toBeInTheDocument();
}

/** Type the verification code and submit — the oracle's `HandleConfirm`. */
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
  mocks.calls.length = 0;
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
  mocks.enrollTotp.mockImplementation(() => {
    mocks.calls.push("enrollTotp");
    return Promise.resolve({ secret: SECRET, qrUri: QR_URI });
  });
  mocks.exchangeEnrollmentToken.mockImplementation(() => {
    mocks.calls.push("exchangeEnrollmentToken");
    return Promise.resolve({ succeeded: true });
  });
  mocks.confirmEnrollment.mockResolvedValue({ succeeded: true, backupCodes: BACKUP_CODES });
});

describe("MfaEnrollForm — starting enrollment", () => {
  it("asks the API for a secret on mount, passing nothing with it", async () => {
    // The oracle fires `HandleStartEnroll()` from `OnInitializedAsync` — there is
    // no "click to begin" gate on the happy path. The empty argument list is the
    // point: `enrollTotp()` takes no cookie header because the partial-auth
    // cookie rides the request itself now. The relay is gone.
    renderForm();

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
    });
    expect(mocks.enrollTotp).toHaveBeenCalledWith();
  });

  it("shows the QR code keyed to the otpauth uri the API returned", async () => {
    // Oracle: `JS.InvokeVoidAsync("qrcode.generate", "mfa-enroll-qr", _qrUri)`.
    // The port renders client-side instead of reaching through JS interop, but
    // the contract is the same — the QR must encode the uri the API minted, not
    // one the screen reassembled. `data-qr-uri` is the assertable seam for that;
    // asserting on rendered SVG/canvas pixels would pin a library choice this
    // spec has no opinion about.
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-qr")).toHaveAttribute("data-qr-uri", QR_URI);
  });

  it("shows the secret for manual entry when the camera is not an option", async () => {
    // Oracle: "Or enter this secret manually" — the fallback path for a user on
    // the same device as their authenticator.
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
    // Oracle: the `JSException` from `qrcode.generate` is swallowed — "QR display
    // is optional; secret text suffices for enrollment". A missing qrUri must
    // degrade to manual entry, never blank the form out.
    //
    // The positive half is load-bearing: "no QR element" is trivially true of a
    // page that rendered nothing.
    mocks.enrollTotp.mockResolvedValue({ secret: SECRET, qrUri: null });
    renderForm();
    await waitForSecret();

    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-qr").query()).toBeNull();
  });

  it("withholds the code form until the secret is in hand", async () => {
    // A code field with no secret behind it cannot be confirmed —
    // `confirmEnrollment` needs `{ secret, code }` and the oracle's `_secret!`
    // would be null. The trailing positive assertion is what keeps this honest:
    // an empty stub also has no code field.
    let release!: () => void;
    mocks.enrollTotp.mockReturnValue(
      new Promise((resolve) => {
        release = () => resolve({ secret: SECRET, qrUri: QR_URI });
      }),
    );
    renderForm();

    expect(page.getByTestId("mfa-enroll-code").query()).toBeNull();
    expect(page.getByTestId("mfa-enroll-secret").query()).toBeNull();

    release();
    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
  });

  it("shows no backup codes before the code is confirmed", async () => {
    // Backup codes are minted by `enroll/confirm`, not `enroll/totp`. Anchored
    // against the form that must be showing instead.
    renderForm();
    await waitForSecret();

    await expect.element(page.getByTestId("mfa-enroll-submit")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-backup-codes").query()).toBeNull();
  });

  it("offers a way out of enrollment", async () => {
    // Oracle: the footer `Cancel` link to "/". A user who opened this by mistake
    // must not be trapped in it.
    renderForm();
    await waitForSecret();

    await expect.element(page.getByTestId("mfa-enroll-cancel")).toHaveAttribute("href", "/");
  });
});

describe("MfaEnrollForm — the enrollment-token path", () => {
  it("exchanges the token for a session BEFORE asking for a secret", async () => {
    // Oracle: `if (!string.IsNullOrEmpty(EnrollToken)) await
    // ExchangeEnrollmentTokenAsync(EnrollToken); await HandleStartEnroll();`
    //
    // Order is the entire contract. The exchange is what mints the
    // `Identity.MfaPartial` cookie; `enroll/totp` fired first has no session to
    // resolve and 401s. Per-spy call counts cannot catch a reversed order, hence
    // the shared `calls` log.
    renderForm({ enrollToken: ENROLL_TOKEN });

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalled();
    });
    expect(mocks.calls).toEqual(["exchangeEnrollmentToken", "enrollTotp"]);
  });

  it("hands the token over verbatim", async () => {
    // The token is a data-protected blob with a 60-second lifetime
    // (`_enrollmentTokenLifetime`); any mangling fails `Unprotect`.
    renderForm({ enrollToken: ENROLL_TOKEN });

    await vi.waitFor(() => {
      expect(mocks.exchangeEnrollmentToken).toHaveBeenCalledWith(ENROLL_TOKEN);
    });
  });

  it("skips the exchange entirely on the ordinary sign-in flow", async () => {
    // No token means the user arrived mid-login and already holds a partial-auth
    // cookie. Anchored on the enrollment that MUST still happen.
    renderForm();

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
    });
    expect(mocks.exchangeEnrollmentToken).not.toHaveBeenCalled();
  });

  it("surfaces an error and does not enroll when the token is expired", async () => {
    // 400 { error: "invalid_or_expired_token" } — 60 seconds is easy to miss.
    // Calling `enroll/totp` anyway would just 401 and blame the wrong thing.
    mocks.exchangeEnrollmentToken.mockRejectedValue(rejectionWithStatus(400));
    renderForm({ enrollToken: ENROLL_TOKEN });

    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    expect(mocks.enrollTotp).not.toHaveBeenCalled();
  });
});

describe("MfaEnrollForm — when enrollment cannot start", () => {
  it("explains that setup could not begin", async () => {
    // Oracle: "Failed to start MFA enrollment. Please try again."
    mocks.enrollTotp.mockRejectedValue(rejectionWithStatus(500));
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/try again/iu);
  });

  it("says the session is gone rather than telling the user to retry", async () => {
    // `enroll/totp` has exactly one 401: `no_auth_session`. Retrying cannot mint
    // a cookie, so "try again" would loop the user forever — the divergence the
    // unambiguous status earns (see file header).
    mocks.enrollTotp.mockRejectedValue(noSessionRejection());
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("offers begin-setup as the way back", async () => {
    // The oracle's intro branch is reachable only once `_secret` is still null
    // after `HandleStartEnroll` — i.e. only after a failure. The button is a
    // retry (see file header).
    mocks.enrollTotp.mockRejectedValue(rejectionWithStatus(500));
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
  });

  it("retries enrollment on begin-setup, clearing the standing error", async () => {
    // Oracle: `HandleStartEnroll` opens with `_errorMessage = null`. A stale error
    // sitting above a freshly-minted QR code is a lie.
    mocks.enrollTotp.mockRejectedValueOnce(rejectionWithStatus(500));
    const user = userEvent.setup();
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
    await user.click(page.getByTestId("mfa-enroll-begin-setup"));

    await waitForSecret();
    expect(mocks.enrollTotp).toHaveBeenCalledTimes(2);
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();
  });
});

describe("MfaEnrollForm — confirming the code", () => {
  it("requires a code before calling the endpoint", async () => {
    // Oracle: `if (string.IsNullOrWhiteSpace(_code))` guards ahead of the call.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user, "");

    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .toHaveTextContent(/enter the verification code/iu);
    expect(mocks.confirmEnrollment).not.toHaveBeenCalled();
  });

  it("treats a whitespace-only code as blank", async () => {
    // `IsNullOrWhiteSpace`, not `IsNullOrEmpty` — spaces are not a code.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user, "   ");

    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .toHaveTextContent(/enter the verification code/iu);
    expect(mocks.confirmEnrollment).not.toHaveBeenCalled();
  });

  it("sends the enrolled secret with the typed code, and nothing else", async () => {
    // Oracle: `ConfirmEnrollmentAsync(_secret!, _code)`. The secret MUST be the
    // one `enroll/totp` just minted — the server re-validates the TOTP against
    // the secret in the body before storing it.
    //
    // The exact-object assertion is the relay's tombstone: the oracle had to
    // smuggle a cookie header alongside these two fields, and nothing here does.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.confirmEnrollment).toHaveBeenCalledWith({ secret: SECRET, code: CODE });
    });
  });

  it("disables submit while the confirm call is in flight", async () => {
    // Oracle: `Loading="_isSubmitting" Disabled="_isSubmitting"`. A double-submit
    // burns the TOTP window and mints backup codes twice.
    let release!: () => void;
    mocks.confirmEnrollment.mockReturnValue(
      new Promise((resolve) => {
        release = () => resolve({ succeeded: true, backupCodes: BACKUP_CODES });
      }),
    );
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-submit")).toBeDisabled();

    release();
    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
  });
});

describe("MfaEnrollForm — a confirmed code", () => {
  it("shows the backup codes", async () => {
    // Oracle: `_backupCodes = result.BackupCodes`, which swaps the whole card for
    // the success state. This is the only moment these codes are ever visible.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
  });

  it("shows every code the API returned", async () => {
    // A truncated list locks the user out later. All of them, or none of this
    // screen matters.
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
    // Oracle: `_backupCodes is not null` wins the render branch over `_secret`.
    // Leaving a live code box under the success state invites a second submit
    // that would regenerate the codes the user is mid-way through writing down.
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-code").query()).toBeNull();
    expect(page.getByTestId("mfa-enroll-submit").query()).toBeNull();
  });

  it("shows the success state even when the API returns no codes", async () => {
    // Oracle: `result.BackupCodes ?? Array.Empty<string>()` — an empty list is
    // still a successful enrollment, and MFA really is on. Falling back to the
    // error state here would tell the user a lie about their account.
    mocks.confirmEnrollment.mockResolvedValue({ succeeded: true });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-backup-codes")).toBeInTheDocument();
    expect(page.getByTestId("mfa-enroll-error").query()).toBeNull();
  });

  it("hands off to the return url on THIS origin, not an API origin", async () => {
    // Oracle: Done -> `BuildApiReturnUrl(Sanitize(ReturnUrl))`, which prepends
    // `ApiBaseUrl`. NOT ported (bd memory `wallow-auth-same-origin-baseurl-apps-
    // wallow-auth`): the proxy serves `/connect/**` from this origin, and going
    // cross-origin would drop the cookie `enroll/confirm` just upgraded to full
    // auth — the exact round-trip in this bead's acceptance.
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
    // The this-origin-relative target the jsdom stub used to hold verbatim.
    expect(nav.relative()).toBe(RETURN_URL);
    // No API origin prepended: the hand-off stays on THIS origin.
    expect(new URL(nav.absolute() as string).origin).toBe(globalThis.location.origin);
  });

  it("sends a user who arrived without a return url home", async () => {
    // Oracle: `Sanitize(null)` -> "/". A nullish returnUrl is a legitimate direct
    // enrollment, not an attack, and gets the "/" fallback (bd memory
    // `returnurl-guard-refuse-dont-sanitize` — only a PRESENT-but-unsafe value is
    // refused).
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
    // The oracle's `"invalid_code"` message, reached by status since the reason
    // string dies at the seam (see file header).
    mocks.confirmEnrollment.mockRejectedValue(invalidCodeRejection());
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .toHaveTextContent(/invalid verification code/iu);
  });

  it("says the session is gone on a 401 rather than blaming the code", async () => {
    // `no_auth_session` is the ONLY 401 `enroll/confirm` emits. Telling this user
    // their code was invalid sends them to retype a code that can never work.
    mocks.confirmEnrollment.mockRejectedValue(noSessionRejection());
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("falls back to the generic message on an unrecognised status", async () => {
    // The oracle's `_` tail: "Failed to confirm MFA enrollment. Please try again."
    mocks.confirmEnrollment.mockRejectedValue(rejectionWithStatus(500));
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
    // A network rejection carries no `.status`. Narrow STRUCTURALLY — a screen may
    // not `instanceof WallowError`, since it may not import the SDK.
    mocks.confirmEnrollment.mockRejectedValue(new Error("Failed to fetch"));
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/try again/iu);
  });

  it("never leaks a raw rejection or a machine reason token into the page", async () => {
    // The API's error tail can print `result.Error` raw, exposing the literal
    // "update_failed". That wart is not ported.
    mocks.confirmEnrollment.mockRejectedValue(rejectionWithStatus(400));
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
    // The TOTP window rolls every 30 seconds — the overwhelmingly common cause of
    // a rejected code is a stale one, and the next attempt succeeds.
    mocks.confirmEnrollment.mockRejectedValue(invalidCodeRejection());
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
    mocks.confirmEnrollment.mockRejectedValueOnce(invalidCodeRejection());
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.confirmEnrollment).toHaveBeenCalledTimes(2);
    });
    expect(mocks.confirmEnrollment).toHaveBeenLastCalledWith({ secret: SECRET, code: CODE });
    expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
  });

  it("clears the error once a later attempt succeeds", async () => {
    // Oracle: `HandleConfirm` opens with `_errorMessage = null`.
    mocks.confirmEnrollment.mockRejectedValueOnce(invalidCodeRejection());
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
 * The token-keyed half of the error mapping, which the status-only fixtures above
 * cannot reach. Added in the IMPLEMENT phase: the RED spec was written against a
 * seam that dropped the API's `error` member, but Wallow-vec7.7 landed
 * `readCode`'s `extensions.code > code > error` probe, so the token now survives.
 *
 * This is strictly ADDITIVE — every status-only test above still passes unchanged,
 * because an unrecognised `code` still falls through to the status rule. What the
 * token buys is the pair of 400s the status alone MISATTRIBUTES: `user_not_found`
 * and `update_failed` are server-side write failures, and the status fallback can
 * only guess "invalid_code" at them, telling a user whose account write failed to
 * retype a code that was already correct — the same infinite loop the port refuses
 * to send a `no_auth_session` user round.
 */
describe("MfaEnrollForm — the reason token the API sends", () => {
  it("blames the code when the API says invalid_code", async () => {
    mocks.confirmEnrollment.mockRejectedValue(rejectionWithCode(400, "invalid_code"));
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect
      .element(page.getByTestId("mfa-enroll-error"))
      .toHaveTextContent(/invalid verification code/iu);
  });

  it("does NOT blame the code when the write failed rather than the code", async () => {
    // `update_failed` is a 400, so status alone cannot tell it from `invalid_code`.
    // The user's code was fine; telling them to retype it is a loop they cannot
    // escape. THIS is what the token recovers.
    mocks.confirmEnrollment.mockRejectedValue(rejectionWithCode(400, "update_failed"));
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    const error = page.getByTestId("mfa-enroll-error");
    await expect.element(error).toHaveTextContent(/try again/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("does NOT blame the code when the user vanished mid-flow", async () => {
    // `user_not_found`, the other should-never-happen 400.
    mocks.confirmEnrollment.mockRejectedValue(rejectionWithCode(400, "user_not_found"));
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    const error = page.getByTestId("mfa-enroll-error");
    await expect.element(error).toHaveTextContent(/try again/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("names the session on no_auth_session even when no status rides along", async () => {
    // Keying on the TOKEN, not the status: this rejection carries no `.status` at
    // all, so a status-only port would fall to its generic tail.
    mocks.confirmEnrollment.mockRejectedValue(
      Object.assign(new Error("Unknown error"), { code: "no_auth_session" }),
    );
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("names the expired LINK, not the session, when the token exchange is refused", async () => {
    // `invalid_or_expired_token` — the user's fix is to start setup again from the
    // app that linked them here, which a generic "try again" would not tell them.
    mocks.exchangeEnrollmentToken.mockRejectedValue(
      rejectionWithCode(400, "invalid_or_expired_token"),
    );
    renderForm({ enrollToken: ENROLL_TOKEN });

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/expired/iu);
    expect(mocks.enrollTotp).not.toHaveBeenCalled();
  });

  it("still never renders the raw token, whatever the API sends", async () => {
    // The oracle's `_` tail prints `result.Error` raw. Now that the token actually
    // ARRIVES, this guard matters more than it did when everything was UNKNOWN.
    mocks.confirmEnrollment.mockRejectedValue(rejectionWithCode(400, "update_failed"));
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
    // REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`).
    // This DIVERGES from the oracle, which silently rewrites an unsafe returnUrl
    // to "/" and enrolls anyway, swallowing the attempt. Refusing on mount is the
    // ConsentScreen/MfaChallenge precedent: do not make a user set up a second
    // factor for a destination already decided against.
    renderForm({ returnUrl: "//evil.example.com/steal" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(mocks.enrollTotp).not.toHaveBeenCalled();
  });

  it("refuses an absolute return url", async () => {
    renderForm({ returnUrl: "https://evil.example.com/steal" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(mocks.enrollTotp).not.toHaveBeenCalled();
  });

  it("refuses an empty-string return url", async () => {
    // `""` is present-but-unsafe, not absent: `isSafeReturnUrl("")` is false, and
    // only a NULLISH value earns the "/" fallback.
    renderForm({ returnUrl: "" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith({ href: ERROR_HREF });
    });
    expect(mocks.enrollTotp).not.toHaveBeenCalled();
  });

  it("does not refuse a missing return url", async () => {
    // A direct/non-OIDC enrollment is legitimate and must proceed.
    renderForm();

    await vi.waitFor(() => {
      expect(mocks.enrollTotp).toHaveBeenCalled();
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
    // The oracle called `enroll/totp` during PRERENDER and then had to persist the
    // result specifically so the interactive circuit would NOT call it again —
    // `TryTakeFromJson<PersistedEnrollment>` exists only to suppress a second
    // call, which would mint a SECOND secret and invalidate the QR code the user
    // had already scanned. With no prerender/circuit split there is one render
    // pass and one call; this pins that a stray effect dependency has not
    // reintroduced the very bug the relay existed to paper over.
    renderForm();

    await waitForSecret();
    expect(mocks.enrollTotp).toHaveBeenCalledTimes(1);
  });

  it("exchanges the enrollment token exactly once per mount", async () => {
    // The token is single-purpose and 60-second-lived; a second exchange is a
    // wasted round trip at best.
    renderForm({ enrollToken: ENROLL_TOKEN });

    await waitForSecret();
    expect(mocks.exchangeEnrollmentToken).toHaveBeenCalledTimes(1);
  });
});

/**
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criteria under test — returnUrl and
 * enrollToken read off the query string — only exist once a URL is parsed by a
 * router. The root here is a throwaway: the app's real `__root.tsx` renders
 * `<html>`, and `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
 */
function renderRouteAt(url: string) {
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    mfaEnrollRoute.update({
      id: "/mfa/enroll",
      path: "/mfa/enroll",
      getParentRoute: () => rootRoute,
    }),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
}

describe("/mfa/enroll route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // Wallow-vec7.3.16 registered this path against a placeholder component; this
    // task's job is to replace it. The path is the contract and is not this
    // task's to change.
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
      expect(mocks.exchangeEnrollmentToken).toHaveBeenCalledWith(ENROLL_TOKEN);
    });
  });

  it("renders a bare /mfa/enroll with no query string at all", async () => {
    // Both params are optional: a user sent here mid-login carries neither.
    // `validateSearch` must not throw at them.
    renderRouteAt("/mfa/enroll");

    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    expect(mocks.exchangeEnrollmentToken).not.toHaveBeenCalled();
  });
});
