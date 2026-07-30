import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  type SdkCall,
  type SdkHarness,
  type SdkResponder,
} from "@bc-solutions-coder/testing/sdk-harness";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { createAuthHarness } from "@shared/testing/harness";
import { Route as mfaChallengeRoute } from "@app/routes/mfa/challenge";
import { MfaChallengeForm, type MfaChallengeFormProps } from "./MfaChallengeForm";

/**
 * Component spec for the MfaChallenge screen (Wallow-vec7.3.6).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `mfa-challenge-error`, `mfa-challenge-success`, `mfa-challenge-backup-code`,
 * `mfa-challenge-code`, `mfa-challenge-submit`, `mfa-challenge-toggle-backup`.
 *
 * TEST SEAM: `@bc-solutions-coder/testing/sdk-harness` (Wallow-pu6a.5.1). The
 * SDK is the REAL one and only its `fetch` is faked, so the screen's whole
 * pipeline — request-scoped SDK -> generated operation -> CSRF interceptor ->
 * serialization -> error shaping -> React Query — runs here. Nothing mocks
 * `@bc-solutions-coder/sdk` or its `./query` entry, and there is no app-level
 * facade left to mock (Wallow-pu6a.5.5). `renderWithWallow` supplies the router
 * context the screen reads its SDK off, and `createAuthHarness()` pins the
 * harness origin to this app's root-mounted API surface — so every recorded
 * `call.path` below is the endpoint path verbatim, with no `/api` prefix.
 *
 * WHAT THAT BOUGHT. Three seams that used to be spies are now the real thing:
 *
 *   - `verifyMfa` / `useBackupCode` are ONE endpoint (`POST /v1/identity/auth/
 *     mfa/verify`) distinguished by `useBackupCode: false | true` in the body
 *     body. The pair of spies could only say "the right method was called";
 *     the recorded REQUEST says which code went to which
 *     validator, which is the claim those tests were reaching for.
 *   - the allow-list probe is a real `GET /v1/identity/auth/redirect-uri/validate`,
 *     so the mirrored `IsAllowedAsync` rule below now reads `uri` and `clientId`
 *     off the QUERY STRING the screen actually built — including the
 *     omit-the-key-entirely contract for a blank client id.
 *   - `isSafeReturnUrl` and `buildExchangeTicketUrl` are the REAL pure builders
 *     from `packages/sdk/src/auth-oidc.ts`, no longer mirrored or stubbed, so
 *     the exchange hand-off is asserted as the exact URL the user is sent to.
 *
 * ── THE ERROR-BRANCH PORT (read off the controller, not assumed) ──────────────
 *
 * The oracle switches its message on `result.Error`:
 *
 *     "invalid_code"      => "Invalid verification/backup code. Please try again."
 *     "expired_challenge" => "Challenge expired. Please sign in again."
 *     _                   => result.Error ?? "Verification failed. Please try again."
 *
 * Reading the endpoint shows that switch is partly fiction.
 * `AccountController.VerifyMfaChallenge`
 * (api/.../Controllers/AccountController.cs:167-236) has FIVE returns and every
 * failure is non-2xx:
 *
 *     401 { succeeded: false, error: "no_mfa_session" }   partial cookie missing/expired
 *     401 { succeeded: false, error: "invalid_code" }     no user / no TOTP secret
 *     401 { succeeded: false, error: "invalid_code" }     code rejected
 *     423 { succeeded: false, error: "mfa_locked_out" }   already locked, or now locked
 *     200 { succeeded: true, signInTicket }               the ONLY success
 *
 * Those five bodies are now literally what the failure tests below put ON THE
 * WIRE, rather than a hand-built rejection object standing in for them.
 *
 * Two of the oracle's three branches are warts, and are NOT ported:
 *
 *  1. `"expired_challenge"` IS DEAD CODE. This endpoint never emits that string —
 *     the expired-cookie case is `no_mfa_session`. A branch keyed on a token the
 *     API cannot send is not a behaviour worth carrying across.
 *  2. The API's error tail renders `result.Error` RAW, so a user can be shown
 *     the literal "no_mfa_session" or "mfa_locked_out". The oracle shows machine
 *     tokens to humans; pinned against by "never leaks a raw reason token".
 *
 * ── WHY THIS SPEC KEYS ON `code` (REVISED — was status-only) ──────────────────
 *
 * The first cut of this spec narrowed on HTTP status alone and accepted a known
 * loss: 401 is AMBIGUOUS between `invalid_code` and `no_mfa_session`, so a user
 * whose partial-auth cookie had expired was mis-told their code was wrong.
 * `toWallowError()` built its `code` from `extensions.code ?? code`, and these
 * endpoints emit neither — they return a bare `{ succeeded, error }` anon object,
 * so the token sitting under `error` was never read and the screen always
 * received `code: "UNKNOWN"`.
 *
 * Wallow-vec7.7 closed that: `readCode` (now `packages/sdk/src/runtime-config.ts`)
 * probes `extensions.code > code > error`, so the API's own token reaches the
 * screen intact. The loss is recovered and this spec is revised UPWARD to pin
 * the better behaviour — the distinction the oracle's switch was reaching for:
 *
 *     code "invalid_code"    -> the oracle's mode-sensitive invalid-code message.
 *                               The form stays up: the user has attempts left.
 *     code "no_mfa_session"  -> the challenge session is gone; retyping codes
 *                               cannot help. Send them back to sign in — what the
 *                               oracle's dead "expired_challenge" branch MEANT to
 *                               say, now reachable via the token the API really
 *                               sends.
 *     code "mfa_locked_out"
 *       OR status 423        -> the locked-out message. 423 is kept as a
 *                               status-level fallback because it identifies this
 *                               failure on its own, and a locked user retyping
 *                               codes only re-locks themselves.
 *     anything else          -> the oracle's generic `_` tail, minus the leak.
 *
 * `code` is matched against KNOWN tokens only, never rendered: it is a machine
 * string. An unrecognised code — including a 401 carrying one — falls to the
 * generic message rather than guessing, which is why a blanket `401 -> invalid
 * code` rule is pinned against below.
 *
 * ── THE ORIGIN DIVERGENCE (inherited from Wallow-vec7.3.4) ────────────────────
 *
 * The oracle prepends an absolute API origin (`Configuration["ApiBaseUrl"] ??
 * "http://localhost:5001"`) to BOTH of its navigation targets — the
 * exchange-ticket URL and `BuildApiReturnUrl`. That prepend is deliberately NOT
 * ported: apps/wallow-auth's API surface (`src/lib/api-passthrough.ts`) is a
 * passthrough reverse proxy mounting `/v1/**` and `/connect/**` at the ROOT, so
 * this origin DOES host them and the origin argument is `""` (bd memory
 * `wallow-auth-same-origin-baseurl-apps-wallow-auth`).
 *
 * This is the security decision this screen exists to prove. Going cross-origin
 * would drop the `SameSite` partial-auth cookie that `mfa/verify` reads and the
 * exchange-ticket endpoint upgrades — the exact round-trip named in this bead's
 * acceptance. It is pinned end-to-end now: the recorded navigation target is a
 * ROOT-RELATIVE path, so it resolves against the page's own origin, and an
 * implementation that prepended an API origin would produce a different absolute
 * URL and fail.
 *
 * ── NAVIGATION SEAM (Wallow-xzha.3.1: real Chromium, not jsdom) ───────────────
 *
 * `window.location` is `[Unforgeable]` in a real browser, so the old jsdom-only
 * `vi.stubGlobal("location", …)` cannot shadow it and a real `location.href = …`
 * navigates the Chromium runner away and tears it down. The seam that replaces
 * it is the NAVIGATION API: a `navigate` listener records
 * `event.destination.url` and calls `event.preventDefault()`, which cancels the
 * cross-document navigation before it unloads the runner. So the assigned URL is
 * observed DIRECTLY and exactly, and the two workarounds the builder mock forced
 * — a non-navigating `#exchange-ticket` sentinel return value, and a same-page
 * `directReturnUrl()` for the builder-less branch — are both gone.
 */

