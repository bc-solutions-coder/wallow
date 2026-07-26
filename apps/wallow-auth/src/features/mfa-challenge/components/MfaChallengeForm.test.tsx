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
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route as mfaChallengeRoute } from "../../../routes/mfa/challenge";
import { MfaChallengeForm, type MfaChallengeFormProps } from "./MfaChallengeForm";

/**
 * Component spec for the MfaChallenge screen (Wallow-vec7.3.6).
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `mfa-challenge-error`, `mfa-challenge-success`, `mfa-challenge-backup-code`,
 * `mfa-challenge-code`, `mfa-challenge-submit`, `mfa-challenge-toggle-backup`.
 *
 * MOCKING SEAM: `../../../lib/wallow-auth-sdk` — the app's own facade, never
 * `@bc-solutions-coder/sdk` directly (that module is the only permitted importer
 * of the SDK). Per bd memories `vitest-resetmodules-breaks-instanceof-across-
 * graphs`, this file uses a plain `vi.mock` factory + `vi.hoisted` spies and
 * NEVER `vi.resetModules()`.
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
 * Wallow-vec7.7 closed that: `readCode` (packages/sdk/src/auth-client.ts) now
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
 * ported: apps/wallow-auth's h3 server (`src/lib/auth-server.ts`) is a
 * passthrough reverse proxy mounting `/v1/**` and `/connect/**` at the ROOT, so
 * this origin DOES host them and the origin argument is `""` (bd memory
 * `wallow-auth-same-origin-baseurl-apps-wallow-auth`).
 *
 * This is the security decision this screen exists to prove. Going cross-origin
 * would drop the `SameSite` partial-auth cookie that `mfa/verify` reads and the
 * exchange-ticket endpoint upgrades — the exact round-trip named in this bead's
 * acceptance. The builder-seam assertions below pin it in both directions.
 *
 * ── NAVIGATION SEAM (Wallow-xzha.3.1: real Chromium, not jsdom) ───────────────
 *
 * `window.location` is `[Unforgeable]` in a real browser, so the old jsdom-only
 * `vi.stubGlobal("location", …)` cannot shadow it and a real `location.href = …`
 * navigates the Chromium runner away and tears it down. The exchange hand-off is
 * therefore pinned by asserting the deterministic URL-builder seam
 * (`buildExchangeTicketUrl`) was called with the exact origin + ticket +
 * returnUrl — equivalent to pinning the assigned string — and the builder mock
 * returns a NON-navigating fragment sentinel so the assignment stays put. The
 * one hand-off with no builder (the no-ticket branch's direct
 * `location.href = returnUrl`) is exercised with a same-page `#`-suffixed
 * returnUrl (`directReturnUrl`), which is still relative-safe and changes only
 * the fragment, so the navigation is observable without unloading the runner.
 */

// Hoisted so the vi.mock factories and the test bodies share the same spies.
const mocks = vi.hoisted(() => ({
  verifyMfa: vi.fn(),
  useBackupCode: vi.fn(),
  validateRedirectUri: vi.fn(),
  isSafeReturnUrl: vi.fn(),
  buildExchangeTicketUrl: vi.fn(),
  navigate: vi.fn(),
}));

vi.mock("../../../lib/wallow-auth-sdk", () => ({
  getWallowAuthSdk: () => ({
    auth: {
      verifyMfa: mocks.verifyMfa,
      useBackupCode: mocks.useBackupCode,
    },
    oidc: {
      isSafeReturnUrl: mocks.isSafeReturnUrl,
      buildExchangeTicketUrl: mocks.buildExchangeTicketUrl,
    },
  }),
}));

/**
 * THE READ SEAM MOVED (Wallow-evd5.3.1). The absolute-returnUrl probe is now
 * `useQuery(authQueries.redirectValidation(returnUrl))` from the SDK query layer
 * rather than a facade call inside inline `useQuery` options, so the facade mock
 * above no longer carries `validateRedirectUri` and the spy hangs off the
 * FACTORY. Code verification and the backup-code path are facade mutations and
 * stay there.
 *
 * The factory is SHARED with the logout screen — both ask the same endpoint
 * whether a URL is allowed — so `importOriginal` keeping the real `queryKey`
 * matters: the two screens are meant to hit one cache entry per URL. The spy
 * returns the RAW endpoint payload (the screen narrows it to a boolean), which is
 * what these tests already feed it.
 *
 * The client id (Wallow-nv7l.1) is forwarded to the spy only when the caller
 * supplies one, so the pre-existing single-argument assertions keep saying what
 * they always said: an unscoped probe is one argument, a scoped one is two.
 */
