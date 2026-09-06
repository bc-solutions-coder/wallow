import { asString } from "@bc-solutions-coder/utils/guards";
import { ERROR_HREF, decideReturnUrl } from "@shared/lib/return-url";
/**
 * The login screen's RESULT LAYER (Wallow-vec7.3.11 / 2.8a): everything that
 * turns an untyped `auth.*` response into a decision, with no React and no SDK
 * in it.
 *
 * This module is the port of the oracle's `HandleSuccessfulAuth`
 * (`api/src/Wallow.Auth/Components/Pages/Login.razor`:502-560) plus its
 * `result.Error` and `Error`-query-param switches. It is deliberately PURE —
 * no SDK client, no `useNavigate`, no `globalThis.location` — so that
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
 *     401 problem Auth.InvalidCredentials
 *     423 problem Auth.LockedOut
 *     403 problem Auth.EmailNotConfirmed
 *
 * So `unwrap()` does NOT throw for the MFA branches — unlike `mfa/verify`, where
 * every failure is a rejection. The rejections are not mapped here at all: the
 * shell hands the thrown `ApiFailure` to `useFailureMessage`, which reads the
 * catalog's `detail`. The facade types `login` as `Promise<unknown>`
 * (the C# endpoint returns an anonymous `Ok(new { … })` with no OpenAPI schema),
 * so the narrowing is owned HERE, at this boundary, per bd memory
 * `untyped-sdk-response-fail-closed-pattern-wallow-auth`: structural `in`-style
 * probes, no cast (the repo forbids `as any`), and C#'s STRICT `== true` rather
 * than JS truthiness — which would happily accept `succeeded: "false"`.
 */

/**
 * The oracle's `_ =>` tail. Also the FAIL-CLOSED answer for a 200 body this
 * screen cannot make sense of: a garbage body is not a sign-in.
 */
export const GENERIC_MESSAGE = "An error occurred. Please try again.";

/** The oracle's `Error` query-param switch (Login.razor:268-273). */
const EXTERNAL_LOGIN_FAILED_MESSAGE =
  "External sign-in failed. Please try again or use a different method.";
const SESSION_EXPIRED_MESSAGE = "Your session has expired. Please try again.";

/** The oracle's blank-input guard (Login.razor:327). */
export const BLANK_CREDENTIALS_MESSAGE = "Please enter your email and password.";

/** In-app destinations. Constant paths — see the guard note on `authDispositionOf`. */
const MFA_CHALLENGE_PATH = "/mfa/challenge";
const MFA_ENROLL_PATH = "/mfa/enroll";

/**
 * Read a member off an unknown value without asserting its shape.
 *
 * The membership test is `in` rather than a truthiness check: these endpoints
 * answer `{ succeeded: false }`, and a real `false` has to stay distinguishable
 * from an absent member. Exported for the sibling result modules, which narrow
 * their own success bodies the same way.
 */
export function readMember(value: unknown, name: string): unknown {
  if (typeof value !== "object" || value === null || !(name in value)) {
    return undefined;
  }

  return (value as Record<string, unknown>)[name];
}

/** A member that is only meaningful as a string; anything else reads as absent. */
function readString(value: unknown, name: string): string | undefined {
  return asString(readMember(value, name));
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
 * by the passthrough reverse proxy and not by the client-side route tree, which would 404
 * in-app.
 */
type AuthOutcome =
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
 * `decideReturnUrl` is consulted on the TICKET path ONLY, and that asymmetry is
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
 * The mode is `"empty-ok"` — the oracle's `IsNullOrEmpty` parity, where `""`
 * means "no destination" and lands on the signed-in banner, never `/error`.
 * The empty-before-safety ordering that used to live inline here is owned by
 * `decideReturnUrl` itself now; see `@shared/lib/return-url`.
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
 */
export function authDispositionOf(body: unknown, returnUrl: string | undefined): AuthDisposition {
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

  const destination = decideReturnUrl(returnUrl, "empty-ok");

  if (destination.verdict === "absent") {
    // The oracle's trailing `else`: nowhere to send the user, so say so rather
    // than inventing a destination. No `"/"` fallback.
    return { outcome: { kind: "signed-in" }, graceDeadline };
  }

  if (destination.verdict === "refuse") {
    // REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`).
    return { outcome: { kind: "navigate", href: ERROR_HREF }, graceDeadline };
  }

  const ticket: string | undefined = result.signInTicket;

  if (ticket === undefined || ticket === "") {
    // The dead `BuildApiReturnUrl` arm. `buildExchangeTicketUrl` THROWS on a blank
    // ticket ("ticket is required", auth-oidc.ts:131), so there is nothing to build.
    return { outcome: { kind: "failed", message: GENERIC_MESSAGE }, graceDeadline };
  }

  return {
    outcome: { kind: "exchange-ticket", ticket, returnUrl: destination.returnUrl },
    graceDeadline,
  };
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

/**
 * The one `message` query token the login screen acknowledges (Wallow-xzha.1.2).
 * `ResetPasswordForm` navigates to `/login?message=password_reset` after a
 * completed reset; the screen renders a one-line success banner for it.
 *
 * Exported so the route's `validateSearch` threads ONLY this literal through and
 * drops every other value, and the screen gates its banner on the SAME token — the
 * attacker-safe known-token discipline `errorParamMessage` follows. `?message=` is
 * a URL anyone can construct, so a bare `===` against the one recognised literal
 * is the whole allow-list: no arbitrary attacker string ever becomes a prop or
 * reaches the DOM.
 */
export const PASSWORD_RESET_MESSAGE = "password_reset";

export function isPasswordResetMessage(message: string | undefined): boolean {
  return message === PASSWORD_RESET_MESSAGE;
}
