import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createPassthroughHarness,
  type SdkCall,
  type SdkHarness,
  type SdkResponder,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { Route as mfaChallengeRoute } from "@app/routes/mfa/challenge";
import { MfaChallengeForm, type MfaChallengeFormProps } from "./MfaChallengeForm";

/**
 * MFA challenge screen + its route.
 *
 * Real SDK over a faked fetch (sdk-harness): assertions read the recorded request, and every
 * failure is a non-2xx carrying a bare `{ succeeded, error }` — not problem details — whose
 * `error` member becomes the screen's `code`. Known codes are matched, never rendered; an
 * unrecognised one falls to the generic message rather than guessing.
 *
 * `window.location` is [Unforgeable] here, so a hand-off is captured by CANCELLING the
 * Navigation API's `navigate` event — without the cancel it unloads the Chromium runner.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy. Only the router's
// `useNavigate` is mocked; it is the seam for the screen's in-app bail to /error.
const mocks = vi.hoisted(() => ({
  navigate: vi.fn(),
}));

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const CODE = "123456";
const BACKUP_CODE = "abcd-efgh-ijkl";
const TICKET = "sign-in-ticket-xyz";
const RETURN_URL = "/connect/authorize?client_id=web";

/** The one endpoint behind BOTH `verifyMfa` and `useBackupCode`. */
const VERIFY_PATH = "/v1/identity/auth/mfa/verify";

/** The server-authoritative allow-list probe. */
const VALIDATE_PATH = "/v1/identity/auth/redirect-uri/validate";

/**
 * The returnUrl the EXTERNAL-LOGIN hand-off sends: ABSOLUTE and already allow-listed by the
 * API before it redirected here. `isSafeReturnUrl` is false for every absolute URL, so this
 * shape is settled by the server probe rather than locally.
 */
const EXTERNAL_RETURN_URL = "http://localhost:5002/login";

/** An absolute returnUrl from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

/** The client that started the flow. */
const CLIENT_ID = "client-a";

/** A SECOND registered client — the one this flow does NOT belong to. */
const OTHER_CLIENT_ID = "client-b";

/**
 * An absolute returnUrl registered by `client-b` and by nobody else: allowed when `client-b`
 * is asking, refused when `client-a` is, which is the whole point of scoping the probe.
 */
const OTHER_CLIENT_RETURN_URL = "https://b.example.com/callback";

/** The `AuthUrl` origin, which the server admits for every client. */
const AUTH_URL_ORIGIN = "http://localhost:5002";

/**
 * What each client has REGISTERED, which is what the server consults once it is given a
 * client id.
 *
 * A Map rather than a Record because the lookup key is attacker-supplied query cargo: a
 * Record would answer `"constructor"` with an inherited value.
 */
const CLIENT_REGISTERED_ORIGINS = new Map<string, readonly string[]>([
  [CLIENT_ID, ["https://app.example.com"]],
  [OTHER_CLIENT_ID, ["https://b.example.com"]],
]);

/**
 * The origins admitted with NO client id: the UNION of every registered client's, plus
 * `AuthUrl`. That union is why an unscoped probe is a hole — it answers "yes" for a URI any
 * client at all registered, whoever is asking.
 */
const ALLOWED_ORIGINS = new Set([
  AUTH_URL_ORIGIN,
  ...[...CLIENT_REGISTERED_ORIGINS.values()].flat(),
]);

/** The bail target for an unsafe returnUrl, matching the ConsentScreen port. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/**
 * The server's allow-list rule, mirrored rather than hard-coded: a fake answering a constant
 * would let the evil-origin test pass for the wrong reason.
 *
 * Given a client id the set is THAT client's registered origins plus AuthUrl; given none, the
 * union over every client. An UNKNOWN id resolves to no application and leaves only AuthUrl —
 * fail-closed, not an error.
 */
function allowedOriginsFor(clientId: string | undefined): ReadonlySet<string> {
  if (clientId === undefined) {
    return ALLOWED_ORIGINS;
  }

  return new Set([AUTH_URL_ORIGIN, ...(CLIENT_REGISTERED_ORIGINS.get(clientId) ?? [])]);
}

function isAllowedByServer(uri: string, clientId: string | undefined): boolean {
  let parsed: URL;
  try {
    parsed = new URL(uri);
  } catch {
    // Not absolute — the server's parse gate fails it and the endpoint says no.
    return false;
  }

  return allowedOriginsFor(clientId).has(parsed.origin);
}

