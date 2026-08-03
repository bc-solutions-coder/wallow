/**
 * The MfaEnroll screen's RESULT LAYER: everything that turns a rejection from one
 * of the three enrollment endpoints into user-facing copy, with no React in it.
 *
 * `MfaController` (api/.../Controllers/MfaController.cs:57-120) fails only with
 * non-2xx bodies of the bare `{ succeeded: false, error }` shape (NOT problem
 * details):
 *
 *   enroll/totp            401 "no_auth_session"
 *   enroll/confirm         401 "no_auth_session" | 400 "invalid_code"
 *                        | 400 "user_not_found"  | 400 "update_failed"
 *   enroll/exchange-token  400 "invalid_or_expired_token"
 *
 * Every failure is non-2xx, so `unwrap()` throws and a `succeeded: false` body
 * NEVER arrives as data — the oracle's `if (result.Succeeded) … else switch` is
 * unreachable through this seam, and a RESOLVED call always means success.
 *
 * `toWallowError()`'s `readCode` probes `extensions.code > code > error`, so the
 * `error` member of that anon body arrives as `WallowError.code`. HTTP status is
 * kept as a FALLBACK because `code` is not a guaranteed-stable token (bd memory
 * `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-bind`), and here
 * the statuses are unambiguous enough to carry it.
 *
 * Both wart and divergence from the oracle:
 *
 *   - The API's error tail can render `result.Error` RAW, exposing the literal
 *     "update_failed". `code` is a machine token and is never rendered: it is
 *     matched against KNOWN values and anything else falls to the generic
 *     message rather than guessing.
 *   - `no_auth_session` gets a SIGN-IN message, not the oracle's "try again".
 *     That tail loops the user forever — no number of retries mints a cookie.
 *   - `user_not_found`/`update_failed` get the generic message rather than the
 *     status fallback's "invalid code": telling a user whose WRITE failed to
 *     retype a correct code is the same infinite loop in miniature.
 *
 * Narrowing is STRUCTURAL rather than `instanceof WallowError`, because that
 * class is exported from the SDK's `./server` entry and screens may not import
 * the SDK at all. A network-level rejection carries neither `code` nor `status`
 * and must fall through to the generic message rather than throw.
 */

import { readErrorCode, readMember } from "@shared/lib/error-code";

/** The oracle's `HandleStartEnroll` failure copy. */
const START_FAILED_MESSAGE = "Failed to start MFA enrollment. Please try again.";

/** The oracle's `"invalid_code" =>` branch. */
const INVALID_CODE_MESSAGE = "Invalid verification code. Please try again.";

/** The oracle's `_ =>` tail, minus its raw-string leak. */
const CONFIRM_FAILED_MESSAGE = "Failed to confirm MFA enrollment. Please try again.";

/**
 * `no_auth_session`: the enrollment session is gone, so nothing on this screen
 * can work. The message is about the SESSION rather than the input, because
 * retrying cannot mint a cookie — the oracle's "try again" tail loops forever.
 */
const NO_SESSION_MESSAGE = "Your enrollment session has expired. Please sign in again.";

/**
 * `invalid_or_expired_token`: the settings hand-off token lives 60 seconds
 * (`_enrollmentTokenLifetime`), which is easy to miss. Naming the LINK is what
 * separates this from a generic failure — the user's fix is to start setup again
 * from the app that sent them, not to retry here.
 */
const EXPIRED_TOKEN_MESSAGE =
  "This enrollment link has expired. Please start setup again from your account settings.";

/** The API's machine tokens for these endpoints. Matched against, never rendered. */
const NO_AUTH_SESSION = "no_auth_session";
const INVALID_CODE = "invalid_code";
const USER_NOT_FOUND = "user_not_found";
const UPDATE_FAILED = "update_failed";
const INVALID_OR_EXPIRED_TOKEN = "invalid_or_expired_token";

/**
 * Status fallbacks, for when `code` is absent or unrecognised. `enroll/confirm`
 * emits exactly one 401 (`no_auth_session`, from `ResolveEnrollmentUserIdAsync`
 * returning null), and `invalid_code` is by far the dominant 400 — the other two
 * need a race to reach.
 */
const UNAUTHORIZED_STATUS = 401;
const BAD_REQUEST_STATUS = 400;

/** Map an `enroll/totp` rejection onto user-facing copy. */
export function startFailureMessage(cause: unknown): string {
  const code: string | undefined = readErrorCode(cause);

  if (code === NO_AUTH_SESSION || readMember(cause, "status") === UNAUTHORIZED_STATUS) {
    return NO_SESSION_MESSAGE;
  }

  return START_FAILED_MESSAGE;
}

/** Map an `enroll/exchange-token` rejection onto user-facing copy. */
export function exchangeFailureMessage(cause: unknown): string {
  if (readErrorCode(cause) === INVALID_OR_EXPIRED_TOKEN) {
    return EXPIRED_TOKEN_MESSAGE;
  }

  // A 400 is the ONLY failure this endpoint has, so an unrecognised code with one
  // is still the expired-token case; anything else is a genuine unknown.
  if (readMember(cause, "status") === BAD_REQUEST_STATUS) {
    return EXPIRED_TOKEN_MESSAGE;
  }

  return START_FAILED_MESSAGE;
}

/** Map an `enroll/confirm` rejection onto user-facing copy — see the note above. */
export function confirmFailureMessage(cause: unknown): string {
  const code: string | undefined = readErrorCode(cause);

  if (code === INVALID_CODE) {
    return INVALID_CODE_MESSAGE;
  }

  if (code === NO_AUTH_SESSION) {
    return NO_SESSION_MESSAGE;
  }

  // The should-never-happen writes. Both are 400s, so WITHOUT the token they
  // would fall to the status rule below and wrongly blame the user's code.
  if (code === USER_NOT_FOUND || code === UPDATE_FAILED) {
    return CONFIRM_FAILED_MESSAGE;
  }

  const status: unknown = readMember(cause, "status");

  if (status === UNAUTHORIZED_STATUS) {
    return NO_SESSION_MESSAGE;
  }

  if (status === BAD_REQUEST_STATUS) {
    return INVALID_CODE_MESSAGE;
  }

  return CONFIRM_FAILED_MESSAGE;
}
