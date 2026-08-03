/**
 * The Register screen's RESULT LAYER: the five client-side guards and the
 * rejection→copy mapping, with no React in it.
 *
 * ── THE ERROR BRANCHES (REVISED — see Wallow-vec7.7) ─────────────────────────
 *
 * `AccountController.Register` (api/.../Controllers/AccountController.cs:639-724)
 * fails in four ways, each a 400 with a bare `{ succeeded, error }` body (NOT
 * problem details), so `unwrap()` throws on all four:
 *
 *     400 error "passwords_do_not_match"        line 648
 *     400 error "invalid_client_id"             line 658
 *     400 error "email_taken"                   ~686, from DuplicateEmail/UserName
 *     400 error <raw IdentityResult sentence>   the `_ =>` fallback
 *
 * All four share a 400, so — unlike the sibling ResetPassword port, where one
 * failure reason made the status itself meaningful — status cannot narrow here.
 * What does is `toWallowError()`'s `readCode`, which probes `extensions.code >
 * code > error` and so carries the API's token through intact. Three of the four
 * branches are recoverable that way; the fourth stays generic on purpose,
 * because its "code" is a raw English sentence from Identity rather than a
 * stable token.
 *
 * The oracle's own switch is only partly worth porting:
 *
 *   - Its `"password_too_weak"` branch is DEAD CODE — the controller never emits
 *     that string. That case arrives as the raw sentence and lands on the
 *     generic tail here.
 *   - The API's error tail renders `result.Error` RAW, so a user really can be
 *     shown Identity's own prose ("Passwords must have at least one digit
 *     ('0'-'9')."). `code` is a machine member here: matched against KNOWN
 *     tokens and NEVER rendered, so anything unrecognised — including a token
 *     added tomorrow — falls to the generic message rather than guessing.
 *
 * Narrowing is STRUCTURAL rather than `instanceof WallowError`, because that
 * class is exported from the SDK's `./server` entry and screens may not import
 * the SDK at all. A network-level rejection carries no `code` and must fall
 * through to the generic message rather than throw.
 */

import { readErrorCode } from "@shared/lib/error-code";

/** The oracle's client-side guards, in the oracle's own order. */
const BLANK_EMAIL_MESSAGE = "Please enter your email address.";
const BLANK_PASSWORD_MESSAGE = "Please enter a password.";
export const PASSWORD_MISMATCH_MESSAGE = "Passwords do not match.";
const TERMS_REQUIRED_MESSAGE = "You must agree to the Terms of Service.";
const PRIVACY_REQUIRED_MESSAGE = "You must agree to the Privacy Policy.";

/** The oracle's `"email_taken" =>` branch, reachable again as of Wallow-vec7.7. */
const EMAIL_TAKEN_MESSAGE = "An account with this email already exists. Please sign in instead.";

/**
 * `invalid_client_id`: the `client_id` came off the QUERY STRING, not the form.
 * Nothing the user typed is wrong and retyping it cannot help, so the copy points
 * at the link rather than blaming their input.
 */
const INVALID_CLIENT_MESSAGE =
  "The sign-up link you followed is not valid. Please go back to the application you came from and try again.";

/**
 * The oracle's `_ =>` tail, minus its raw-string leak. Also the honest home of
 * the weak-password rejection, whose reason arrives as an English sentence
 * rather than a token — see the error-branch note above.
 */
const GENERIC_FAILURE_MESSAGE = "An error occurred. Please try again.";

/** The API's machine tokens for this endpoint. Matched against, never rendered. */
const EMAIL_TAKEN = "email_taken";
const PASSWORDS_DO_NOT_MATCH = "passwords_do_not_match";
const INVALID_CLIENT_ID = "invalid_client_id";

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

/** Map a `register` rejection onto user-facing copy — see the note above. */
export function registerFailureMessage(cause: unknown): string {
  const code: string | undefined = readErrorCode(cause);

  if (code === EMAIL_TAKEN) {
    return EMAIL_TAKEN_MESSAGE;
  }

  if (code === PASSWORDS_DO_NOT_MATCH) {
    // The server-side echo of the local guard, so it says the same thing.
    return PASSWORD_MISMATCH_MESSAGE;
  }

  if (code === INVALID_CLIENT_ID) {
    return INVALID_CLIENT_MESSAGE;
  }

  return GENERIC_FAILURE_MESSAGE;
}
