/**
 * The MfaChallenge screen's RESULT LAYER: the blank-input guard and this
 * screen's own words for a rejected code, with no React in it.
 *
 * `mfa/verify` answers problems with catalog codes (`Mfa.CodeInvalid`,
 * `Mfa.SessionMissing`, `Mfa.LockedOut`), and the screen resolves them through
 * `useFailureMessage`. Session-missing and lockout read the catalog's own
 * `detail`; only the invalid-code sentence is this screen's, because it depends
 * on which MODE the user is in — a wrong backup code is not a wrong TOTP code.
 */

import { ErrorCode, type FailureMessageRegistry } from "@bc-solutions-coder/api-errors";

/** The oracle's blank-input guards, mode-sensitive as the oracle's are. */
const BLANK_CODE_MESSAGE = "Please enter the verification code.";
const BLANK_BACKUP_CODE_MESSAGE = "Please enter a backup code.";

/** The oracle's `"invalid_code" =>` branch, both halves of it. */
const INVALID_CODE_MESSAGE = "Invalid verification code. Please try again.";
const INVALID_BACKUP_CODE_MESSAGE = "Invalid backup code. Please try again.";

/** The oracle's `_ =>` tail, minus its raw-string leak. */
export const VERIFY_FAILED_MESSAGE = "Verification failed. Please try again.";

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

/** This screen's sentence for a rejected code, in the mode the user typed it. */
export function challengeMessages(useBackupCode: boolean): FailureMessageRegistry {
  return {
    [ErrorCode.MFA_CODE_INVALID]: () =>
      useBackupCode ? INVALID_BACKUP_CODE_MESSAGE : INVALID_CODE_MESSAGE,
  };
}