// Hoisted so the vi.mock factory and the test bodies share the same spy. Only
// the router's `useNavigate` is mocked: it is the seam for the screen's in-app
// bail to /error, has nothing to do with how the screen reaches the API, and is
// not matched by the SDK-seam guard (`src/sdk-test-seam.test.ts`).
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
 * The returnUrl the EXTERNAL-LOGIN hand-off really sends (Wallow-vec7.3.17).
 *
 * `AccountController.ExternalLoginCallback` normalizes returnUrl at L273-277 --
 * either it passed `redirectUriValidator.IsAllowedAsync`, which requires
 * `Uri.TryCreate(uri, UriKind.Absolute)` (OpenIddictRedirectUriValidator.cs:24),
 * or it was replaced by the `authUrl` fallback, absolute by construction -- and
 * then redirects to `{authUrl}/mfa/challenge?returnUrl={encodedReturn}` (L313,
 * L335). So this shape, ABSOLUTE and allow-listed, is what 100% of external-login
 * MFA users arrive with. `isSafeReturnUrl` is false for every one of them, which
 * is the dead-end this bead fixes.
 */
const EXTERNAL_RETURN_URL = "http://localhost:5002/login";

/** An absolute returnUrl from an origin the allow-list has never heard of. */
const EVIL_RETURN_URL = "https://evil.example.com/steal";

