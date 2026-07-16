/**
 * The login screen's RESULT LAYER (Wallow-vec7.3.11 / 2.8a): everything that
 * turns an untyped `auth.*` response into a decision, with no React and no SDK
 * in it.
 *
 * This module is the port of the oracle's `HandleSuccessfulAuth`
 * (`api/src/Wallow.Auth/Components/Pages/Login.razor`:502-560) plus its
 * `result.Error` and `Error`-query-param switches. It is deliberately PURE —
 * no `getWallowAuthSdk`, no `useNavigate`, no `globalThis.location` — so that
 * `.3.12` (magic-link), `.3.13` (OTP) and `.3.15` (MFA hand-off) can IMPORT the
 * navigation decision rather than re-derive it. All three of those endpoints
 * hand back the same `AuthResponse` shape, and three copies of this branch table
 * would be three chances to disagree about where a half-authenticated user goes.
 *
 * ── THE FOUR BRANCHES ARE 200s, NOT REJECTIONS ───────────────────────────────
 *
 * `AccountController.Login` (api/.../Controllers/AccountController.cs:65-165)
 * reports THREE of its four outcomes inside a SUCCESSFUL response body:
 *
 *     200 { succeeded: false, mfaRequired: true }                       :100
 *     200 { succeeded: false, mfaEnrollmentRequired: true }             :125  (grace expired)
 *     200 { succeeded: true, mfaEnrollmentRequired: true,
 *           mfaGraceDeadline: <DateTimeOffset>, signInTicket: <t> }     :118  (in grace)
 *     200 { succeeded: true, signInTicket: <t> }                        :138
 *     401 { succeeded: false, error: "invalid_credentials" }            :83, :164
 *     423 { succeeded: false, error: "locked_out" }                     :149
 *     403 { succeeded: false, error: "email_not_confirmed" }            :154
 *
 * So `unwrap()` does NOT throw for the MFA branches — unlike `mfa/verify`, where
 * every failure is a rejection. The facade types `login` as `Promise<unknown>`
 * (the C# endpoint returns an anonymous `Ok(new { … })` with no OpenAPI schema),
 * so the narrowing is owned HERE, at this boundary, per bd memory
 * `untyped-sdk-response-fail-closed-pattern-wallow-auth`: structural `in`-style
 * probes, no cast (the repo forbids `as any`), and C#'s STRICT `== true` rather
 * than JS truthiness — which would happily accept `succeeded: "false"`.
 */

/** The oracle's `result.Error` switch (Login.razor:345-350), minus its raw-token tail. */
export const INVALID_CREDENTIALS_MESSAGE = "Invalid email or password.";
export const LOCKED_OUT_MESSAGE = "Account locked. Try again later.";
export const EMAIL_NOT_CONFIRMED_MESSAGE = "Please verify your email before signing in.";

/**
 * The oracle's `_ =>` tail. Also the FAIL-CLOSED answer for a 200 body this
 * screen cannot make sense of: a garbage body is not a sign-in.
 */
export const GENERIC_MESSAGE = "An error occurred. Please try again.";

/**
 * The oracle's `catch (HttpRequestException)` arm (Login.razor:355), kept
 * DISTINCT from the generic tail: "the server said no" and "the server never
 * answered" are different instructions to the user, and collapsing them tells a
 * user with no network to go re-read their password.
 */
export const UNREACHABLE_MESSAGE = "Unable to reach the server. Please try again later.";

/** The oracle's `Error` query-param switch (Login.razor:268-273). */
export const EXTERNAL_LOGIN_FAILED_MESSAGE =
  "External sign-in failed. Please try again or use a different method.";
export const SESSION_EXPIRED_MESSAGE = "Your session has expired. Please try again.";

/** The oracle's blank-input guard (Login.razor:327). */
export const BLANK_CREDENTIALS_MESSAGE = "Please enter your email and password.";

/** This endpoint's machine tokens. Matched against, NEVER rendered. */
const INVALID_CREDENTIALS = "invalid_credentials";
const LOCKED_OUT = "locked_out";
const EMAIL_NOT_CONFIRMED = "email_not_confirmed";

