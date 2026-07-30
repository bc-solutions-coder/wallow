import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { type SdkCall, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { AUTH_HARNESS_ORIGIN, createAuthHarness } from "@shared/testing/harness";
import { Route as mfaEnrollRoute } from "@app/routes/mfa/enroll";
import { MfaEnrollForm } from "./MfaEnrollForm";

/**
 * Component spec for the MfaEnroll screen (Wallow-vec7.3.7).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `mfa-enroll-error`, `mfa-enroll-backup-codes`, `mfa-enroll-qr`,
 * `mfa-enroll-secret`, `mfa-enroll-code`, `mfa-enroll-submit`,
 * `mfa-enroll-begin-setup`, `mfa-enroll-cancel`.
 *
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness` (Wallow-pu6a.5.1). The
 * SDK is the REAL one and only its `fetch` is faked, so the screen's whole
 * pipeline — request-scoped SDK -> generated operation -> CSRF interceptor ->
 * serialization -> error shaping -> React Query — runs here. There is no
 * app-level facade left to stand in for (Wallow-pu6a.5.5), which changes what
 * these tests can SEE in two ways worth stating up front:
 *
 *   - The old spy assertions ("`confirmEnrollment` was called with
 *     `{ secret, code }`") are now assertions on the RECORDED HTTP REQUEST —
 *     `harness.last.path` / `.method` / `.body` / `.url`. That covers the
 *     endpoint URL and the serialization a spy skipped entirely, so it is
 *     strictly stronger. This app's SDK is rooted at the origin, so a recorded
 *     `path` is the bare endpoint path.
 *   - The `oidc.isSafeReturnUrl` stub is GONE, and with it the mirrored copy of
 *     its rule this file used to carry. The real function
 *     (`packages/sdk/src/auth-oidc.ts`, pure, 67 tests of its own from
 *     Wallow-vec7.2.2) now runs, so the open-redirect tests below exercise the
 *     guard the app actually ships rather than a re-implementation of it.
 *
 * `renderWithWallow` supplies the router context the screen reads its SDK off,
 * and `createAuthHarness()` pins the harness origin to this app's root-mounted
 * API surface.
 *
 * The `useNavigate` mock STAYS: navigation is a ROUTER seam, not an SDK one.
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
 * None of that is ported. wallow-auth's API surface is a passthrough reverse proxy
 * and the client sends `credentials: "include"`, so the `Identity.MfaPartial`
 * cookie rides ordinary same-origin requests. The absence of the relay is pinned
 * as a behaviour, not left to inspection: the recorded `enroll/totp` request has
 * NO body at all and the recorded `enroll/confirm` request's body is exactly
 * `{ secret, code }` — no cookie header threads through either, because there is
 * nothing to thread. Over the real transport that is now literally observable on
 * the wire rather than inferred from a spy's argument list.
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
 * Those bodies are what the fixtures below put ON THE WIRE, verbatim — the whole
 * point of the harness is that the spec no longer gets to invent the error object
 * the screen receives, so what the screen receives is now decided by the SDK's
 * own error pipeline (`wireWallowErrorInterceptor` -> `toWallowError` ->
 * `readCode`, which probes `extensions.code > code > error`; the bare `error`
 * member of these bodies is that third probe).
 *
 * EVERY failure is non-2xx, so a resolved `confirmEnrollment` ALWAYS means
 * success — the oracle's `if (result.Succeeded) … else` is unreachable through
 * this seam. What the screen narrows on is the pair the pipeline preserves:
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
 * A body of `{}` is the deliberately least-informative failure and is what binds
 * the STATUS fallback, which must survive because `.code` is not a
 * guaranteed-stable token (bd memory `code-keyed-error-mapping-needs-an-
 * unrecognised-code-test-to-bind`).
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

/** The three endpoints this screen touches (packages/sdk/src/generated/sdk.gen.ts). */
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
 * A failure carrying NO reason at all, only the status. Binds the status
 * fallback: the port must not be secretly relying on a code that a future API
 * revision might stop sending.
 */
function failWithStatus(status: number): EndpointResponder {
  return () => Response.json({}, { status });
}

/**
 * A failure in the shape `MfaController` really writes: a bare anon body whose
 * `error` member is the machine token. `readCode`'s third probe lifts it onto
 * `WallowError.code`.
 */
function failWithCode(status: number, code: string): EndpointResponder {
  return () => Response.json({ succeeded: false, error: code }, { status });
}

/** Answer `first` once, then `rest` forever — the old `mockRejectedValueOnce`. */
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
 * Program the wire per ENDPOINT rather than per facade method: one transport
 * serves all three calls, so responses are dispatched on the recorded path.
 * Anything unlisted answers the happy body.
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

let harness: SdkHarness;

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
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
  harness = createAuthHarness();
  program();
});

