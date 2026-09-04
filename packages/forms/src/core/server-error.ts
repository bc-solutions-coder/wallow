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
  isApiFailure,
  resolveFailureMessage,
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

/** The two surfaces a failed submit is split across. */
export interface SplitServerError {
  /**
   * camelCase field name -> messages, for the names the form actually has. Only
   * matched entries appear; an unmatched one joins {@link formError} instead.
   */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  /** The banner sentence, or `null` when every message landed on a field. */
  readonly formError: string | null;
}

/**
 * The human-readable sentence for `error`: the API's ProblemDetails `detail`
 * when it sent one, else the sentence `@bc-solutions-coder/api-errors` resolves
 * for the code or status, else `fallback`.
 *
 * @deprecated Resolve a failure message through the registry instead:
 * `useFailureMessage(error)` from `@bc-solutions-coder/ui/failure-messages`
 * hoisted to the top of the component (it accepts a nullish error and returns
 * `null`, so it replaces an `isError ? errorText(...) : null` expression
 * without a conditional hook), or `resolveFailureMessage(error, options)` from
 * `@bc-solutions-coder/api-errors` outside React. This helper prefers the raw
 * `detail` over the registry and echoes a thrown `Error`'s message, both of
 * which the failure model forbids. It stays only until its remaining call
 * sites move; do not add one.
 */
export function errorText(error: unknown, fallback: string): string {
  if (isApiFailure(error)) {
    return error.detail ?? resolveFailureMessage(error, { fallback });
  }
  return error instanceof Error && error.message !== "" ? error.message : fallback;
}

/**
 * Split a failed submit into field-level and form-level messages.
 *
 * `knownFields` is the set of camelCase names the form holds; a message keyed by
 * anything else joins the banner rather than vanishing.
 *
 * @deprecated `useAppForm` does not use this: it splits with api-errors and
 * resolves the banner through the registry. A bespoke form should do the
 * same — `splitFieldErrors` from
 * `@bc-solutions-coder/api-errors` for the fields, `useFailureMessage` from
 * `@bc-solutions-coder/ui/failure-messages` for the banner. This helper joins
 * unmatched messages into one string and echoes a thrown `Error`'s message,
 * both of which the failure model forbids. It stays only until its remaining
 * call sites move; do not add one.
 */
export function splitServerError(
  error: unknown,
  knownFields: readonly string[],
  fallback: string,
): SplitServerError {
  if (!isApiFailure(error)) {
    // A thrown `Error` that carries its own sentence (a network fault, say) says
    // more than the caller's generic fallback; an empty one says nothing.
    const message: string =
      error instanceof Error && error.message !== "" ? error.message : fallback;

    return { fieldErrors: {}, formError: message };
  }

  const { fieldErrors: matched, unmatched }: SplitFieldErrors = splitFieldErrors(
    error,
    knownFields,
  );

  if (unmatched.length > 0) {
    // A message the form cannot show next to an input still has to be shown.
    return { fieldErrors: matched, formError: unmatched.join(" ") };
  }

  if (Object.keys(matched).length > 0) {
    // Everything landed on a field, so a banner would only repeat it.
    return { fieldErrors: matched, formError: null };
  }

  return { fieldErrors: matched, formError: error.detail ?? fallback };
}
