/**
 * The Invitation screen's RESULT LAYER: the expiry predicate and this screen's
 * own words for the two calls' failures, with no React in it.
 *
 * `verify/{token}` answers a 404 `Identity.InvitationNotFound` problem for an
 * unknown or spent token; `{token}/accept` refuses with `InvitationNotFound`,
 * `InvitationExpired` or `InvitationNotPending`. The screen resolves each
 * rejection through `useFailureMessage` with the sentences below as call-site
 * `messages`; anything else — a 5xx, a dead network — reads the catalog's or
 * the call site's fallback, which is deliberately about a TRANSIENT problem
 * rather than a bad invitation.
 */

import { ErrorCode, type FailureMessageRegistry } from "@bc-solutions-coder/api-errors";
import type { InvitationResponse } from "@bc-solutions-coder/sdk";

/** The oracle's `IsNullOrWhiteSpace(Token)` guard message. */
export const NO_TOKEN_MESSAGE = "No invitation token provided.";

/** The oracle's `_invitation is null` branch, reached here via the 404 problem. */
const INVALID_INVITATION_MESSAGE = "This invitation is not valid or has already been used.";

/** The oracle's `catch` around the verify call: any other failure. */
export const VERIFY_FAILED_MESSAGE = "Unable to verify this invitation. Please try again later.";

/**
 * The oracle's `success == false` branch on accept. One sentence for all three
 * refusals: an EXPIRED invitation is precisely the case this copy names, and
 * telling that user "an error occurred, please try again" would send them
 * retrying a request that can never succeed.
 */
const ACCEPT_REJECTED_MESSAGE =
  "Unable to accept this invitation. It may have expired or already been used.";

/** The oracle's `catch` around the accept call: any other failure. */
export const ACCEPT_FAILED_MESSAGE =
  "An error occurred while accepting the invitation. Please try again.";

/** The oracle's expired `BbAlert`. */
export const EXPIRED_MESSAGE =
  "This invitation has expired. Please ask your administrator to send a new one.";

/** The verify call's sentences, ahead of the app registry. */
export const VERIFY_MESSAGES: FailureMessageRegistry = {
  [ErrorCode.IDENTITY_INVITATION_NOT_FOUND]: () => INVALID_INVITATION_MESSAGE,
};

/** The accept call's sentences, ahead of the app registry. */
export const ACCEPT_MESSAGES: FailureMessageRegistry = {
  [ErrorCode.IDENTITY_INVITATION_NOT_FOUND]: () => ACCEPT_REJECTED_MESSAGE,
  [ErrorCode.IDENTITY_INVITATION_EXPIRED]: () => ACCEPT_REJECTED_MESSAGE,
  [ErrorCode.IDENTITY_INVITATION_NOT_PENDING]: () => ACCEPT_REJECTED_MESSAGE,
};

/**
 * The oracle's `Status is "Expired" || ExpiresAt < UtcNow`. The OR is
 * load-bearing: `Status` only flips when the `CleanupExpiredAsync` sweep gets
 * to it, so between the expiry instant and the sweep the date is the ONLY
 * branch that catches it.
 *
 * An unparseable `expiresAt` yields `NaN`, and every `NaN` comparison is false —
 * so a malformed date falls through to "not expired" and lets the SERVER refuse
 * the accept, rather than this screen declaring a live invitation dead over a
 * date it could not read.
 */
export function isExpired(invitation: InvitationResponse): boolean {
  return invitation.status === "Expired" || Date.parse(invitation.expiresAt) < Date.now();
}