describe("MfaEnrollForm — starting enrollment", () => {
  it("asks the API for a secret on mount, passing nothing with it", async () => {
    // The oracle fires `HandleStartEnroll()` from `OnInitializedAsync` — there is
    // no "click to begin" gate on the happy path. The empty request is the point:
    // `enroll/totp` carries no cookie header because the partial-auth cookie
    // rides the request itself now. The relay is gone.
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
    program({ totp: () => Response.json({ secret: SECRET, qrUri: null }, { status: OK }) });
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
    program({
      totp: async () =>
        await new Promise<Response>((resolve) => {
          release = () => {
            resolve(okTotp() as Response);
          };
        }),
    });
    renderForm();

    // Wait for the request to REACH the transport before releasing it: the
    // responder is what hands `release` back, so releasing earlier would call an
    // unassigned binding.
    await vi.waitFor(() => {
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });
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
    // resolve and 401s. Per-endpoint call counts cannot catch a reversed order —
    // the recorded request LOG can, and it is the wire order rather than the
    // facade-call order, which is the thing that actually matters.
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
    // The token is a data-protected blob with a 60-second lifetime
    // (`_enrollmentTokenLifetime`); any mangling fails `Unprotect`. The token
    // rides the QUERY STRING, so it is read off `url`, not `path`.
    renderForm({ enrollToken: ENROLL_TOKEN });

    await vi.waitFor(() => {
      expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
    });
    const exchange: SdkCall | undefined = callsTo(EXCHANGE_ENDPOINT)[0];
    expect(new URL(exchange?.url ?? "").searchParams.get("token")).toBe(ENROLL_TOKEN);
  });

  it("skips the exchange entirely on the ordinary sign-in flow", async () => {
    // No token means the user arrived mid-login and already holds a partial-auth
    // cookie. Anchored on the enrollment that MUST still happen.
    renderForm();

    await vi.waitFor(() => {
      expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
    });
    expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(0);
  });

  it("surfaces an error and does not enroll when the token is expired", async () => {
    // 400 { error: "invalid_or_expired_token" } — 60 seconds is easy to miss.
    // Calling `enroll/totp` anyway would just 401 and blame the wrong thing.
    program({ exchange: failWithStatus(BAD_REQUEST) });
    renderForm({ enrollToken: ENROLL_TOKEN });

    await expect.element(page.getByTestId("mfa-enroll-error")).toBeInTheDocument();
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(0);
  });
});

