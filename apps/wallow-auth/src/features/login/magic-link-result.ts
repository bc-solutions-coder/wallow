/**
 * The magic-link tab's RESULT LAYER: what turns an untyped `sendMagicLink`
 * response into a decision, plus this tab's own words for a failure, with no
 * React and no SDK in it.
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

/** The oracle's blank-input guard — note WHITEspace. */
export const BLANK_EMAIL_MESSAGE = "Please enter your email.";

/** The oracle's `_magicLinkSent` alert. */
export const MAGIC_LINK_SENT_MESSAGE = "Check your email for a magic link.";

/**
 * The verify failure copy, naming the LINK: a tampered, spent or expired token
 * all mean the same thing to the person holding it, so both catalog codes read
 * the same sentence.
 */
const MAGIC_LINK_EXPIRED_MESSAGE =
  "This magic link has expired or has already been used. Please request a new one.";

/** This tab's sentences, ahead of the app registry when the shell renders them. */
export const MAGIC_LINK_FAILURE_MESSAGES: FailureMessageRegistry = {
  [ErrorCode.AUTH_TOKEN_INVALID]: () => MAGIC_LINK_EXPIRED_MESSAGE,
  [ErrorCode.AUTH_TOKEN_EXPIRED]: () => MAGIC_LINK_EXPIRED_MESSAGE,
};

/**
 * Did the API actually accept the send? The endpoint returns an anonymous
 * `Ok(new { … })` with no OpenAPI schema, so the narrowing belongs here, at the
 * boundary.
 *
 * STRICT `=== true`, reproducing C#'s `if (result.Succeeded)`: JS truthiness would
 * accept the string `"false"`. A body this screen cannot read is NOT a sent link —
 * telling a user to go check an inbox that will stay empty is worse than an error.
 */
export function magicLinkWasSent(body: unknown): boolean {
  return readMember(body, "succeeded") === true;
}