/**
 * The statuses those tokens ride on, retained as a FALLBACK beneath them per the
 * Wallow-vec7.7 rule (match known tokens FIRST, keep HTTP status underneath).
 *
 * NOTE FOR REVIEWERS — code-keying is NOT observable on this endpoint. Unlike
 * `mfa/verify` (two meanings on one 401), each failure status here carries
 * exactly ONE token, so a code-keyed and a status-keyed map are observationally
 * IDENTICAL for every input the API can produce. The fallback earns its place on
 * the UNKNOWN token: a token the screen has never heard of on a 423 still means
 * a locked-out user, and dropping them to the generic "try again" would tell
 * them to retype a password that cannot possibly work.
 */
const UNAUTHORIZED_STATUS = 401;
const FORBIDDEN_STATUS = 403;
const LOCKED_STATUS = 423;

/** In-app destinations. Constant paths — see the guard note on `authDispositionOf`. */
const MFA_CHALLENGE_PATH = "/mfa/challenge";
const MFA_ENROLL_PATH = "/mfa/enroll";

/** The bail target for an unsafe returnUrl, matching the Consent/MfaChallenge ports. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/**
 * Read a member off an unknown value without asserting its shape.
 *
 * Exported (Wallow-vec7.3.12) so each tab's own result module — `./magic-link-result`,
 * and `./otp-result` when `.3.13` lands — narrows its untyped bodies and rejections
 * the SAME way rather than each rolling a slightly laxer probe of its own. It is the
 * only member of this module's private narrowing helpers that is shared.
 */
export function readMember(value: unknown, name: string): unknown {
  if (typeof value !== "object" || value === null || !(name in value)) {
    return undefined;
  }

  return (value as Record<string, unknown>)[name];
}

/** A member that is only meaningful as a string; anything else reads as absent. */
function readString(value: unknown, name: string): string | undefined {
  const member: unknown = readMember(value, name);

  return typeof member === "string" ? member : undefined;
}

/**
 * The `AuthResponse` members this screen acts on, already narrowed. The three
 * flags are `boolean` because they have been compared STRICTLY to `true` — the
 * only place `succeeded: "false"` can be rejected is at the point of narrowing.
 */
interface LoginResult {
  readonly succeeded: boolean;
  readonly mfaRequired: boolean;
  readonly mfaEnrollmentRequired: boolean;
  /** `WallowUser.MfaGraceDeadline` as an ISO-8601 `DateTimeOffset`. */
  readonly mfaGraceDeadline?: string;
  readonly signInTicket?: string;
}

/** `null` when the body is not an object at all — the fail-closed tail. */
function narrowLoginResult(body: unknown): LoginResult | null {
  if (typeof body !== "object" || body === null) {
    return null;
  }

  return {
    succeeded: readMember(body, "succeeded") === true,
    mfaRequired: readMember(body, "mfaRequired") === true,
    mfaEnrollmentRequired: readMember(body, "mfaEnrollmentRequired") === true,
    mfaGraceDeadline: readString(body, "mfaGraceDeadline"),
    signInTicket: readString(body, "signInTicket"),
  };
}

/**
 * The oracle's `result.MfaGraceDeadline.HasValue && result.MfaGraceDeadline.Value
 * > DateTimeOffset.UtcNow` — a COMPARISON, not a presence check. Reading the
 * deadline as merely "present" would strand a user whose grace expired on the
 * login page behind a banner, instead of enrolling them.
 *
 * An unparseable deadline fails closed to "not within grace": the safe direction
 * is enrollment, not an indefinite pass.
 */
function isWithinGracePeriod(deadline: string | undefined): boolean {
  if (deadline === undefined) {
    return false;
  }

  const deadlineMs: number = Date.parse(deadline);

  return !Number.isNaN(deadlineMs) && deadlineMs > Date.now();
}

/**
 * `returnUrl` as query CARGO on a constant in-app path. `encodeURIComponent` is
 * what a DEFERRED guard still owes (see `authDispositionOf`): the value must land
 * as ONE query value. Raw interpolation would let a returnUrl containing
 * `&cookieRelay=…` split into a second key, and ASP.NET binds a duplicated
 * `[FromQuery]` as `"a,b"` — a parse failure that silently takes the wrong branch.
 */
function handOffHref(path: string, returnUrl: string | undefined): string {
  if (returnUrl === undefined || returnUrl === "") {
    return path;
  }

  return `${path}?returnUrl=${encodeURIComponent(returnUrl)}`;
}

/**
 * What the screen must DO about an auth response.
 *
 * `navigate` is the client router (`useNavigate`); `exchange-ticket` is a FULL
 * navigation (`globalThis.location.href`), because the exchange endpoint is served
 * by the h3 reverse proxy and not by the client-side route tree, which would 404
 * in-app.
 */
