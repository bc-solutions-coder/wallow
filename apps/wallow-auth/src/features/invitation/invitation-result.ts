/**
 * The Invitation screen's RESULT LAYER: the rejection→copy mapping and the
 * expiry predicate, with no React in it.
 *
 * ── FOUR ORACLE BRANCHES COLLAPSE INTO TWO REJECTIONS, KEYED ON STATUS ───────
 *
 * The oracle's `AuthApiClient` SWALLOWS non-2xx into sentinels —
 * `VerifyInvitationAsync` returns null on any failure (AuthApiClient.cs:297-312),
 * `AcceptInvitationAsync` returns `IsSuccessStatusCode` (:314-322) — so it forks
 * sentinel-vs-`catch` and gives each of the two calls two messages. The facade's
 * `unwrap()` THROWS on every non-2xx, so each pair arrives as ONE rejection and
 * that fork is gone. What survives is `.status`, and it is enough to keep all
 * four messages:
 *
 *   - `verify/{token}` has exactly ONE failure return, a bare `NotFound()`
 *     (InvitationsController.cs:71-80) — so its 404 IS the oracle's null case,
 *     and anything else is the oracle's `catch`.
 *   - `{token}/accept` rejects an unknown/spent/expired token from the service
 *     and its aggregate (:82-91) — every one a 4xx, i.e. the oracle's
 *     "expired or already been used". A 5xx is the `catch`.
 *
 * Keyed on STATUS and deliberately NOT on `code`: unlike `/v1/identity/auth/*`,
 * these two endpoints send no machine-readable code at all — `NotFound()` is a
 * bare status with no body — so every rejection here parses as
 * `Client.UnrecognizedResponse`. A code-keyed mapping would collapse all four
 * messages into the generic one.
 */

import type { InvitationResponse } from "@bc-solutions-coder/sdk";

import { readMember } from "@shared/lib/error-code";

/** The oracle's `IsNullOrWhiteSpace(Token)` guard message. */
export const NO_TOKEN_MESSAGE = "No invitation token provided.";

/** The oracle's `_invitation is null` branch, reached here via HTTP 404. */
const INVALID_INVITATION_MESSAGE = "This invitation is not valid or has already been used.";

/** The oracle's `catch` around the verify call: any other failure. */
const VERIFY_FAILURE_MESSAGE = "Unable to verify this invitation. Please try again later.";

/** The oracle's `success == false` branch on accept, reached here via any 4xx. */
const ACCEPT_REJECTED_MESSAGE =
  "Unable to accept this invitation. It may have expired or already been used.";

/** The oracle's `catch` around the accept call: any other failure. */
const ACCEPT_FAILURE_MESSAGE =
  "An error occurred while accepting the invitation. Please try again.";

/** The oracle's expired `BbAlert`. */
export const EXPIRED_MESSAGE =
  "This invitation has expired. Please ask your administrator to send a new one.";

/** The only failure status `verify/{token}` has — see the seam note above. */
const NOT_FOUND_STATUS = 404;

/** The 4xx band: every way `{token}/accept` says "no" to a well-formed request. */
const CLIENT_ERROR_MIN = 400;
const CLIENT_ERROR_MAX = 500;

/** The HTTP status of a rejection, if it carries one. */
function statusOf(cause: unknown): number | undefined {
  const status: unknown = readMember(cause, "status");

  return typeof status === "number" ? status : undefined;
}

/** The oracle's two verify messages, chosen by status — see the seam note. */
export function verifyFailureMessage(cause: unknown): string {
  return statusOf(cause) === NOT_FOUND_STATUS ? INVALID_INVITATION_MESSAGE : VERIFY_FAILURE_MESSAGE;
}

/**
 * The oracle's two accept messages, chosen by status. Keyed on the whole 4xx band
 * rather than 404 alone: the service throws `EntityNotFoundException` for an
 * unknown or spent token, but an EXPIRED one is refused by the aggregate, and
 * "the invitation is expired" is precisely the case this copy names. Telling that
 * user "an error occurred, please try again" would send them retrying a request
 * that can never succeed.
 */
export function acceptFailureMessage(cause: unknown): string {
  const status: number | undefined = statusOf(cause);

  if (status !== undefined && status >= CLIENT_ERROR_MIN && status < CLIENT_ERROR_MAX) {
    return ACCEPT_REJECTED_MESSAGE;
  }

  return ACCEPT_FAILURE_MESSAGE;
}

/**
 * The oracle's `Status is "Expired" || ExpiresAt < UtcNow` (InvitationLanding.
 * razor:147). The OR is load-bearing: `Status` only flips when the
 * `CleanupExpiredAsync` sweep gets to it (InvitationService.cs:71-89), so between
 * the expiry instant and the sweep the date is the ONLY branch that catches it.
 *
 * An unparseable `expiresAt` yields `NaN`, and every `NaN` comparison is false —
 * so a malformed date falls through to "not expired" and lets the SERVER refuse
 * the accept, rather than this screen declaring a live invitation dead over a
 * date it could not read.
 */
export function isExpired(invitation: InvitationResponse): boolean {
  return invitation.status === "Expired" || Date.parse(invitation.expiresAt) < Date.now();
}