vi.mock("@bc-solutions-coder/sdk/query", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk/query")>();
  return {
    ...actual,
    authQueries: {
      ...actual.authQueries,
      redirectValidation: (url: string, clientId?: string) => ({
        ...actual.authQueries.redirectValidation(url, clientId),
        queryFn: async (): Promise<unknown> =>
          clientId === undefined
            ? await mocks.validateRedirectUri(url)
            : await mocks.validateRedirectUri(url, clientId),
      }),
    },
  };
});

vi.mock("@tanstack/react-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@tanstack/react-router")>()),
  useNavigate: () => mocks.navigate,
}));

const CODE = "123456";
const BACKUP_CODE = "abcd-efgh-ijkl";
const TICKET = "sign-in-ticket-xyz";
const RETURN_URL = "/connect/authorize?client_id=web";

/**
 * The non-navigating value `buildExchangeTicketUrl` is mocked to return. The real
 * builder produces `/v1/identity/auth/exchange-ticket?...`, which would navigate
 * the Chromium runner when the screen assigns it to `location.href`. A pure
 * fragment resolves against the current page and changes only the hash, so the
 * assignment stays put. The assigned string is not asserted; the builder's CALL
 * ARGS are (see the navigation-seam note above).
 */
const EXCHANGE_TICKET_SENTINEL = "#exchange-ticket";

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
 * A relative-safe returnUrl that resolves to the CURRENT test page plus a
 * fragment, so the screen's builder-less no-ticket hand-off
 * (`globalThis.location.href = returnUrl`) changes only the hash and does NOT
 * unload the Chromium runner. It still starts with a single `/`, so
 * `isSafeReturnUrl` accepts it with no server probe, exactly as a real relative
 * returnUrl is accepted — the branch under test.
 */
function directReturnUrl(): string {
  return `${globalThis.location.pathname}${globalThis.location.search}#direct-return`;
}

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
 * The real `IsAllowedAsync` rule (OpenIddictRedirectUriValidator.cs:23-32),
 * mirrored for the same reason `isSafeReturnUrlRule` is: a mock that returned a
 * constant would let the evil-origin test pass for the wrong reason.
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

function validateRedirectUriRule(
  uri: string,
  clientId?: string,
): Promise<{ readonly allowed: boolean }> {
  let parsed: URL;
  try {
    parsed = new URL(uri);
  } catch {
    // Not absolute -- `TryCreate(UriKind.Absolute)` fails and the endpoint says no.
    return Promise.resolve({ allowed: false });
  }

  return Promise.resolve({ allowed: allowedOriginsFor(clientId).has(parsed.origin) });
}

/**
 * What the facade really throws for this endpoint's failures. `title` stays
 * "Unknown error" — these endpoints emit no problem details, so no human-readable
 * title ever arrives and the screen must supply its own copy. `code` carries the
 * API's own token, which `readCode` now recovers from the body's `error` member
 * (Wallow-vec7.7); `status` is the response status.
 */
function rejection(status: number, code: string): Error & { status: number; code: string } {
  return Object.assign(new Error("Unknown error"), {
    name: "WallowError",
    status,
    code,
    title: "Unknown error",
  });
}

/** 401 + `invalid_code`: the code was wrong. Two of the endpoint's three 401s. */
function invalidCodeRejection(): Error & { status: number; code: string } {
  return rejection(401, "invalid_code");
}

/** 401 + `no_mfa_session`: the partial-auth cookie is missing or expired. */
function noMfaSessionRejection(): Error & { status: number; code: string } {
  return rejection(401, "no_mfa_session");
}