/** The client that started the flow, as `external-login-callback` names it. */
const CLIENT_ID = "client-a";

/** A SECOND registered client — the one this flow does NOT belong to. */
const OTHER_CLIENT_ID = "client-b";

/**
 * An absolute returnUrl registered by `client-b` and by nobody else. Allowed
 * when `client-b` is asking and refused when `client-a` is, which is the whole
 * point of scoping the probe.
 */
const OTHER_CLIENT_RETURN_URL = "https://b.example.com/callback";

/** The `AuthUrl` origin, which `IsAllowedAsync` admits for every client. */
const AUTH_URL_ORIGIN = "http://localhost:5002";

/**
 * What each client has REGISTERED (its redirect + post-logout URIs), which is
 * what `IsAllowedAsync` consults once it is given a client id
 * (OpenIddictRedirectUriValidator.cs:44-52).
 *
 * A Map rather than a Record because the lookup key is attacker-supplied query
 * cargo (bd memory `attacker-supplied-query-key-lookups-use-map-not-record`): a
 * Record would answer `"constructor"` with an inherited value.
 */
const CLIENT_REGISTERED_ORIGINS = new Map<string, readonly string[]>([
  [CLIENT_ID, ["https://app.example.com"]],
  [OTHER_CLIENT_ID, ["https://b.example.com"]],
]);

/**
 * The origins `IsAllowedAsync` admits with NO client id: the UNION of every
 * registered client's, plus `AuthUrl` (OpenIddictRedirectUriValidator.cs:53-65).
 *
 * The union is the reason the unscoped probe is a hole rather than a nuisance —
 * it answers "yes" for a URI any client at all registered, whoever is asking.
 */
const ALLOWED_ORIGINS = new Set([
  AUTH_URL_ORIGIN,
  ...[...CLIENT_REGISTERED_ORIGINS.values()].flat(),
]);

/** The bail target for an unsafe returnUrl, matching the ConsentScreen port. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/**
 * The real `IsAllowedAsync` rule (OpenIddictRedirectUriValidator.cs:23-32),
 * mirrored rather than hard-coded: a fake that answered a constant would let the
 * evil-origin test pass for the wrong reason.
 *
 * Both halves are load-bearing. `Uri.TryCreate(uri, UriKind.Absolute)` is the
 * parse gate -- `new URL()` throws on `//evil.example.com/steal` exactly as
 * TryCreate fails it -- and `allowedOrigins.Contains(GetOrigin(parsed))` is the
 * allow-list. The endpoint answers `Ok(new { allowed = result })`
 * (AccountController.cs:601-612).
 *
 * The `clientId` arm mirrors `GetAllowedOriginsAsync`: given an id, the set is
 * THAT client's registered origins plus AuthUrl; given none, it is the union
 * over every client. An UNKNOWN id resolves to no application and leaves only
 * AuthUrl -- the fail-closed behaviour, not an error.
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
    // Not absolute -- `TryCreate(UriKind.Absolute)` fails and the endpoint says no.
    return false;
  }

  return allowedOriginsFor(clientId).has(parsed.origin);
}

/**
 * The allow-list endpoint, answering off the QUERY STRING the screen built.
 *
 * Reading `uri`/`clientId` back out of `call.url` rather than off a spy's
 * arguments is what makes the scoping assertions below real: an implementation
 * that computed the right client id and then failed to put it on the wire would
 * now be caught.
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

/**
 * What this endpoint really puts on the wire for a failure: a non-2xx carrying a
 * bare `{ succeeded: false, error }` anon object — NOT problem details. The
 * screen's `code` comes from that `error` member via `readCode`'s third probe,
 * and its `status` from the transport status.
 */
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
 * The exact URL `buildExchangeTicketUrl("", ticket, returnUrl, clientId?)`
 * produces (packages/sdk/src/auth-oidc.ts:146-175), spelled out rather than
 * imported: importing the builder to build the expectation would assert it
 * against itself.
 *
 * ROOT-RELATIVE, which IS the same-origin claim — see the origin-divergence note
 * in the file header.
 */
