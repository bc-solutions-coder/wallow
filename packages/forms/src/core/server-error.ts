/**
 * Splitting a failed submit into the two surfaces a form has for it: the
 * per-field messages that belong next to an input, and the one banner sentence
 * that belongs above the form.
 *
 * The API answers a validation failure with RFC 7807 problem details whose
 * `errors` member keys messages by property name — the SDK's interceptor
 * carries that through as `ApiFailure.fieldErrors`, and
 * `@bc-solutions-coder/api-errors`' `splitFieldErrors` reconciles the API's
 * property names with the form's camelCase field names. The banner is never
 * assembled here: it is a failure message, resolved through the registry by
 * `useFailureMessage` in the hook, so an app's own wording and the shipped
 * defaults both apply to a form exactly as they apply to every other surface.
 *
 * Layer 0 of the package: `src/core/` imports nothing from `src/fields/` or
 * `src/form/`.
 */

import {
  type ApiFailure,
  type SplitFieldErrors,
  splitFieldErrors,
  toApiFailure,
} from "@bc-solutions-coder/api-errors";

/** What one failed submit leaves for the form to show. */
export interface SubmitFailure extends Pick<SplitFieldErrors, "fieldErrors"> {
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
 * then carries the failure's own resolved sentence rather than a joined list of
 * the API's wording.
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

  return { fieldErrors, bannerFailure: everyMessagePlaced ? null : failure };
}
