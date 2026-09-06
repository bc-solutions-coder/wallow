/**
 * The OTP tab's RESULT LAYER: what turns an untyped `sendOtp` response into a
 * decision, plus this tab's own words for a failure, with no React and no SDK in
 * it.
 *
 * The shared branch table (`authDispositionOf`) lives once in `./auth-result`;
 * each tab keeps only what the oracle kept per tab. Failures are not mapped
 * here: a rejection is an `ApiFailure` the fetch layer built from the problem
 * body, and the shell resolves its sentence through `useFailureMessage`. This
 * module contributes only the sentences that are THIS tab's, keyed by catalog
 * code, for the shell to pass as call-site `messages`.
 */

import { ErrorCode, type FailureMessageRegistry } from "@bc-solutions-coder/api-errors";

import { readMember } from "./auth-result";

/**
 * The oracle's blank-input guard — note WHITEspace.
 *
 * Byte-identical to `./magic-link-result`'s `BLANK_EMAIL_MESSAGE`, and deliberately
 * NOT shared with it: they are two independent literals in the oracle, and
 * hoisting them into one constant would mean re-wording one tab's guard silently
 * re-worded the other's. Identical copy is not the same fact as shared copy.
 */
export const OTP_BLANK_EMAIL_MESSAGE = "Please enter your email.";

/** The oracle's second blank-input guard, on the code form. */
export const OTP_BLANK_CODE_MESSAGE = "Please enter the verification code.";

/**
 * The oracle's verify failure copy. "Invalid OR EXPIRED" covers both of the
 * service's reasons, which the catalog folds into one `Auth.OtpInvalid`.
 */
const OTP_INVALID_CODE_MESSAGE = "Invalid or expired code. Please try again.";

/** This tab's sentences, ahead of the app registry when the shell renders them. */
export const OTP_FAILURE_MESSAGES: FailureMessageRegistry = {
  [ErrorCode.AUTH_OTP_INVALID]: () => OTP_INVALID_CODE_MESSAGE,
};

/**
 * Did the API actually accept the send? The endpoint returns an anonymous
 * `Ok(new { … })` with no OpenAPI schema, so the narrowing belongs here, at the
 * boundary.
 *
 * STRICT `=== true`, reproducing C#'s `if (result.Succeeded)`: JS truthiness would
 * accept the string `"false"` and march the user to a code form for a code that was
 * never sent.
 */
export function otpWasSent(body: unknown): boolean {
  return readMember(body, "succeeded") === true;
}