function exchangeUrl(ticket: string, returnUrl: string, clientId?: string): string {
  const base: string =
    `/v1/identity/auth/exchange-ticket` +
    `?ticket=${encodeURIComponent(ticket)}` +
    `&returnUrl=${encodeURIComponent(returnUrl)}`;

  return clientId === undefined ? base : `${base}&clientId=${encodeURIComponent(clientId)}`;
}

/**
 * The Navigation API's `destination.url` is always ABSOLUTE, so a relative
 * expectation has to be resolved against the page the runner is on before it can
 * be compared.
 */
function absolute(url: string): string {
  return new URL(url, globalThis.location.href).href;
}

/**
 * The minimum of the Navigation API this spec uses. Declared structurally
 * because `globalThis.navigation` is not in the DOM lib this repo compiles
 * against, and a cast would be an `as any` in all but name.
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

/**
 * Record the destination and CANCEL the navigation. The cancel is what keeps the
 * Chromium runner alive: without it the first `location.href = …` unloads the
 * page mid-suite and every later test dies with it.
 */
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

/** Every recorded call to the verify endpoint — the "was it submitted" question. */
function verifyCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === VERIFY_PATH);
}

/** Every recorded call to the allow-list probe. */
function validateCalls(): readonly SdkCall[] {
  return harness.calls.filter((call: SdkCall) => call.path === VALIDATE_PATH);
}

/** Query string of the first allow-list probe, or an empty set if it never happened. */
function probeQuery(): URLSearchParams {
  const url: string | undefined = validateCalls().at(0)?.url;
  return url === undefined ? new URLSearchParams() : new URL(url).searchParams;
}

/** Render `ui` on the shared harness: real SDK, fake transport, real router context. */
function renderWithClient(ui: ReactElement) {
  return renderWithWallow(ui, { harness });
}

/** Render the screen as an OIDC MFA hand-off would: a safe returnUrl present. */
function renderForm(props: Partial<MfaChallengeFormProps> = {}) {
  return renderWithClient(<MfaChallengeForm returnUrl={RETURN_URL} {...props} />);
}

