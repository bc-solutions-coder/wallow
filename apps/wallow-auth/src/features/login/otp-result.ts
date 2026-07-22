/**
 * The OTP tab's RESULT LAYER (Wallow-vec7.3.13 / 2.8c): everything that turns an
 * untyped `auth.sendOtp` / `auth.verifyOtp` response into user-facing copy, with
 * no React and no SDK in it.
 *
 * WHY A SEPARATE MODULE FROM `./auth-result`. The oracle keeps ONE
 * `HandleSuccessfulAuth` and a DIFFERENT error switch per tab
 * (`api/src/Wallow.Auth/Components/Pages/Login.razor`). Wallow-vec7.3.11 split it
 * the same way and `.3.12` followed: the shared branch table lives once in
 * `./auth-result`, and each panel owns its own error switch. This is the OTP tab's
 * switch. It sits beside `./magic-link-result` rather than inside it, and IMPORTS
 * the shared copy rather than restating it.
 *
 * ── THE WIRE, READ FROM THE CONTROLLER (not from a client DTO) ───────────────
 *
 * `AccountController` (api/.../Identity/Wallow.Identity.Api/Controllers/AccountController.cs):
 *
 *   POST /v1/identity/auth/passwordless/otp                               :852
 *     200 { succeeded: true }                                             :863
 *     400 { succeeded: false, error: "Rate limit exceeded. Please try again later." }
 *
 *   POST /v1/identity/auth/passwordless/otp/verify                        :866
 *     200 { succeeded: true, email, signInTicket }                        :876
 *     401 { succeeded: false, error: "Code expired or not found." }       PasswordlessService.cs:166
 *     401 { succeeded: false, error: "Invalid code." }                    PasswordlessService.cs:174
 *
 * As on magic-link, EVERY failure is a non-2xx, so `unwrap()` throws and the
 * oracle's `else` arms are reached through a REJECTION. As of Wallow-vec7.7
 * `readCode` probes `extensions.code > code > error`, so the `error` member of the
 * bare `{ succeeded, error }` body arrives as `WallowError.code`.
 *
 * ── THE TOKENS ARE ENGLISH SENTENCES, AND THAT CHANGES NOTHING ───────────────
 *
 * A server-authored sentence is still a MACHINE TOKEN: it is matched against and
 * never rendered. The oracle's `_ => result.Error` leak is not reproduced; the copy
 * below is the screen's own.
 */

import { GENERIC_MESSAGE, readMember, UNREACHABLE_MESSAGE } from "./auth-result";

/**
 * The oracle's blank-input guard (Login.razor:436) — note WHITEspace.
 *
 * Byte-identical to `./magic-link-result`'s `BLANK_EMAIL_MESSAGE`, and deliberately
 * NOT shared with it: they are two independent literals in the oracle
 * (`HandleSendMagicLink` :376 and `HandleSendOtp` :436), and hoisting them into one
 * constant would mean re-wording one tab's guard silently re-worded the other's.
 * Identical copy is not the same fact as shared copy.
 */
export const OTP_BLANK_EMAIL_MESSAGE = "Please enter your email.";

/** The oracle's second blank-input guard (Login.razor:471), on the code form. */
export const OTP_BLANK_CODE_MESSAGE = "Please enter the verification code.";

/**
 * DIVERGENCE (disclosed on the bead). The oracle shows its GENERIC copy for every
 * send failure — but the rate limit is the ONLY failure `SendOtpAsync` can produce
 * (`PasswordlessService.cs:128-132`; an address with no account returns SUCCESS, to
 * defeat enumeration). "An error occurred. Please try again." therefore tells a
 * rate-limited user to do the one thing guaranteed not to work.
 *
 * This is the same call `.3.11` made keeping a 423 status fallback under
 * `loginFailureMessage`, and the same one `.3.12` made on the magic-link send.
 */
export const OTP_RATE_LIMITED_MESSAGE =
  "Too many code requests. Please wait a few minutes and try again.";

