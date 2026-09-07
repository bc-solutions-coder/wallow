/**
 * Normalize submit failures and separate matched field messages from banner context. The form
 * hook resolves banner text through the app message registry.
 */

import {
  type ApiFailure,
  type SplitFieldErrors,
  splitFieldErrors,
  toApiFailure,
} from "@bc-solutions-coder/api-errors";

/** What one failed submit leaves for the form to show. */
export interface SubmitFailure extends SplitFieldErrors {
  /**
   * The failure the banner resolves its sentence from, or `null` when every
   * message landed on a field — a banner there would only repeat the inputs.
   */
  readonly bannerFailure: ApiFailure | null;
}

/**
 * Split a failed submit across the fields and the banner.
 *
 * `knownFields` is the set of camelCase names the form holds. Anything not
 * already an `ApiFailure` is classified first (a thrown `Error` is a transport
 * failure), so the banner never shows transport text. A message keyed by a
 * field the form does not hold cannot be shown next to an input; the banner
 * receives the unmatched messages as resolver context, without changing the
 * original failure or joining messages together.
 */
export function splitSubmitFailure(error: unknown, knownFields: readonly string[]): SubmitFailure {
  const failure: ApiFailure = toApiFailure(error);
  const { fieldErrors, unmatched }: SplitFieldErrors = splitFieldErrors(failure, knownFields);
  // Messages, not keys: a matched field with an empty list shows nothing, so
  // it must not count as "placed" or the submit would fail with no feedback.
  const placedAnyMessage: boolean = Object.values(fieldErrors).some(
    (messages: readonly string[]): boolean => messages.length > 0,
  );
  const everyMessagePlaced: boolean = unmatched.length === 0 && placedAnyMessage;

  return { fieldErrors, unmatched, bannerFailure: everyMessagePlaced ? null : failure };
}