/** 423 + `mfa_locked_out` — the one failure status also identifies on its own. */
function lockedOutRejection(): Error & { status: number; code: string } {
  return rejection(423, "mfa_locked_out");
}

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderWithClient(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
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
  mocks.isSafeReturnUrl.mockImplementation(isSafeReturnUrlRule);
  mocks.validateRedirectUri.mockImplementation(validateRedirectUriRule);
  // Non-navigating fragment sentinel — the builder's CALL ARGS are asserted, not
  // its return value (see the navigation-seam note in the file header).
  mocks.buildExchangeTicketUrl.mockReturnValue(EXCHANGE_TICKET_SENTINEL);
  mocks.verifyMfa.mockResolvedValue({ succeeded: true, signInTicket: TICKET });
  mocks.useBackupCode.mockResolvedValue({ succeeded: true, signInTicket: TICKET });
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
    mocks.verifyMfa.mockRejectedValue(invalidCodeRejection());
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
    expect(mocks.verifyMfa).not.toHaveBeenCalled();
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
    expect(mocks.useBackupCode).not.toHaveBeenCalled();
  });

  it("treats a whitespace-only code as blank", async () => {
    // `IsNullOrWhiteSpace`, not `IsNullOrEmpty` — "   " never reaches the endpoint.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user, "   ");

    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(mocks.verifyMfa).not.toHaveBeenCalled();
  });

  it("sends the typed code to the authenticator endpoint", async () => {
    // Oracle: `await AuthClient.VerifyMfaChallengeAsync(_code)`.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.verifyMfa).toHaveBeenCalledWith(CODE);
    });
    expect(mocks.useBackupCode).not.toHaveBeenCalled();
  });

  it("sends a backup code to the backup endpoint instead", async () => {
    // Oracle: `_useBackupCode ? UseBackupCodeAsync(_code) : VerifyMfaChallengeAsync(_code)`.
    // These are the same API op with `useBackupCode: true/false` (auth-client.ts:176-179);
    // crossing them would send a recovery code to the TOTP validator.
    const user = userEvent.setup();
    await renderForm();

    await toggleToBackupCode(user);
    await submitCode(user, BACKUP_CODE);

    await vi.waitFor(() => {
      expect(mocks.useBackupCode).toHaveBeenCalledWith(BACKUP_CODE);
    });
    expect(mocks.verifyMfa).not.toHaveBeenCalled();
  });

  it("disables submit while the request is in flight", async () => {
    // Oracle: `Disabled="_isSubmitting"` — one click, one attempt. This screen is
    // rate-limited into a 5-strike lockout, so a double submit can cost the user
    // two of their five.
    let release: (value: unknown) => void = () => {};
    mocks.verifyMfa.mockReturnValue(
      new Promise((resolve) => {
        release = resolve;
      }),
    );
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-submit")).toBeDisabled();
    await expect.element(page.getByTestId("mfa-challenge-submit")).toHaveTextContent(/verifying/iu);

    release({ succeeded: true, signInTicket: TICKET });
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
    // The builder seam's ORIGIN argument (`""`) is what pins same-origin now that
    // the assigned `location.href` cannot be read in a real browser.
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
    });
  });

  it("navigates straight to the return url when the response carries no ticket", async () => {
    // Oracle: `else if (!string.IsNullOrEmpty(safeReturnUrl))` ->
    // `BuildApiReturnUrl(safeReturnUrl)`. The oracle prepends `ApiBaseUrl` there
    // too; same-origin makes that prepend the identity function, so the safe
    // relative path is navigated to verbatim. This branch has no builder seam, so
    // it is exercised with a same-page returnUrl whose assignment is observable.
    mocks.verifyMfa.mockResolvedValue({ succeeded: true });
    const user = userEvent.setup();
    const returnUrl = directReturnUrl();
    await renderForm({ returnUrl });

    await submitCode(user);

    await vi.waitFor(() => {
      expect(globalThis.location.href.endsWith(returnUrl)).toBe(true);
    });
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("treats a blank ticket as no ticket", async () => {
    // `IsNullOrEmpty(result.SignInTicket)`. `buildExchangeTicketUrl` THROWS on a
    // blank ticket ("ticket is required", auth-oidc.ts:131) — a screen that
    // called it anyway would replace the user's redirect with a crash.
    mocks.verifyMfa.mockResolvedValue({ succeeded: true, signInTicket: "" });
    const user = userEvent.setup();
    const returnUrl = directReturnUrl();
    await renderForm({ returnUrl });

    await submitCode(user);

    await vi.waitFor(() => {
      expect(globalThis.location.href.endsWith(returnUrl)).toBe(true);
    });
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("stays put on a direct sign-in with no return url", async () => {
    // The oracle's trailing comment: "No ReturnUrl — direct login, not OIDC. Show
    // success state without redirecting." A nullish returnUrl is not hostile and
    // gets no "/" fallback (bd memory `returnurl-guard-refuse-dont-sanitize`).
    const user = userEvent.setup();
    await renderForm({ returnUrl: undefined });
    const before: string = globalThis.location.href;

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-success")).toBeInTheDocument();
    expect(globalThis.location.href).toBe(before);
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
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
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
    expect(mocks.verifyMfa).not.toHaveBeenCalled();
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
    expect(mocks.verifyMfa).not.toHaveBeenCalled();
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
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, EXTERNAL_RETURN_URL);
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
    expect(mocks.validateRedirectUri).not.toHaveBeenCalled();
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
    expect(mocks.validateRedirectUri).not.toHaveBeenCalled();
  });

  it("does not render the form while the allow-list check is in flight", async () => {
    // FAIL CLOSED IN FLIGHT. A form rendered optimistically is a form a fast user
    // can submit -- burning a one-time second factor on a destination we may be
    // about to refuse, the exact cost the mount-time refusal exists to avoid.
    mocks.validateRedirectUri.mockReturnValue(new Promise(() => {}));
    await renderForm({ returnUrl: EXTERNAL_RETURN_URL });

    await vi.waitFor(() => {
      expect(mocks.validateRedirectUri).toHaveBeenCalledWith(EXTERNAL_RETURN_URL);
    });
    expect(page.getByTestId("mfa-challenge-code").query()).toBeNull();
    expect(page.getByTestId("mfa-challenge-submit").query()).toBeNull();
  });

  it("refuses when the allow-list check is unreachable", async () => {
    // The C# `!IsSuccessStatusCode -> false` arm arrives as a REJECTION (the
    // facade's `unwrap()` throws on non-2xx). An unreachable validator must never
    // become a reason to TRUST a URI.
    mocks.validateRedirectUri.mockRejectedValue(new Error("network down"));
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
      mocks.validateRedirectUri.mockResolvedValue(body);
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
 * accept-terms relay, applied to the other screen on the same journey.
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
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL, CLIENT_ID);
    });
  });

  it("scopes the allow-list probe to it", async () => {
    const user = userEvent.setup();
    await renderForm({ returnUrl: EXTERNAL_RETURN_URL, clientId: CLIENT_ID });

    await vi.waitFor(() => {
      expect(mocks.validateRedirectUri).toHaveBeenCalledWith(EXTERNAL_RETURN_URL, CLIENT_ID);
    });

    // Anchored on a positive outcome: the user still gets through on a URL their
    // own client registered. "The probe was called with two arguments" is
    // satisfied by a screen that then refuses everybody.
    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);
    await vi.waitFor(() => {
      expect(mocks.verifyMfa).toHaveBeenCalledWith(CODE);
    });
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
    expect(mocks.verifyMfa).not.toHaveBeenCalled();
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
      expect(mocks.verifyMfa).toHaveBeenCalledWith(CODE);
    });
    expect(mocks.navigate).not.toHaveBeenCalledWith(expect.objectContaining({ href: ERROR_HREF }));
  });

  it("sends no client id at all when the flow carries a blank one", async () => {
    // A present-but-blank id is not a client. The endpoint fails an unknown
    // client CLOSED to the AuthUrl-only origin set, so relaying "" would refuse
    // the very returnUrl the user is mid-journey to, where sending nothing falls
    // back to the behaviour that works today. Pinned on the exact ARITY, which
    // is also what stops an unconditional `undefined` fourth argument.
    const user = userEvent.setup();
    await renderForm({ clientId: "" });

    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
    });
  });
});