/**
 * The allow-list endpoint, answering off the QUERY STRING the screen built. Reading
 * `uri`/`clientId` back out of `call.url` is what makes the scoping assertions real: an
 * implementation that computed the right client id but never put it on the wire is caught.
 */
const allowListResponder: SdkResponder = (call: SdkCall): Response => {
  const params: URLSearchParams = new URL(call.url).searchParams;
  const clientId: string | null = params.get("clientId");

  return Response.json({
    allowed: isAllowedByServer(params.get("uri") ?? "", clientId ?? undefined),
  });
};

/** The endpoint's ONLY success: `200 { succeeded: true, signInTicket }`. */
function verifiedResponse(ticket?: string): Response {
  return Response.json(
    ticket === undefined ? { succeeded: true } : { succeeded: true, signInTicket: ticket },
  );
}

function rejectionResponse(status: number, error: string): Response {
  return Response.json({ succeeded: false, error }, { status });
}

/** 401 + `invalid_code`: the code was wrong. Two of the endpoint's three 401s. */
function invalidCodeResponse(): Response {
  return rejectionResponse(401, "invalid_code");
}

/** 401 + `no_mfa_session`: the partial-auth cookie is missing or expired. */
function noMfaSessionResponse(): Response {
  return rejectionResponse(401, "no_mfa_session");
}

/** 423 + `mfa_locked_out` — the one failure status also identifies on its own. */
function lockedOutResponse(): Response {
  return rejectionResponse(423, "mfa_locked_out");
}

/**
 * The exact URL the SDK's exchange-ticket builder produces, spelled out rather than imported:
 * importing the builder to build the expectation would assert it against itself.
 *
 * ROOT-RELATIVE, which IS the same-origin claim — going cross-origin would drop the SameSite
 * partial-auth cookie that `mfa/verify` reads and the exchange endpoint upgrades.
 */
function exchangeUrl(ticket: string, returnUrl: string, clientId?: string): string {
  const base: string =
    `/v1/identity/auth/exchange-ticket` +
    `?ticket=${encodeURIComponent(ticket)}` +
    `&returnUrl=${encodeURIComponent(returnUrl)}`;

  return clientId === undefined ? base : `${base}&clientId=${encodeURIComponent(clientId)}`;
}

/**
 * The Navigation API's `destination.url` is always ABSOLUTE, so a relative expectation has to
 * be resolved against the page the runner is on before it can be compared.
 */
function absolute(url: string): string {
  return new URL(url, globalThis.location.href).href;
}

/**
 * The minimum of the Navigation API this spec uses. Declared structurally because
 * `globalThis.navigation` is not in the DOM lib this repo compiles against, and a cast would
 * be an `as any` in all but name.
 */
interface NavigateEventLike {
  readonly destination: { readonly url: string };
  readonly cancelable: boolean;
  preventDefault: () => void;
}

interface NavigationApi {
  addEventListener: (type: "navigate", listener: (event: NavigateEventLike) => void) => void;
  removeEventListener: (type: "navigate", listener: (event: NavigateEventLike) => void) => void;
}

function navigationApi(): NavigationApi | undefined {
  return (globalThis as { navigation?: NavigationApi }).navigation;
}

/** Every URL the screen tried to navigate to, in order. Reset per test. */
let navigations: string[] = [];

const recordNavigation = (event: NavigateEventLike): void => {
  navigations.push(event.destination.url);
  if (event.cancelable) {
    event.preventDefault();
  }
};

let harness: SdkHarness;

/** How `POST /v1/identity/auth/mfa/verify` answers. Reassigned per test. */
let verifyWith: SdkResponder;

/** How `GET /v1/identity/auth/redirect-uri/validate` answers. Reassigned per test. */
let validateWith: SdkResponder;

function verifyCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === VERIFY_PATH);
}

function validateCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === VALIDATE_PATH);
}

/** Query string of the first allow-list probe, or an empty set if it never happened. */
function probeQuery(): URLSearchParams {
  const url: string | undefined = validateCalls().at(0)?.url;
  return url === undefined ? new URLSearchParams() : new URL(url).searchParams;
}