export type AuthOutcome =
  | { readonly kind: "navigate"; readonly href: string }
  | { readonly kind: "exchange-ticket"; readonly ticket: string; readonly returnUrl: string }
  | { readonly kind: "signed-in" }
  | { readonly kind: "failed"; readonly message: string };

export interface AuthDisposition {
  readonly outcome: AuthOutcome;
  /**
   * The oracle's `_showMfaEnrollmentBanner` + `_mfaGraceDeadline`, collapsed into
   * one field: within-grace REQUIRES a deadline (`HasValue &&`), so a non-null
   * deadline and a visible banner are the same fact, and two fields could only
   * ever disagree.
   */
  readonly graceDeadline: string | null;
}

/**
 * The port of `HandleSuccessfulAuth` + the `HandleLogin` gate that feeds it
 * (`if (MfaRequired || MfaEnrollmentRequired) … else if (Succeeded) … else error`).
 *
 * ── GUARD PLACEMENT — both poles, and why they differ ─────────────────────────
 *
 * `returnUrlIsSafe` is consulted on the TICKET path ONLY, and that asymmetry is
 * the whole lesson of `.3.6`/`.3.17` (bd memory `guard-where-the-client-picks-…`):
 *
 *   TICKET PATH — the CLIENT picks the destination (`location.href` is built from
 *     `returnUrl`), so the guard belongs here. Its premise holds: this returnUrl
 *     is relative BY CONSTRUCTION — `AuthorizationController.Authorize` builds it
 *     as `Request.PathBase + Request.Path + Request.QueryString` (:53), rejects it
 *     unless `Url.IsLocalUrl` (:62), and only then redirects to
 *     `{authUrl}/login?returnUrl=…` (:67). It is disjoint from the ABSOLUTE,
 *     allow-listed returnUrls `ExternalLoginCallback` sends.
 *
 *   MFA PATH — the destination is a CONSTANT in-app path and `returnUrl` is inert
 *     cargo that `/mfa/challenge` re-guards on arrival (shape-aware, post-`.3.17`).
 *     Guarding here would refuse 100% of external-login traffic: a total outage,
 *     not a security feature. What is owed instead is INJECTION, which
 *     `handOffHref` pays.
 *
 * ── ORDER IS LOAD-BEARING ────────────────────────────────────────────────────
 *
 * EMPTINESS IS CHECKED BEFORE SAFETY, mirroring the oracle's
 * `if (!string.IsNullOrEmpty(ReturnUrl))` wrapping its `IsSafe` call. `""` is NOT
 * nullish and IS unsafe by `isSafeReturnUrl`, so a screen that guarded first
 * would route a perfectly ordinary direct sign-in to `/error`.
 *
 * ── THE DEAD BRANCH ──────────────────────────────────────────────────────────
 *
 * The oracle's `BuildApiReturnUrl` arm (returnUrl present, NO ticket) is
 * UNREACHABLE: every `succeeded: true` response carries a `signInTicket`, and both
 * `mfa*` branches return early. It is not ported — a ticketless body falls to the
 * fail-closed tail rather than being navigated somewhere on a guess.
 *
 * @param body The untyped `auth.login` (or `verifyMagicLink`/`verifyOtp`) response.
 * @param returnUrl The OIDC returnUrl threaded through the login link.
 * @param returnUrlIsSafe `oidc.isSafeReturnUrl(returnUrl)`, passed in already
 *   computed so this module stays free of the SDK facade — and so the method is
 *   never called unbound.
 */