describe("MfaChallengeForm — a rejected code", () => {
  it("reports an invalid verification code on invalid_code", async () => {
    // The oracle's `"invalid_code" =>` branch, reached via the token the API
    // really sends now that the seam surfaces it (Wallow-vec7.7).
    mocks.verifyMfa.mockRejectedValue(invalidCodeRejection());
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect
      .element(page.getByTestId("mfa-challenge-error"))
      .toHaveTextContent(/invalid verification code/iu);
  });

  it("names the backup code when a backup code is rejected", async () => {
    // The oracle's invalid_code branch is mode-sensitive: "Invalid backup code."
    mocks.useBackupCode.mockRejectedValue(invalidCodeRejection());
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
    mocks.verifyMfa.mockRejectedValue(noMfaSessionRejection());
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
    mocks.useBackupCode.mockRejectedValue(noMfaSessionRejection());
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
    mocks.verifyMfa.mockRejectedValue(lockedOutRejection());
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
    mocks.verifyMfa.mockRejectedValue(rejection(423, "UNKNOWN"));
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-error")).toHaveTextContent(/locked/iu);
  });

  it("falls back to the generic message for an unrecognised status", async () => {
    // The oracle's `_ =>` tail. A 500 is not a wrong code and must not be
    // reported as one.
    mocks.verifyMfa.mockRejectedValue(rejection(500, "UNKNOWN"));
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
    mocks.verifyMfa.mockRejectedValue(rejection(401, "some_new_token"));
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);

    const error = page.getByTestId("mfa-challenge-error");
    await expect.element(error).toHaveTextContent(/verification failed/iu);
    await expect.element(error).not.toHaveTextContent(/invalid verification code/iu);
  });

  it("shows the generic message when the request fails without a status", async () => {
    // A network-level rejection has no `status` at all; structural narrowing must
    // not throw on it, and must not claim the code was wrong.
    mocks.verifyMfa.mockRejectedValue(new Error("network down"));
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
    mocks.verifyMfa.mockRejectedValue(invalidCodeRejection());
    const user = userEvent.setup();
    await renderForm();

    await submitCode(user);
    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(page.getByText(/unknown error/iu).query()).toBeNull();
    expect(page.getByText(/no_mfa_session|mfa_locked_out|invalid_code/u).query()).toBeNull();

    // The code the endpoint sends when the session is gone is the one an
    // implementation could most plausibly print: it is the branch with no
    // pre-existing oracle copy behind it.
    mocks.verifyMfa.mockRejectedValue(noMfaSessionRejection());
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
    mocks.verifyMfa.mockRejectedValue(invalidCodeRejection());
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
    mocks.verifyMfa.mockRejectedValue(invalidCodeRejection());
    const user = userEvent.setup();
    await renderForm();
    const before: string = globalThis.location.href;

    await submitCode(user);

    await expect.element(page.getByTestId("mfa-challenge-error")).toBeInTheDocument();
    expect(globalThis.location.href).toBe(before);
    expect(mocks.buildExchangeTicketUrl).not.toHaveBeenCalled();
  });

  it("clears a previous error when the next attempt succeeds", async () => {
    // Oracle: `_errorMessage = null;` at the top of `HandleVerify`. A stale
    // "invalid code" banner above a successful verification would be a lie.
    mocks.verifyMfa.mockRejectedValueOnce(invalidCodeRejection());
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
  const rootRoute = createRootRoute({ component: Outlet });
  const routeTree = rootRoute.addChildren([
    mfaChallengeRoute.update({
      id: "/mfa/challenge",
      path: "/mfa/challenge",
      getParentRoute: () => rootRoute,
    } as any),
  ]);
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [url] }),
  });

  return renderWithClient(<RouterProvider router={router} />);
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
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
    });
  });

  it("threads client_id out of the callback redirect into the exchange", async () => {
    // The relay end to end at the route level: the redirect
    // `external-login-callback` issues goes in, the arguments the exchange
    // builder receives come out, and the snake_case -> camelCase hop happens in
    // between. `validateSearch` has to widen for `client_id` or the id stops at
    // the router.
    const user = userEvent.setup();
    await renderRouteAt(
      `/mfa/challenge?returnUrl=${encodeURIComponent(RETURN_URL)}` +
        `&client_id=${encodeURIComponent(CLIENT_ID)}`,
    );

    await expect.element(page.getByTestId("mfa-challenge-code")).toBeInTheDocument();
    await submitCode(user);

    await vi.waitFor(() => {
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL, CLIENT_ID);
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
      expect(mocks.buildExchangeTicketUrl).toHaveBeenCalledWith("", TICKET, RETURN_URL);
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