/**
 * The oracle's `HandleVerifyOtp` switch copy (Login.razor:484).
 *
 * The COPY is ported; the oracle's `"invalid_code"` KEY is not, because it is DEAD
 * — `ValidateOtpAsync` never returns it. The copy
 * survives the dead key because it already covers both live tokens: "Invalid OR
 * EXPIRED code" is precisely the union of `"Invalid code."` and `"Code expired or
 * not found."`.
 */
export const OTP_INVALID_CODE_MESSAGE = "Invalid or expired code. Please try again.";

/** `SendOtpAsync`'s only failure token (PasswordlessService.cs:131). */
const RATE_LIMITED_TOKEN = "Rate limit exceeded. Please try again later.";

/** The status `ValidateOtpAsync`'s failures ride on (AccountController.cs:872). */
const UNAUTHORIZED_STATUS = 401;

/**
 * Did the API actually accept the send? The facade types this `Promise<unknown>`
 * (the C# endpoint returns an anonymous `Ok(new { … })` with no OpenAPI schema), so
 * the narrowing belongs here, at the boundary (bd memory
 * `untyped-sdk-response-fail-closed-pattern-wallow-auth`).
 *
 * STRICT `=== true`, reproducing C#'s `if (result.Succeeded)`: JS truthiness would
 * accept the string `"false"` and march the user to a code form for a code that was
 * never sent.
 */
export function otpWasSent(body: unknown): boolean {
  return readMember(body, "succeeded") === true;
}

/**
 * The oracle's `HandleSendOtp` failure arms (Login.razor:449-456), reached through
 * a REJECTION rather than an `else` — see the module header.
 *
 * CODE-KEYED, unlike `verifyOtpFailureMessage` below. The rate-limit copy is a
 * divergence written for one specific failure; handing it to some future unrelated
 * 400 would tell a user to wait out a limit they never hit.
 */
export function sendOtpFailureMessage(cause: unknown): string {
  if (readMember(cause, "code") === RATE_LIMITED_TOKEN) {
    return OTP_RATE_LIMITED_MESSAGE;
  }

  // A network-level rejection carries NEITHER `code` NOR `status`, and that absence
  // is exactly what identifies it: the TS shape of the oracle's
  // `catch (HttpRequestException)`, which it keeps DISTINCT from its generic tail.
  if (readMember(cause, "status") === undefined) {
    return UNREACHABLE_MESSAGE;
  }

  return GENERIC_MESSAGE;
}

/**
 * The oracle's `HandleVerifyOtp` switch (Login.razor:482-486).
 *
 * ── NO CODE MAP HERE, AND THAT IS THE DELIBERATE OPPOSITE OF `.3.12` ─────────
 *
 * `verifyMagicLinkFailureMessage` keys on tokens because its 401 carries THREE with
 * TWO meanings. This endpoint's 401 carries two tokens with ONE meaning — "that code
 * did not work, try another" — so a code-keyed map here would be:
 *
 *   1. UNBINDABLE. It is observationally identical to this status rule for every
 *      input the API can produce, so no honest test could tell them apart (bd memory
 *      `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-bind`). `.3.11`
 *      declined to fake such a test on `login`; so does this.
 *   2. WORSE. An unrecognised token on a 401 would fall to the generic tail and tell
 *      a user with a mistyped code that "an error occurred" — hiding the retry that
 *      is the entire remedy.
 *
 * 401 identifies this failure ALONE, which is exactly the condition under which bd
 * memory `wallow-auth-screens-key-error-copy-on-wallowerror-code-not-http-status`
 * keeps a status rule. The generic tail is therefore reached only by a status that
 * is NOT 401 — a 500 is not a bad code and must not be reported as one.
 */
export function verifyOtpFailureMessage(cause: unknown): string {
  const status: unknown = readMember(cause, "status");

  // Checked FIRST: a network rejection has no status at all, and must not fall
  // through to a status comparison that would read `undefined !== 401` as "generic".
  if (status === undefined) {
    return UNREACHABLE_MESSAGE;
  }

  if (status === UNAUTHORIZED_STATUS) {
    return OTP_INVALID_CODE_MESSAGE;
  }

  return GENERIC_MESSAGE;
}