/** Switch to backup-code entry — the oracle's `ToggleBackupCode`. */
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

  harness = createAuthHarness();
  verifyWith = () => verifiedResponse(TICKET);
  validateWith = allowListResponder;
  // ONE dispatcher, installed once: the tests reprogram the two endpoint
  // responders above rather than re-installing a whole transport each time.
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
    // The oracle's two fields are branches of one `if (_useBackupCode)`, never
    // both at once. Two visible code boxes would be a genuinely confusing form.
    //
    // The positive half is load-bearing, not redundant: "the backup field is
    // absent" is trivially true of a page that rendered nothing, so on its own
    // this assertion passes against an empty stub. Anchoring it to the field
    // that MUST be there makes it fail for the right reason.
    await renderForm();

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-backup-code").query()).toBeNull();
  });

  it("links back to sign in", async () => {
    // The card footer. It has no testid and the scout's inventory forbids
    // inventing one for an element that shipped without one, so this asserts the
    // link by role + href instead.
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
    // `_useBackupCode = !_useBackupCode` — the toggle is symmetric, and a user
    // who opened backup entry by mistake must be able to get out of it.
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);
    await user.click(page.getByTestId("mfa-challenge-toggle-backup"));

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-backup-code").query()).toBeNull();
  });

  it("offers the other mode in its label each way round", async () => {
    // The oracle's toggle names the DESTINATION, not the current state
    // ("Use backup code instead" / "Use authenticator code instead"). A toggle
    // labelled with the mode you are already in is a coin flip for the user.
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
    // The oracle's `BbCardDescription` branches on `_useBackupCode`. Asserted
    // against the description SENTENCE, not a bare /backup code/ substring: the
    // oracle's own field label is "Backup code", so a substring match would be
    // satisfied by the label and could never tell the description apart from it.
    const user = userEvent.setup();
    await renderForm();

    await expect
      .element(page.getByText(/enter the code from your authenticator app/iu))
      .toBeInTheDocument();

    await toggleToBackupCode(user);

    await expect.element(page.getByText(/enter one of your backup codes/iu)).toBeInTheDocument();
  });

  it("discards a code typed in the other mode", async () => {
    // Oracle: `_code = string.Empty;` inside `ToggleBackupCode`. A TOTP code left
    // sitting in the backup-code box would be submitted to the wrong branch and
    // burn one of the user's five attempts before the lockout.
    const user = userEvent.setup();
    await renderForm();

    await user.type(page.getByTestId("mfa-challenge-code"), CODE);
    await toggleToBackupCode(user);

    await expect.element(page.getByTestId("mfa-challenge-backup-code")).toHaveValue("");
  });

  it("clears a standing error", async () => {
    // Oracle: `_errorMessage = null;` inside `ToggleBackupCode`. "Invalid
    // verification code" hanging over a freshly-opened backup-code box is a lie.
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
    // Oracle: `if (string.IsNullOrWhiteSpace(_code))`. A blank submit must not
    // reach `mfa/verify` — it cannot succeed, and it costs a lockout attempt.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user, "");

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/enter the verification code/iu);
    expect(verifyCalls()).toHaveLength(0);
  });

  it("asks for a backup code by name when the backup field is blank", async () => {
    // The oracle's guard is mode-sensitive too.
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
    // `IsNullOrWhiteSpace`, not `IsNullOrEmpty` — "   " never reaches the endpoint.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user, "   ");

    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("sends the typed code to the authenticator endpoint", async () => {
    // Oracle: `await AuthClient.VerifyMfaChallengeAsync(_code)`. The facade's two
    // methods are ONE op distinguished by the body flag, so `useBackupCode: false`
    // IS the "went to the authenticator validator" claim — strictly more than the
    // old pair of "this spy, not that spy" assertions could say.
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
    // Oracle: `_useBackupCode ? UseBackupCodeAsync(_code) : VerifyMfaChallengeAsync(_code)`.
    // These are the same API op with `useBackupCode: true/false` (auth-client.ts:196-198);
    // crossing them would send a recovery code to the TOTP validator.
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
    // Oracle: `Disabled="_isSubmitting"` — one click, one attempt. This screen is
    // rate-limited into a 5-strike lockout, so a double submit can cost the user
    // two of their five.
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

    // Wait for the request to REACH the transport before releasing it: the submit
    // button goes disabled the moment the mutation starts, which is a tick or two
    // before `fetch` is called, and releasing into that gap would leave the
    // never-settling responder installed forever.
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
    // Oracle: `_verified = true`, which replaces the form with a success alert.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-success")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-error").query()).toBeNull();
  });

  it("hands the ticket to the exchange endpoint on THIS origin, not an API origin", async () => {
    // THE LOAD-BEARING ASSERTION OF THIS SCREEN. The oracle builds
    // `{ApiBaseUrl}/v1/identity/auth/exchange-ticket?...`; this port passes `""`.
    // A cross-origin exchange would drop the SameSite cookie the whole partial-auth
    // round-trip depends on — see the origin-divergence note in the file header.
    // Asserted as the URL the user is really sent to: `exchangeUrl` is
    // root-relative, so `absolute()` resolves it against the PAGE's origin and an
    // implementation that prepended an API origin produces a different string.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL))]);
    });
  });

  it("navigates straight to the return url when the response carries no ticket", async () => {
    // Oracle: `else if (!string.IsNullOrEmpty(safeReturnUrl))` ->
    // `BuildApiReturnUrl(safeReturnUrl)`. The oracle prepends `ApiBaseUrl` there
    // too; same-origin makes that prepend the identity function, so the safe
    // relative path is navigated to verbatim. The exact-match assertion is also
    // what says the exchange builder was NOT used: an exchange URL is a different
    // destination, and there is only one.
    verifyWith = () => verifiedResponse();
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(RETURN_URL)]);
    });
  });

  it("treats a blank ticket as no ticket", async () => {
    // `IsNullOrEmpty(result.SignInTicket)`. `buildExchangeTicketUrl` THROWS on a
    // blank ticket ("ticket is required", auth-oidc.ts:154) — and it is the REAL
    // builder here, so a screen that called it anyway really would replace the
    // user's redirect with a crash and navigate nowhere.
    verifyWith = () => verifiedResponse("");
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(RETURN_URL)]);
    });
  });

  it("stays put on a direct sign-in with no return url", async () => {
    // The oracle's trailing comment: "No ReturnUrl — direct login, not OIDC. Show
    // success state without redirecting." A nullish returnUrl is not hostile and
    // gets no "/" fallback (bd memory `returnurl-guard-refuse-dont-sanitize`).
    const user = userEvent.setup();
    await renderForm({ returnUrl: undefined });

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-success")).toBeInTheDocument();
    expect(navigations).toEqual([]);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("verifies a backup code through the same redirect path", async () => {
    // The backup branch is a real sign-in, not a second-class one: it must reach
    // the same exchange, or a user recovering with a backup code verifies and
    // then goes nowhere.
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
    // bd memory `returnurl-guard-refuse-dont-sanitize`: REFUSE to /error, do not
    // fall back to "/". The oracle instead nulls an unsafe returnUrl and shows a
    // bare success — silently swallowing an open-redirect attempt.
    //
    // Refused on MOUNT, following the ConsentScreen port (Wallow-vec7.3.4) and
    // `Login.razor` L533-540: making a user produce a second factor for a
    // destination we have already decided to refuse — and telling them only
    // afterwards — wastes a one-time code on a request we know is malformed.
    await renderForm({ returnUrl: "//evil.example.com/steal" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("refuses an absolute return url the allow-list does not know", async () => {
    // Absolute, so `isSafeReturnUrl` cannot answer and the SERVER's allow-list is
    // asked. `evil.example.com` is not a registered origin -> `{ allowed: false }`
    // -> refused. Same outcome as before Wallow-vec7.3.17, for a reason that now
    // discriminates rather than refusing every absolute URL alike.
    await renderForm({ returnUrl: EVIL_RETURN_URL });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("lets the external-login hand-off through on an allow-listed absolute return url", async () => {
    // THE REGRESSION TEST FOR Wallow-vec7.3.17. `AccountController.cs:313/335`
    // sends an ABSOLUTE returnUrl here for every external-login MFA user, and
    // `isSafeReturnUrl` is false for every absolute URL -- so the mount guard
    // bounced 100% of them to /error before the code field ever rendered. They
    // could not sign in at all.
    //
    // The API already admitted this exact value through `IsAllowedAsync`
    // (AccountController.cs:274) before redirecting here, so the allow-list the
    // screen asks is the same one that let it in: it says yes.
    const user = userEvent.setup();
    await renderForm({ returnUrl: EXTERNAL_RETURN_URL });

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);

    // Anchored on a POSITIVE assertion: the user reaches the exchange, not merely
    // "was not sent to /error" -- which a screen that renders nothing satisfies.
    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, EXTERNAL_RETURN_URL))]);
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
  });

  it("decides a relative return url locally, without asking the server", async () => {
    // The password path (Login.razor:509 -> BuildMfaRedirectUrl) threads a
    // RELATIVE returnUrl, and `isSafeReturnUrl` settles it with no network. The
    // probe is the external-login path's cost alone; spending it on every login
    // would put an outbound request between the user and their code field.
    await renderForm();

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(validateCalls()).toHaveLength(0);
    expect(mocks.navigate).not.toHaveBeenCalled();
  });

  it("refuses an empty-string return url without asking the server", async () => {
    // `?returnUrl=` is a PRESENT value that fails `IsNullOrWhiteSpace`, so it is
    // the unsafe case and not the nullish no-redirect one. It is a malformed link,
    // not a destination to ask about -- the `IsNullOrEmpty` short-circuit the
    // LogoutScreen port gates its own probe with (LogoutScreen.tsx:219-221).
    await renderForm({ returnUrl: "" });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(validateCalls()).toHaveLength(0);
  });

  it("does not render the form while the allow-list check is in flight", async () => {
    // FAIL CLOSED IN FLIGHT. A form rendered optimistically is a form a fast user
    // can submit -- burning a one-time second factor on a destination we may be
    // about to refuse, the exact cost the mount-time refusal exists to avoid.
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
    // The C# `!IsSuccessStatusCode -> false` arm arrives as a REJECTION (the
    // client throws on non-2xx, and a transport fault never resolves at all). An
    // unreachable validator must never become a reason to TRUST a URI.
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
    // The `{ allowed }` narrowing is STRICT, as the C# `body?.Allowed == true`
    // collapse is: the STRING "true" is truthy in JS and must NOT pass, or a
    // screen leaning on truthiness would redirect on `allowed: "false"` too.
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
    // The nullish case is the oracle's ordinary non-OIDC path — routing it to
    // /error would break every direct login.
    await renderForm({ returnUrl: undefined });

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});

/**
 * THE FLOW'S CLIENT ID (Wallow-nv7l.1, closing Wallow-53kr's acceptance).
 *
 * Wallow-9jab taught the API to scope its redirect checks to a client id, and
 * Wallow-53kr carried that id through the external-login journey: `external-login`
 * stashes it in the challenge properties, `external-login-callback` recovers it
 * and puts `client_id` on the `/mfa/challenge` redirect it issues
 * (AccountController.cs, both MFA branches). This screen is where that hand-off
 * lands, and it is the last link that still drops the id — so the two things it
 * does with `returnUrl` are both asked UNSCOPED today:
 *
 *   1. the allow-list probe, which without an id answers against the UNION of
 *      every registered client's origins. A URI registered by any client at all
 *      passes for every client — the bypass the scoping exists to close.
 *   2. the exchange-ticket hand-off, where `AccountController.ExchangeTicket`
 *      re-checks the returnUrl and falls back to the same union set.
 *
 * SPELLINGS, both deliberate: the id arrives on the query string as `client_id`
 * (the OIDC spelling the API redirects with) and leaves as `clientId` (the
 * `[FromQuery]` name the endpoint binds) — the contract Wallow-53kr pinned on the
 * accept-terms relay, applied to the other screen on the same journey. With the
 * real transport under the spec, that outbound spelling is now read off the WIRE.
 */
describe("MfaChallengeForm — the flow's client id", () => {
  it("carries it into the exchange-ticket hand-off", async () => {
    // The id has to survive the last hop too: `ExchangeTicket` validates the
    // returnUrl AGAIN before setting the cookie, and without the id it validates
    // against the union set — so a scoped probe followed by an unscoped exchange
    // would leave the journey's final redirect unscoped anyway.
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

    // Anchored on a positive outcome: the user still gets through on a URL their
    // own client registered. "The probe carried two parameters" is satisfied by a
    // screen that then refuses everybody.
    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);
    await vi.waitFor(() => {
      expect(verifyCalls()).toHaveLength(1);
    });
    expect(harness.last?.body).toEqual({ code: CODE, useBackupCode: false });
  });

  it("refuses a return url only ANOTHER client registered", async () => {
    // THE ACCEPTANCE CRITERION, in the browser. `b.example.com` is registered by
    // `client-b` alone, and this flow belongs to `client-a`. The unscoped probe
    // says yes to it — the union contains every client's origins — so today this
    // screen hands a client-a login to a client-b destination. Scoped, the
    // answer is no.
    await renderForm({ returnUrl: OTHER_CLIENT_RETURN_URL, clientId: CLIENT_ID });

    await vi.waitFor(() => {
      expect(mocks.navigate).toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
    });
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
    expect(verifyCalls()).toHaveLength(0);
  });

  it("lets that same url through for the client that DID register it", async () => {
    // The mirror image, and the reason the refusal above is scoping rather than
    // a blanket tightening: `client-b`'s own users must still reach their own
    // destination. A green phase that refused every absolute URL would pass the
    // test above and break every external login.
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
    // A present-but-blank id is not a client. The endpoint fails an unknown
    // client CLOSED to the AuthUrl-only origin set, so relaying "" would refuse
    // the very returnUrl the user is mid-journey to, where sending nothing falls
    // back to the behaviour that works today. The exact-URL match is what pins
    // the ABSENCE: a `&clientId=` of any spelling makes it a different string.
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
    // The oracle's `"invalid_code" =>` branch, reached via the token the API
    // really sends now that the seam surfaces it (Wallow-vec7.7).
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/invalid verification code/iu);
  });

  it("names the backup code when a backup code is rejected", async () => {
    // The oracle's invalid_code branch is mode-sensitive: "Invalid backup code."
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
    // THE BEHAVIOUR RECOVERED BY Wallow-vec7.7. `no_mfa_session` shares its 401
    // with `invalid_code`, so the first cut of this spec could only mis-tell
    // these users their code was wrong — sending them round a loop that burns
    // their five attempts against a cookie that is simply gone. The seam now
    // surfaces the token, so they get the truth: nothing they type here can
    // work, and the "Back to sign in" footer is the way out. This is what the
    // oracle's dead `expired_challenge` branch was reaching for.
    verifyWith = noMfaSessionResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/sign in again/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("does not blame the backup code when the challenge session is gone", async () => {
    // The session message is about the session, not the input: the mode-sensitive
    // wording belongs to `invalid_code` alone. A user recovering with a backup
    // code must not be told a valid one was rejected.
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
    // Worth branching on: the user's codes cannot work until the lockout expires,
    // and "invalid code, try again" would send them round a loop that only
    // re-locks them. The oracle printed the raw token "mfa_locked_out" here.
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
    // 423 is retained as a STATUS-level fallback, not merely as a companion to
    // the token: this status identifies the lockout on its own, and the cost of
    // missing it (a locked user retyping codes) is higher than the cost of the
    // extra rule. Pins the fallback against a code-only rewrite.
    verifyWith = () => rejectionResponse(423, "UNKNOWN");
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-error")).toHaveTextContent(/locked/iu);
  });

  it("falls back to the generic message for an unrecognised status", async () => {
    // The oracle's `_ =>` tail. A 500 is not a wrong code and must not be
    // reported as one.
    verifyWith = () => rejectionResponse(500, "UNKNOWN");
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/verification failed/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("falls back to the generic message for a 401 whose code it does not recognise", async () => {
    // "Match known tokens, else generic" — the rule the recovered `code` earns.
    // Pins against the status-only narrowing this spec was revised away FROM:
    // a blanket `401 -> invalid code` would pass every other test in this block
    // while quietly re-guessing at failures it cannot identify.
    verifyWith = () => rejectionResponse(401, "some_new_token");
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/verification failed/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("shows the generic message when the request fails without a status", async () => {
    // A network-level fault never reaches a response at all, so the rejection
    // carries no API token; structural narrowing must not throw on it, and must
    // not claim the code was wrong.
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
    // The seam hands the screen `title: "Unknown error"`, and the API's error
    // branch prints the API's own `error` string — so a user could
    // be shown "no_mfa_session". Neither is a message for a human.
    //
    // Sharper now than when `code` was always "UNKNOWN": Wallow-vec7.7 puts the
    // real token in the screen's hands, so "render the code" is a live temptation
    // and `no_mfa_session` is a string an implementation could now actually
    // print. Every token the endpoint can send is checked, whichever arrives.
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(page.getByText(/unknown error/iu).query()).toBeNull();
    expect(page.getByText(/no_mfa_session|mfa_locked_out|invalid_code/u).query()).toBeNull();

    // The code the endpoint sends when the session is gone is the one an
    // implementation could most plausibly print: it is the branch with no
    // pre-existing oracle copy behind it.
    verifyWith = noMfaSessionResponse;
    await user.click(page.getByTestId("mfa-challenge-submit"));

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/sign in again/iu);
    expect(page.getByText(/no_mfa_session/u).query()).toBeNull();
  });

  it("keeps the form up so the user can retry", async () => {
    // The oracle only replaces the form on `_verified`. A rejected code must
    // leave the field in place — the user has four attempts left and no way to
    // spend them if the form is gone.
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(page.getByTestId("mfa-challenge-success").query()).toBeNull();
  });

  it("does not navigate on failure", async () => {
    // `_verified` gates the whole redirect block. A failed second factor that
    // still redirected would be the bug this screen must never have.
    verifyWith = invalidCodeResponse;
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(navigations).toEqual([]);
  });

  it("clears a previous error when the next attempt succeeds", async () => {
    // Oracle: `_errorMessage = null;` at the top of `HandleVerify`. A stale
    // "invalid code" banner above a successful verification would be a lie.
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
 * Route-level spec. Rendered through a real memory router rather than by poking
 * at `Route.options.component`, because the criterion under test — "returnUrl
 * read from the query string" — only exists once a URL is parsed by a router,
 * and a bare render of a search-reading route throws (Wallow-vec7.3.2's finding).
 * The root here is a throwaway: the app's real `__root.tsx` renders `<html>`,
 * and `src/router.tsx` is off-limits to this task (Wallow-vec7.3.16).
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
    // Wallow-vec7.3.16 registered this path against a placeholder component;
    // this task's job is to replace it. The path is the contract (it is where
    // Wallow-vec7.3.15's login hand-off navigates) and is not this task's to change.
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
    // The relay end to end at the route level: the redirect
    // `external-login-callback` issues goes in, the URL the user is sent to comes
    // out, and the snake_case -> camelCase hop happens in between.
    // `validateSearch` has to widen for `client_id` or the id stops at the router.
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
    // TanStack Router JSON-parses scalar search values, so `?client_id=42`
    // arrives as the NUMBER 42 (bd memory
    // `tanstack-router-default-search-parser-json-parses-values`).
    // `validateSearch` must `typeof`-narrow it like it narrows returnUrl:
    // relaying a number would scope the exchange to a client that cannot exist,
    // and an unknown client fails closed to the AuthUrl-only origin set.
    const user = userEvent.setup();
    await renderRouteAt(`/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}&client_id=42`);

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);

    await vi.waitFor(() => {
      expect(navigations).toEqual([absolute(exchangeUrl(TICKET, RETURN_URL))]);
    });
  });

  it("renders without throwing when the link carries no query at all", async () => {
    // A bare /mfa/challenge is the direct (non-OIDC) sign-in path and must still
    // render its form — `validateSearch` has to treat returnUrl as optional
    // rather than throw at a user mid-login.
    await renderRouteAt("/mfa/challenge");

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    expect(mocks.navigate).not.toHaveBeenCalled();
  });
});