function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/** Render the screen as an OIDC MFA hand-off would: a safe returnUrl present. */
function renderForm(props: Partial<MfaChallengeFormProps> = {}) {
  return renderWithClient(<MfaChallengeForm returnUrl={RETURN_URL} {...props} />);
}

async function toggleToBackupCode(user: ReturnType<typeof userEvent.setup>) {
  await user.click(page.getByTestId("mfa-challenge-toggle-backup"));
}

/** Type into whichever of the two mutually-exclusive fields is showing, then submit. */
async function submitCode(user: ReturnType<typeof userEvent.setup>, code: string = CODE) {
  if (code !== "") {
    const field =
      page.getByTestId("mfa-challenge-code").query() !== null
        ? page.getByTestId("mfa-challenge-code")
        : page.getByTestId("mfa-challenge-backup-code");
    await user.type(field, code);
  }
  await user.click(page.getByTestId("mfa-challenge-submit"));
}

beforeEach(() => {
  vi.clearAllMocks();
  navigations = [];
  navigationApi()?.addEventListener("navigate", recordNavigation);

  harness = createPassthroughHarness();
  verifyWith = () => verifiedResponse(TICKET);
  validateWith = allowListResponder;
  // ONE dispatcher, installed once: the tests reprogram the two endpoint responders above
  // rather than re-installing a whole transport each time.
  harness.respond((call: SdkCall) => {
    if (call.path === VERIFY_PATH) {
      return verifyWith(call);
    }
    if (call.path === VALIDATE_PATH) {
      return validateWith(call);
    }
    return Response.json({});
  });
});

afterEach(() => {
  navigationApi()?.removeEventListener("navigate", recordNavigation);
});

describe("MfaChallengeForm", () => {
  it("renders the authenticator-code field, and no error or success before submit", async () => {
    await renderForm();

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-challenge-submit")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-challenge-toggle-backup")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-error").query()).toBeNull();
    expect(page.getByTestId("mfa-challenge-success").query()).toBeNull();
  });

  it("shows only the authenticator field until the user asks for backup entry", async () => {
    // The positive half is load-bearing: "the backup field is absent" is trivially true of a
    // page that rendered nothing, so it needs anchoring to the field that MUST be there.
    await renderForm();

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-backup-code").query()).toBeNull();
  });

  it("links back to sign in", async () => {
    // The card footer link carries no testid, so it is asserted by role + href.
    await renderForm();

    await expect
      .element(page.getByRole("link", { name: /back to sign in/iu }))
      .toHaveAttribute("href", "/login");
  });
});

describe("MfaChallengeForm — the backup-code toggle", () => {
  it("swaps the authenticator field for the backup-code field", async () => {
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);

    await expect.element(page.getByTestId("mfa-challenge-backup-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
  });

  it("toggles back to the authenticator field", async () => {
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);
    await user.click(page.getByTestId("mfa-challenge-toggle-backup"));

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-backup-code").query()).toBeNull();
  });

  it("offers the other mode in its label each way round", async () => {
    // The toggle names the DESTINATION, not the current state: a toggle labelled with the
    // mode you are already in is a coin flip for the user.
    const user = userEvent.setup();
    await renderForm();

    await expect
      .element(page.getByTestId("mfa-challenge-toggle-backup"))
      .toHaveTextContent(/use backup code instead/iu);

    await toggleToBackupCode(user);

    await expect
      .element(page.getByTestId("mfa-challenge-toggle-backup"))
      .toHaveTextContent(/use authenticator code instead/iu);
  });

  it("describes the mode the user is in", async () => {
    // Asserted against the description SENTENCE, not a bare /backup code/ substring: the
    // field label is "Backup code", so a substring match could never tell the two apart.
    const user = userEvent.setup();
    await renderForm();

    await expect
      .element(page.getByText(/enter the code from your authenticator app/iu))
      .toBeInTheDocument();

    await toggleToBackupCode(user);

    await expect.element(page.getByText(/enter one of your backup codes/iu)).toBeInTheDocument();
  });

  it("discards a code typed in the other mode", async () => {
    // A TOTP code left sitting in the backup-code box would be submitted to the wrong branch
    // and burn one of the user's five attempts before the lockout.
    const user = userEvent.setup();
    await renderForm();

    await user.type(page.getByTestId("mfa-challenge-code"), CODE);
    await toggleToBackupCode(user);

    await expect.element(page.getByTestId("mfa-challenge-backup-code")).toHaveValue("");
  });

  it("clears a standing error", async () => {
    // "Invalid verification code" hanging over a freshly-opened backup-code box is a lie.
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();

    await toggleToBackupCode(user);

    expect(page.getByTestId("mfa-challenge-error").query()).toBeNull();
  });
});