describe("MfaEnrollForm — when enrollment cannot start", () => {
  it("explains that setup could not begin", async () => {
    // Oracle: "Failed to start MFA enrollment. Please try again."
    program({ totp: failWithStatus(SERVER_ERROR) });
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/try again/iu);
  });

  it("says the session is gone rather than telling the user to retry", async () => {
    // `enroll/totp` has exactly one 401: `no_auth_session`. Retrying cannot mint
    // a cookie, so "try again" would loop the user forever — the divergence the
    // unambiguous status earns (see file header).
    program({ totp: failWithCode(UNAUTHORIZED, "no_auth_session") });
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("offers begin-setup as the way back", async () => {
    // The oracle's intro branch is reachable only once `_secret` is still null
    // after `HandleStartEnroll` — i.e. only after a failure. The button is a
    // retry (see file header).
    program({ totp: failWithStatus(SERVER_ERROR) });
    renderForm();

    await expect.element(page.getByTestId("mfa-enroll-begin-setup")).toBeInTheDocument();
  });

  it("retries enrollment on begin-setup, clearing the standing error", async () => {
    // Oracle: `HandleStartEnroll` opens with `_errorMessage = null`. A stale error
    // sitting above a freshly-minted QR code is a lie.
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
    // Oracle: `if (string.IsNullOrWhiteSpace(_code))` guards ahead of the call.
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
    // `IsNullOrWhiteSpace`, not `IsNullOrEmpty` — spaces are not a code.
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
    // Oracle: `ConfirmEnrollmentAsync(_secret!, _code)`. The secret MUST be the
    // one `enroll/totp` just minted — the server re-validates the TOTP against
    // the secret in the body before storing it.
    //
    // The exact-object assertion is the relay's tombstone: the oracle had to
    // smuggle a cookie header alongside these two fields, and the request that
    // really goes out carries those two fields and nothing else.
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
    // Oracle: `Loading="_isSubmitting" Disabled="_isSubmitting"`. A double-submit
    // burns the TOTP window and mints backup codes twice.
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

    // The button goes disabled a tick BEFORE `fetch` is reached, so wait for the
    // request to land before asserting and releasing.
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
    program({ confirm: () => Response.json({ succeeded: true }, { status: OK }) });
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
    // The oracle's `"invalid_code"` message, reached by STATUS: this failure
    // carries no reason token at all, which is what binds the fallback.
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
    // `no_auth_session` is the ONLY 401 `enroll/confirm` emits. Telling this user
    // their code was invalid sends them to retype a code that can never work.
    program({ confirm: failWithStatus(UNAUTHORIZED) });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("falls back to the generic message on an unrecognised status", async () => {
    // The oracle's `_` tail: "Failed to confirm MFA enrollment. Please try again."
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
    // A network-level fault: the transport throws before a response exists, so
    // there is no status anywhere. Narrow STRUCTURALLY — a screen may not
    // `instanceof WallowError`, since it may not import the SDK.
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
    // The API's error tail can print `result.Error` raw, exposing the literal
    // "update_failed". That wart is not ported.
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
    // The TOTP window rolls every 30 seconds — the overwhelmingly common cause of
    // a rejected code is a stale one, and the next attempt succeeds.
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
    // The SECOND attempt still carries the FIRST secret — no re-enrollment
    // happened behind the user's back, and `enroll/totp` was hit exactly once.
    expect(callsTo(CONFIRM_ENDPOINT)[1]?.body).toEqual({ secret: SECRET, code: CODE });
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
  });

  it("clears the error once a later attempt succeeds", async () => {
    // Oracle: `HandleConfirm` opens with `_errorMessage = null`.
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
 *
 * Over the harness these are no longer hand-built error objects: each test puts
 * the controller's real `{ succeeded: false, error }` body on the wire and lets
 * the SDK's `readCode` lift the token, so the probe order itself is under test.
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
    // `update_failed` is a 400, so status alone cannot tell it from `invalid_code`.
    // The user's code was fine; telling them to retype it is a loop they cannot
    // escape. THIS is what the token recovers.
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
    // `user_not_found`, the other should-never-happen 400.
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
    // Keying on the TOKEN, not the status. Over a real transport EVERY response
    // has a status, so "no status rides along" is not expressible verbatim; the
    // equivalent — and strictly stronger — statement is a `no_auth_session` token
    // arriving under a status whose fallback says something ELSE. A 400 would map
    // to "invalid verification code" on status alone, so only the token can
    // produce the session message here.
    program({ confirm: failWithCode(BAD_REQUEST, "no_auth_session") });
    const user = userEvent.setup();
    renderForm();
    await waitForSecret();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/sign in/iu);
  });

  it("names the expired LINK, not the session, when the token exchange is refused", async () => {
    // `invalid_or_expired_token` — the user's fix is to start setup again from the
    // app that linked them here, which a generic "try again" would not tell them.
    program({ exchange: failWithCode(BAD_REQUEST, "invalid_or_expired_token") });
    renderForm({ enrollToken: ENROLL_TOKEN });

    await expect.element(page.getByTestId("mfa-enroll-error")).toHaveTextContent(/expired/iu);
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(0);
  });

  it("still never renders the raw token, whatever the API sends", async () => {
    // The oracle's `_` tail prints `result.Error` raw. Now that the token actually
    // ARRIVES, this guard matters more than it did when everything was UNKNOWN.
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
    // REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`).
    // This DIVERGES from the oracle, which silently rewrites an unsafe returnUrl
    // to "/" and enrolls anyway, swallowing the attempt. Refusing on mount is the
    // ConsentScreen/MfaChallenge precedent: do not make a user set up a second
    // factor for a destination already decided against.
    //
    // The guard is the SDK's REAL `isSafeReturnUrl` now, not a mirror of its
    // rule — these cases exercise the shipped function.
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
    // `""` is present-but-unsafe, not absent: `isSafeReturnUrl("")` is false, and
    // only a NULLISH value earns the "/" fallback.
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
    // The oracle called `enroll/totp` during PRERENDER and then had to persist the
    // result specifically so the interactive circuit would NOT call it again —
    // `TryTakeFromJson<PersistedEnrollment>` exists only to suppress a second
    // call, which would mint a SECOND secret and invalidate the QR code the user
    // had already scanned. With no prerender/circuit split there is one render
    // pass and one call; this pins that a stray effect dependency has not
    // reintroduced the very bug the relay existed to paper over.
    renderForm();

    await waitForSecret();
    expect(callsTo(TOTP_ENDPOINT)).toHaveLength(1);
  });

  it("exchanges the enrollment token exactly once per mount", async () => {
    // The token is single-purpose and 60-second-lived; a second exchange is a
    // wasted round trip at best.
    renderForm({ enrollToken: ENROLL_TOKEN });

    await waitForSecret();
    expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
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
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/mfa/enroll", route: mfaEnrollRoute }],
  });
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
      expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(1);
    });
    expect(new URL(callsTo(EXCHANGE_ENDPOINT)[0]?.url ?? "").searchParams.get("token")).toBe(
      ENROLL_TOKEN,
    );
  });

  it("renders a bare /mfa/enroll with no query string at all", async () => {
    // Both params are optional: a user sent here mid-login carries neither.
    // `validateSearch` must not throw at them.
    renderRouteAt("/mfa/enroll");

    await expect.element(page.getByTestId("mfa-enroll-code")).toBeInTheDocument();
    expect(callsTo(EXCHANGE_ENDPOINT)).toHaveLength(0);
  });
});