export function authDispositionOf(
  body: unknown,
  returnUrl: string | undefined,
  returnUrlIsSafe: boolean,
): AuthDisposition {
  const result: LoginResult | null = narrowLoginResult(body);

  if (result === null) {
    return { outcome: { kind: "failed", message: GENERIC_MESSAGE }, graceDeadline: null };
  }

  if (result.mfaRequired) {
    return {
      outcome: { kind: "navigate", href: handOffHref(MFA_CHALLENGE_PATH, returnUrl) },
      graceDeadline: null,
    };
  }

  let graceDeadline: string | null = null;

  if (result.mfaEnrollmentRequired) {
    if (!isWithinGracePeriod(result.mfaGraceDeadline)) {
      return {
        outcome: { kind: "navigate", href: handOffHref(MFA_ENROLL_PATH, returnUrl) },
        graceDeadline: null,
      };
    }

    // Within grace: raise the banner and FALL THROUGH to the returnUrl block, so
    // the user keeps signing in. Grace does not short-circuit the hand-off.
    graceDeadline = result.mfaGraceDeadline ?? null;
  } else if (!result.succeeded) {
    // The oracle's `else` arm: neither MFA flag, and not succeeded. The rejected
    // logins reach the screen as REJECTIONS (see `loginFailureMessage`), so this
    // is the fail-closed tail for a 200 that claims neither.
    return { outcome: { kind: "failed", message: GENERIC_MESSAGE }, graceDeadline: null };
  }

  if (returnUrl === undefined || returnUrl === "") {
    // The oracle's trailing `else`: nowhere to send the user, so say so rather
    // than inventing a destination. No `"/"` fallback.
    return { outcome: { kind: "signed-in" }, graceDeadline };
  }

  if (!returnUrlIsSafe) {
    // REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`).
    return { outcome: { kind: "navigate", href: ERROR_HREF }, graceDeadline };
  }

  const ticket: string | undefined = result.signInTicket;

  if (ticket === undefined || ticket === "") {
    // The dead `BuildApiReturnUrl` arm. `buildExchangeTicketUrl` THROWS on a blank
    // ticket ("ticket is required", auth-oidc.ts:131), so there is nothing to build.
    return { outcome: { kind: "failed", message: GENERIC_MESSAGE }, graceDeadline };
  }

  return { outcome: { kind: "exchange-ticket", ticket, returnUrl }, graceDeadline };
}

/**
 * Map a REJECTION onto user-facing copy — the 401/423/403 arms, plus the
 * network arm.
 *
 * As of Wallow-vec7.7 `readCode` probes `extensions.code > code > error`, so the
 * `error` member of this endpoint's bare `{ succeeded, error }` body reaches the
 * screen as `WallowError.code`. Narrowing is STRUCTURAL rather than
 * `instanceof WallowError`: that class is exported from the SDK's `./server`
 * entry, and screens may not import the SDK at all.
 *
 * A network-level rejection carries NEITHER `code` NOR `status` — that absence is
 * exactly what identifies it, and it is the TS shape of the oracle's
 * `catch (HttpRequestException)`.
 */
export function loginFailureMessage(cause: unknown): string {
  const code: unknown = readMember(cause, "code");

  if (code === INVALID_CREDENTIALS) {
    return INVALID_CREDENTIALS_MESSAGE;
  }

  if (code === LOCKED_OUT) {
    return LOCKED_OUT_MESSAGE;
  }

  if (code === EMAIL_NOT_CONFIRMED) {
    return EMAIL_NOT_CONFIRMED_MESSAGE;
  }

  const status: unknown = readMember(cause, "status");

  if (status === undefined) {
    return UNREACHABLE_MESSAGE;
  }

  if (status === LOCKED_STATUS) {
    return LOCKED_OUT_MESSAGE;
  }

  if (status === UNAUTHORIZED_STATUS) {
    return INVALID_CREDENTIALS_MESSAGE;
  }

  if (status === FORBIDDEN_STATUS) {
    return EMAIL_NOT_CONFIRMED_MESSAGE;
  }

  // `code` is a machine token and is never rendered: the oracle's `_ => result.Error`
  // tail leaks the raw string to the user, and that leak is not ported.
  return GENERIC_MESSAGE;
}

/**
 * The oracle's `Error` query-param switch (`OnInitialized`, Login.razor:264-275).
 *
 * A `ReadonlyMap` + `.get()`, NOT a `Record` + bracket lookup — and this is not a
 * style preference. `?error=` is a URL ANYONE can construct and send a victim; an
 * object literal resolves INHERITED keys, so `?error=toString` would hand
 * `Object.prototype.toString` — a FUNCTION — to the renderer (bd memory
 * `attacker-supplied-query-key-lookups-use-map-not-record`). A Map sees only the
 * keys explicitly put in it.
 */
const ERROR_PARAM_MESSAGES: ReadonlyMap<string, string> = new Map([
  ["external_login_failed", EXTERNAL_LOGIN_FAILED_MESSAGE],
  ["session_expired", SESSION_EXPIRED_MESSAGE],
]);

/** `null` when the link carries no `error` — the oracle's `!IsNullOrEmpty(Error)`. */
export function errorParamMessage(error: string | undefined): string | null {
  if (error === undefined || error === "") {
    return null;
  }

  return ERROR_PARAM_MESSAGES.get(error) ?? GENERIC_MESSAGE;
}