describe("MfaChallengeForm — submitting a code", () => {
  it("requires a code before calling the endpoint", async () => {
    // A blank submit must not reach `mfa/verify`: it cannot succeed, and it costs one of the
    // five attempts before the lockout.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user, "");

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/enter the verification code/iu);
    expect(verifyCalls()).toHaveLength(0);
  });

  it("asks for a backup code by name when the backup field is blank", async () => {
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);
    await submitCode(user, "");

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/enter a backup code/iu);
    expect(verifyCalls()).toHaveLength(0);
  });

  it("treats a whitespace-only code as blank", async () => {
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user, "   ");

    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("sends the typed code to the authenticator endpoint", async () => {
    // `useBackupCode: false` IS the "went to the authenticator validator" claim: one endpoint
    // serves both modes and only the body flag tells them apart.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(VERIFY_PATH);
    });
    expect(harness.last?.method).toBe("POST");
    expect(harness.last?.body).toEqual({ code: CODE, useBackupCode: false });
  });

  it("sends a backup code to the backup endpoint instead", async () => {
    // Crossing the flag would send a recovery code to the TOTP validator.
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);
    await submitCode(user, BACKUP_CODE);

    await vi.waitFor(() => {
      expect(harness.last?.path).toBe(VERIFY_PATH);
    });
    expect(harness.last?.body).toEqual({ code: BACKUP_CODE, useBackupCode: true });
  });

  it("disables submit while the request is in flight", async () => {
    // One click, one attempt: this screen is rate-limited into a 5-strike lockout, so a
    // double submit can cost the user two of their five.
    let release: () => void = () => {};
    verifyWith = async () =>
      await new Promise<Response>((resolve) => {
        release = () => {
          resolve(verifiedResponse(TICKET));
        };
      });
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    // Wait for the request to REACH the transport before releasing it: the button goes
    // disabled a tick or two before `fetch` is called, and releasing into that gap would
    // leave the never-settling responder installed forever.
    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });
    await expect.element(page.getByTestId("mfa-challenge-submit")).toBeDisabled();
    await expect.element(page.getByTestId("mfa-challenge-submit")).toHaveTextContent(/verifying/iu);

    release();
    await expect.element(page.getByTestId("mfa-challenge-success")).toBeInTheDocument();
  });
});

describe("MfaChallengeForm — a verified code", () => {
  it("shows the success state", async () => {
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-success")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-error").query()).toBeNull();
  });

  it("hands the ticket to the exchange endpoint on THIS origin, not an API origin", async () => {
    // THE LOAD-BEARING ASSERTION OF THIS SCREEN. `exchangeUrl` is root-relative, so
    // `absolute()` resolves it against the PAGE's origin: an implementation that prepended
    // an API origin produces a different string and drops the SameSite partial-auth cookie
    // the whole round-trip depends on.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL))]);
    });
  });

  it("navigates straight to the return url when the response carries no ticket", async () => {
    // The exact-match assertion is also what says the exchange builder was NOT used: an
    // exchange URL is a different destination, and there is only one.
    verifyWith = () => verifiedResponse();
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(RETURN_URL)]);
    });
  });

  it("treats a blank ticket as no ticket", async () => {
    // The REAL builder THROWS on a blank ticket, so a screen that called it anyway would
    // replace the user's redirect with a crash and navigate nowhere.
    verifyWith = () => verifiedResponse("");
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(RETURN_URL)]);
    });
  });

  it("stays put on a direct sign-in with no return url", async () => {
    // No returnUrl means a direct login, not OIDC: a nullish value is not hostile, and on
    // this screen it earns no "/" fallback either.
    const user = userEvent.setup();
    await renderForm({ returnUrl: undefined });

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-success")).toBeInTheDocument();
    expect(navigations).toEqual([]);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("verifies a backup code through the same redirect path", async () => {
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);
    await submitCode(user, BACKUP_CODE);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL))]);
    });
  });
});

