/**
 * wallow-web's failure-message registry: the one table of app-specific
 * sentences `resolveFailureMessage` consults after a call site's own
 * `messages` and before the package's shipped copy. It is mounted once through
 * `FailureMessagesProvider` in the root route and handed to the query client's
 * unhandled-failure callback, so a banner, a form, and a toast all say the
 * same thing for the same code.
 *
 * The entries are the MFA endpoints' codes. The catalog's own `detail` would do
 * for each of them; these sentences exist because the settings UI calls the
 * feature "two-factor" and speaks to a signed-in user, where the catalog speaks
 * to anyone. Keep the keys on `ErrorCode` so a renamed code fails typecheck
 * here rather than silently falling back to the catalog's wording.
 */
import {
  defineFailureMessages,
  ErrorCode,
  type FailureMessageRegistry,
} from "@bc-solutions-coder/api-errors";

export const failureMessages: FailureMessageRegistry = defineFailureMessages({
  [ErrorCode.MFA_SESSION_MISSING]: () => "Your session has expired. Please sign in again.",
  [ErrorCode.MFA_PASSWORD_INVALID]: () => "That password is incorrect.",
  [ErrorCode.MFA_CODE_INVALID]: () => "That verification code is not valid.",
  [ErrorCode.IDENTITY_USER_NOT_FOUND]: () =>
    "Your account could not be found. Please sign in again.",
  [ErrorCode.MFA_UPDATE_FAILED]: () =>
    "Your two-factor settings could not be saved. Please try again.",
  [ErrorCode.MFA_NOT_ENABLED]: () => "Two-factor authentication is not enabled on your account.",
});
