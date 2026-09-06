/**
 * The MfaEnroll screen's RESULT LAYER: the blank-input guard and this screen's
 * last-resort sentences, with no React in it.
 *
 * The three enrollment endpoints answer problems with catalog codes
 * (`Mfa.SessionMissing`, `Mfa.CodeInvalid`, `Mfa.EnrollmentTokenInvalid`, …) and
 * the screen resolves them through `useFailureMessage`: session and code copy
 * come from the catalog's `detail`, the expired hand-off link from the app
 * registry. What is left here is the two fallbacks for a failure nobody wrote a
 * sentence for.
 */

/** The oracle's `HandleStartEnroll` failure copy. */
export const START_FAILED_MESSAGE = "Failed to start MFA enrollment. Please try again.";

/** The oracle's `IsNullOrWhiteSpace(_code)` guard, ahead of the confirm call. */
const BLANK_CODE_MESSAGE = "Please enter the verification code.";

/** The oracle's `_ =>` tail on confirm, minus its raw-string leak. */
export const CONFIRM_FAILED_MESSAGE = "Failed to confirm MFA enrollment. Please try again.";

/** What the confirm step's form holds, and what the guard below reads. */
export interface ConfirmValues {
  readonly code: string;
}

/**
 * The oracle's pre-call guard, as one message or `null`.
 *
 * It is a submit-time check rather than a zod rule because the screen shows a
 * single banner shared with the confirm rejection: a zod failure would abort
 * `handleSubmit` before the callback that owns that banner ever ran.
 */
export function confirmGuardMessage(values: ConfirmValues): string | null {
  if (values.code.trim() === "") {
    return BLANK_CODE_MESSAGE;
  }

  return null;
}