describe("MfaChallengeForm — the open-redirect guard", () => {
  it("refuses an unsafe return url instead of sanitizing it", async () => {
    // REFUSE to /error, do not fall back to "/" — silently rewriting an unsafe returnUrl
    // swallows the open-redirect attempt. Refused on MOUNT: making a user produce a second
    // factor for a destination already decided against wastes a one-time code.
    await renderForm({ returnUrl: "//evil.example.com/steal" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("refuses an absolute return url the allow-list does not know", async () => {
    // Absolute, so `isSafeReturnUrl` cannot answer and the SERVER's allow-list is asked.
    await renderForm({ returnUrl: EVIL_RETURN_URL });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("lets the external-login hand-off through on an allow-listed absolute return url", async () => {
    // Every external-login MFA user arrives with an ABSOLUTE returnUrl, and `isSafeReturnUrl`
    // is false for all of them — so a mount guard that stopped at the local check would bounce
    // 100% of them to /error. The API admitted this exact value before redirecting here, so
    // the allow-list the screen asks is the same one that let it in.
    const user = userEvent.setup();
    await renderForm({ returnUrl: EXTERNAL_RETURN_URL });

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);

    // Anchored on a POSITIVE assertion: the user reaches the exchange, not merely "was not
    // sent to /error", which a screen that renders nothing satisfies.
    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, EXTERNAL_RETURN_URL))]);
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
  });

  it("decides a relative return url locally, without asking the server", async () => {
    // The password path threads a RELATIVE returnUrl, which `isSafeReturnUrl` settles with no
    // network. The probe is the external-login path's cost alone; spending it on every login
    // would put an outbound request between the user and their code field.
    await renderForm();

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(validateCalls()).toHaveLength(0);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("refuses an empty-string return url without asking the server", async () => {
    // `?returnUrl=` is a PRESENT value, so it is the unsafe case and not the nullish
    // no-redirect one. A malformed link is not a destination worth asking the server about.
    await renderForm({ returnUrl: "" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(validateCalls()).toHaveLength(0);
  });

  it("does not render the form while the allow-list check is in flight", async () => {
    // FAIL CLOSED IN FLIGHT. A form rendered optimistically is a form a fast user can submit,
    // burning a one-time second factor on a destination we may be about to refuse.
    validateWith = () => new Promise<Response>(() => {});
    await renderForm({ returnUrl: EXTERNAL_RETURN_URL });

    await vi.waitFor(() => {
      expect(validateCalls()).toHaveLength(1);
    });
    expect(probeQuery().get("uri")).toBe(EXTERNAL_RETURN_URL);
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
    expect(page.getByTestId("mfa-challenge-submit").query()).toBeNull();
  });

  it("refuses when the allow-list check is unreachable", async () => {
    // An unreachable validator must never become a reason to TRUST a URI.
    validateWith = () => {
      throw new TypeError("network down");
    };
    await renderForm({ returnUrl: EXTERNAL_RETURN_URL });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
  });

  it("refuses a body that is not literally allowed:true", async () => {
    // The `{ allowed }` narrowing is STRICT: the STRING "true" is truthy in JS and must NOT
    // pass, or a screen leaning on truthiness would redirect on `allowed: "false"` too.
    for (const body of [{ allowed: false }, { allowed: "true" }, {}, "allowed", null]) {
      mocks.navigate.mockClear();
      validateWith = () => Response.json(body);
      const { unmount } = await renderForm({ returnUrl: EXTERNAL_RETURN_URL });

      await vi.waitFor(() => {
        expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
      });
      expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
      await unmount();
    }
  });

  it("does not refuse a direct sign-in with no return url at all", async () => {
    // The nullish case is the ordinary non-OIDC path — routing it to /error would break
    // every direct login.
    await renderForm({ returnUrl: undefined });

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});

/**
 * The flow's client id scopes BOTH things this screen does with `returnUrl`: the allow-list
 * probe, and the exchange-ticket hand-off (whose endpoint re-checks the returnUrl before
 * setting the cookie). Unscoped, each falls back to the union of every client's origins.
 *
 * SPELLINGS, both deliberate: the id arrives on the query string as `client_id` (the OIDC
 * spelling the API redirects with) and leaves as `clientId` (the name the endpoint binds).
 */
describe("MfaChallengeForm — the flow's client id", () => {
  it("carries it into the exchange-ticket hand-off", async () => {
    // A scoped probe followed by an unscoped exchange would leave the journey's final
    // redirect unscoped anyway.
    const user = userEvent.setup();
    await renderForm({ clientId: CLIENT_ID });

    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL, CLIENT_ID))]);
    });
  });

  it("scopes the allow-list probe to it", async () => {
    const user = userEvent.setup();
    await renderForm({ returnUrl: EXTERNAL_RETURN_URL, clientId: CLIENT_ID });

    await vi.waitFor(() => {
      expect(validateCalls()).toHaveLength(1);
    });
    expect(probeQuery().get("uri")).toBe(EXTERNAL_RETURN_URL);
    expect(probeQuery().get("clientId")).toBe(CLIENT_ID);

    // Anchored on a positive outcome: "the probe carried two parameters" is satisfied by a
    // screen that then refuses everybody.
    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);
    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });
    expect(harness.last?.body).toEqual({ code: CODE, useBackupCode: false });
  });

  it("refuses a return url only ANOTHER client registered", async () => {
    // `b.example.com` is registered by `client-b` alone and this flow belongs to `client-a`.
    // An unscoped probe says yes — the union contains every client's origins — and hands a
    // client-a login to a client-b destination.
    await renderForm({ returnUrl: OTHER_CLIENT_RETURN_URL, clientId: CLIENT_ID });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("lets that same url through for the client that DID register it", async () => {
    // The mirror image, and the reason the refusal above is scoping rather than a blanket
    // tightening: an implementation that refused every absolute URL would pass the test
    // above and break every external login.
    const user = userEvent.setup();
    await renderForm({ returnUrl: OTHER_CLIENT_RETURN_URL, clientId: OTHER_CLIENT_ID });

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);

    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });
    expect(harness.last?.body).toEqual({ code: CODE, useBackupCode: false });
    expect(mocks.navigate).not.toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
  });

  it("sends no client id at all when the flow carries a blank one", async () => {
    // A present-but-blank id is not a client. An unknown client fails CLOSED to the
    // AuthUrl-only origin set, so relaying "" would refuse the very returnUrl the user is
    // mid-journey to. The exact-URL match is what pins the ABSENCE: a `&clientId=` of any
    // spelling makes it a different string.
    const user = userEvent.setup();
    await renderForm({ clientId: "" });

    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL))]);
    });
  });
});

