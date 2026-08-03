/**
 * The MfaChallenge screen's RESULT LAYER: the blank-input guard and the
 * rejection→copy mapping, with no React in it.
 *
 * ── THE ERROR BRANCHES ────────────────────────────────────────────────────────
 *
 * `AccountController.VerifyMfaChallenge` (api/.../Controllers/AccountController.cs:167-236)
 * fails in exactly three ways, each a non-2xx with a bare `{ succeeded, error }`
 * body (NOT problem details):
 *
 *     401 error "no_mfa_session"   partial-auth cookie missing or expired
 *     401 error "invalid_code"     no user / no TOTP secret / code rejected
 *     423 error "mfa_locked_out"   already locked, or locked by this attempt
 *
 * `unwrap()` throws on all three, and `toWallowError()` recovers the token: as of
 * Wallow-vec7.7 `readCode` probes `extensions.code > code > error`, so the `error`
 * member of that anon body reaches the screen as `WallowError.code`. (Before that
 * it did not, and this screen narrowed on HTTP status alone — which could not tell
 * `no_mfa_session` from `invalid_code`, since they share a 401.)
 *
 * The oracle's own switch is only partly worth porting:
 *
 *   - Its `"expired_challenge"` branch is DEAD CODE — this endpoint never emits
 *     that string. The expired-cookie case is `no_mfa_session`, which the oracle
 *     drops into its `_` tail. `SESSION_EXPIRED_MESSAGE` says what that dead
 *     branch was reaching for, keyed on the token the API actually sends.
 *   - The API's error tail can render `result.Error` RAW, exposing the literal
 *     "no_mfa_session". `code` is a machine token and is never rendered here: it
 *     is matched against KNOWN values, and anything else — including a 401
 *     carrying an unrecognised code — falls to the generic message rather than
 *     guessing.
 *
 * Narrowing is STRUCTURAL rather than `instanceof WallowError`, because that
 * class is exported from the SDK's `./server` entry and screens may not import
 * the SDK at all. A network-level rejection carries neither `code` nor `status`
 * and must fall through to the generic message rather than throw.
 */

import { readErrorCode, readMember } from "@shared/lib/error-code";

/** The oracle's blank-input guards, mode-sensitive as the oracle's are. */
const BLANK_CODE_MESSAGE = "Please enter the verification code.";
const BLANK_BACKUP_CODE_MESSAGE = "Please enter a backup code.";

/** The oracle's `"invalid_code" =>` branch, both halves of it. */
const INVALID_CODE_MESSAGE = "Invalid verification code. Please try again.";
const INVALID_BACKUP_CODE_MESSAGE = "Invalid backup code. Please try again.";

/**
 * `no_mfa_session`: the challenge session is gone, so nothing the user types
 * here can work. The message is about the SESSION, not the input — telling a
 * user their valid code was rejected would send them round a loop that burns
 * their five attempts against a cookie that no longer exists.
 */
const SESSION_EXPIRED_MESSAGE = "Your verification session has expired. Please sign in again.";

/** `mfa_locked_out`: the oracle printed the raw token here. */
const LOCKED_OUT_MESSAGE =
  "Too many failed attempts. Your account is temporarily locked. Please try again later.";

/** The oracle's `_ =>` tail, minus its raw-string leak. */
const GENERIC_FAILURE_MESSAGE = "Verification failed. Please try again.";

/** The API's machine tokens for this endpoint. Matched against, never rendered. */
const INVALID_CODE = "invalid_code";
const NO_MFA_SESSION = "no_mfa_session";
const MFA_LOCKED_OUT = "mfa_locked_out";

/**
 * Retained as a status-level fallback alongside the `mfa_locked_out` token: 423
 * identifies this failure on its own, and the cost of missing it — a locked user
 * retyping codes that cannot work, re-locking themselves — is worth the extra rule.
 */
const LOCKED_OUT_STATUS = 423;

/** What the screen's form holds. The MODE is not one of them — see the guard below. */
export interface ChallengeValues {
  readonly code: string;
}

/**
 * The oracle's `if (string.IsNullOrWhiteSpace(_code))`, as one message or `null`.
 *
 * A submit-time check rather than a zod rule, because it shares one banner with
 * the rejection copy and a zod failure would abort `handleSubmit` before the
 * callback that owns that banner ran. A blank submit cannot succeed and costs a
 * lockout attempt, so it never reaches `mfa/verify`.
 *
 * `useBackupCode` is a parameter rather than a form value: the card heading
 * outside the form branches on it too, so the mode is the screen's state and the
 * form holds only what the user typed.
 */
export function challengeGuardMessage(
  values: ChallengeValues,
  useBackupCode: boolean,
): string | null {
  if (values.code.trim() === "") {
    return useBackupCode ? BLANK_BACKUP_CODE_MESSAGE : BLANK_CODE_MESSAGE;
  }

  return null;
}

/** Map a rejection onto user-facing copy — see the error-branch note above. */
export function verifyFailureMessage(cause: unknown, useBackupCode: boolean): string {
  const code: string | undefined = readErrorCode(cause);

  if (code === INVALID_CODE) {
    return useBackupCode ? INVALID_BACKUP_CODE_MESSAGE : INVALID_CODE_MESSAGE;
  }

  if (code === NO_MFA_SESSION) {
    return SESSION_EXPIRED_MESSAGE;
  }

  if (code === MFA_LOCKED_OUT || readMember(cause, "status") === LOCKED_OUT_STATUS) {
    return LOCKED_OUT_MESSAGE;
  }

  return GENERIC_FAILURE_MESSAGE;
}
