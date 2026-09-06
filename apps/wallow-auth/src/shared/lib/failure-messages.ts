import {
  defineFailureMessages,
  ErrorCode,
  type FailureMessageRegistry,
} from "@bc-solutions-coder/api-errors";

/**
 * The app's one failure-message registry, published from the root through
 * `FailureMessagesProvider`. Every surface below resolves through it, so a
 * code answered by two screens reads the same sentence on both.
 *
 * Only codes whose catalog `detail` is not the right sentence for THIS app are
 * listed; everything else falls through to the model's copy. Per-screen
 * wording (the challenge's "invalid backup code", the OTP tab's "invalid or
 * expired code", each screen's own "this LINK has expired") stays at the call
 * site through `messages`, so a token code has no row here: every screen that
 * can receive one names its own link.
 */
export const failureMessages: FailureMessageRegistry = defineFailureMessages({
  [ErrorCode.AUTH_EMAIL_TAKEN]: () =>
    "An account with this email already exists. Please sign in instead.",
  [ErrorCode.AUTH_CLIENT_ID_INVALID]: () =>
    "The sign-up link you followed is not valid. Please go back to the application you came from and try again.",
  [ErrorCode.MFA_ENROLLMENT_TOKEN_INVALID]: () =>
    "This enrollment link has expired. Please start setup again from your account settings.",
});