describe("MfaChallengeForm — a rejected code", () => {
  it("reports an invalid verification code on invalid_code", async () => {
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/invalid verification code/iu);
  });

  it("names the backup code when a backup code is rejected", async () => {
    // The invalid-code message is mode-sensitive.
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);
    await submitCode(user, BACKUP_CODE);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/invalid backup code/iu);
    await expect.element(error).not.toHaveTextContent(/verification code/iu);
  });

  it("tells the user to sign in again when the challenge session is gone", async () => {
    // `no_mfa_session` shares its 401 with `invalid_code`, so only the TOKEN can tell them
    // apart. Getting it wrong sends a user whose cookie is simply gone round a loop that
    // burns their five attempts on codes that cannot work.
    verifyWith = noMfaSessionResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/sign in again/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("does not blame the backup code when the challenge session is gone", async () => {
    // The session message is about the session, not the input: mode-sensitive wording belongs
    // to `invalid_code` alone, and a user recovering with a backup code must not be told a
    // valid one was rejected.
    verifyWith = noMfaSessionResponse;
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);
    await submitCode(user, BACKUP_CODE);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/sign in again/iu);
    await expect.element(error).not.toHaveTextContent(/invalid backup code/iu);
  });

  it("explains the lockout on mfa_locked_out", async () => {
    // Worth its own branch: the user's codes cannot work until the lockout expires, so
    // "invalid code, try again" would send them round a loop that only re-locks them.
    verifyWith = lockedOutResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/too many/iu);
    await expect.element(error).toHaveTextContent(/locked/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("explains the lockout on a 423 whose code it does not recognise", async () => {
    // 423 identifies the lockout on its own, so it is retained as a STATUS-level fallback
    // rather than only as a companion to the token — pinned against a code-only rewrite.
    verifyWith = () => rejectionResponse(423, "UNKNOWN");
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-error")).toHaveTextContent(/locked/iu);
  });

  it("falls back to the generic message for an unrecognised status", async () => {
    // A 500 is not a wrong code and must not be reported as one.
    verifyWith = () => rejectionResponse(500, "UNKNOWN");
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/verification failed/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("falls back to the generic message for a 401 whose code it does not recognise", async () => {
    // "Match known tokens, else generic". A blanket `401 -> invalid code` would pass every
    // other test in this block while re-guessing at failures it cannot identify.
    verifyWith = () => rejectionResponse(401, "some_new_token");
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/verification failed/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("shows the generic message when the request fails without a status", async () => {
    // A network-level fault never reaches a response, so the rejection carries no API token:
    // narrowing must neither throw on it nor claim the code was wrong.
    verifyWith = () => {
      throw new TypeError("network down");
    };
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/verification failed/iu);
  });

  it("never leaks the raw rejection or a machine reason token into the page", async () => {
    // Two strings are in reach and neither is a message for a human: the seam's
    // `title: "Unknown error"`, and the API's own `error` token. The screen holds the real
    // token now, so rendering it is a live temptation — every one it can send is checked.
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(page.getByText(/unknown error/iu).query()).toBeNull();
    expect(page.getByText(/no_mfa_session|mfa_locked_out|invalid_code/u).query()).toBeNull();

    // The session-gone token is the one most plausibly printed: its branch is the newest.
    verifyWith = noMfaSessionResponse;
    await user.click(page.getByTestId("mfa-challenge-submit"));

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/sign in again/iu);
    expect(page.getByText(/no_mfa_session/u).query()).toBeNull();
  });

  it("keeps the form up so the user can retry", async () => {
    // The user has four attempts left and no way to spend them if the field is gone.
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-success").query()).toBeNull();
  });

  it("does not navigate on failure", async () => {
    // A failed second factor that still redirected is the bug this screen must never have.
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(navigations).toEqual([]);
  });

  it("clears a previous error when the next attempt succeeds", async () => {
    // A stale "invalid code" banner above a successful verification is a lie.
    let attempts = 0;
    verifyWith = () => {
      attempts += 1;
      return attempts === 1 ? invalidCodeResponse() : verifiedResponse(TICKET);
    };
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();

    await user.click(page.getByTestId("mfa-challenge-submit"));

    await expect.element(page.getByTestId("mfa-challenge-success")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-error").query()).toBeNull();
  });
});

/**
 * Rendered through a real memory router rather than by poking at `Route.options.component`:
 * "returnUrl read from the query string" only exists once a router has parsed a URL, and a
 * bare render of a search-reading route throws.
 */
function renderRouteAt(url: string) {
  return renderWithWallow(null, {
    harness,
    path: url,
    routes: [{ path: "/mfa/challenge", route: mfaChallengeRoute }],
  });
}

describe("/mfa/challenge route", () => {
  it("renders the real screen in place of the pre-registration placeholder", async () => {
    // The path is the contract: it is where the login hand-off navigates.
    await renderRouteAt(`/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}`);

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(page.getByTestId("route-placeholder").query()).toBeNull();
  });

  it("threads the return url out of the query string into the exchange", async () => {
    const user = userEvent.setup();
    await renderRouteAt(`/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}`);

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL))]);
    });
  });

  it("threads client_id out of the callback redirect into the exchange", async () => {
    // The relay end to end: the callback's redirect goes in, the URL the user is sent to comes
    // out. `validateSearch` has to widen for `client_id` or the id stops at the router.
    const user = userEvent.setup();
    await renderRouteAt(
      `/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}` +
        `&client_id=${encodeURIComponent(CLIENT_ID)}`,
    );

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL, CLIENT_ID))]);
    });
  });

  it("treats a non-string client_id as absent", async () => {
    // TanStack Router JSON-parses scalar search values, so `?client_id=42` arrives as the
    // NUMBER 42 and `validateSearch` must `typeof`-narrow it as it narrows returnUrl: relaying
    // a number scopes the exchange to a client that cannot exist, which fails closed to the
    // AuthUrl-only origin set.
    const user = userEvent.setup();
    await renderRouteAt(`/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=42`);

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL))]);
    });
  });

  it("renders without throwing when the link carries no query at all", async () => {
    // A bare /mfa/challenge is the direct (non-OIDC) sign-in path, so `validateSearch` has to
    // treat returnUrl as optional rather than throw at a user mid-login.
    await renderRouteAt("/mfa/challenge");

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});
