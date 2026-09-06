/**
 * The Register screen's RESULT LAYER: the five client-side guards and this
 * screen's own words for a rejection, with no React in it.
 *
 * `register` answers problems with catalog codes (`Auth.EmailTaken`,
 * `Auth.ClientIdInvalid`, `Auth.PasswordsDoNotMatch`, `Validation.Failed`
 * for a weak password) and the screen resolves them through `useFailureMessage`.
 * Email-taken and client-id copy are the app registry's; the server-side echo of
 * the local mismatch guard is this screen's, so it says the same thing the guard
 * does. A weak-password rejection reads the policy's own `detail`.
 */

import { ErrorCode, type FailureMessageRegistry } from "@bc-solutions-coder/api-errors";

/** The oracle's client-side guards, in the oracle's own order. */
const BLANK_EMAIL_MESSAGE = "Please enter your email address.";
const BLANK_PASSWORD_MESSAGE = "Please enter a password.";
export const PASSWORD_MISMATCH_MESSAGE = "Passwords do not match.";
const TERMS_REQUIRED_MESSAGE = "You must agree to the Terms of Service.";
const PRIVACY_REQUIRED_MESSAGE = "You must agree to the Privacy Policy.";

/** The oracle's `_ =>` tail, minus its raw-string leak. */
export const REGISTER_FAILED_MESSAGE = "An error occurred. Please try again.";

/** This screen's sentences, ahead of the app registry. */
export const REGISTER_MESSAGES: FailureMessageRegistry = {
  // The server-side echo of the local guard, so it says the same thing.
  [ErrorCode.AUTH_PASSWORDS_DO_NOT_MATCH]: () => PASSWORD_MISMATCH_MESSAGE,
};

/** What the screen's form holds, and what the guards below read. */
export interface RegisterValues {
  readonly email: string;
  readonly password: string;
  readonly confirmPassword: string;
  readonly isPasswordless: boolean;
  readonly termsAccepted: boolean;
  readonly privacyAccepted: boolean;
}

/**
 * The oracle's `HandleRegister` guards, in the oracle's own order, as ONE
 * message or `null`.
 *
 * They are a submit-time sequence rather than the schema's per-field rules
 * because the screen shows a single banner and the ORDER is what it says: a
 * form that is wrong in three ways reports the first fault, not all three. A
 * zod rule could not do that — it would report every field at once, and it
 * would abort the submit before the callback that owns this order ever ran.
 *
 * Both password guards sit inside the oracle's `if (!_isPasswordless)`: a
 * passwordless signup has no password to check, so demanding one would make the
 * toggle unusable.
 */
export function registerGuardMessage(values: RegisterValues): string | null {
  if (values.email.trim() === "") {
    return BLANK_EMAIL_MESSAGE;
  }

  if (!values.isPasswordless) {
    if (values.password.trim() === "") {
      return BLANK_PASSWORD_MESSAGE;
    }

    if (values.password !== values.confirmPassword) {
      return PASSWORD_MISMATCH_MESSAGE;
    }
  }

  if (!values.termsAccepted) {
    return TERMS_REQUIRED_MESSAGE;
  }

  if (!values.privacyAccepted) {
    return PRIVACY_REQUIRED_MESSAGE;
  }

  return null;
}
