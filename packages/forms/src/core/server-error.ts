/**
 * Splitting a failed submit into the two surfaces a form has for it: the
 * per-field messages that belong next to an input, and the one banner sentence
 * that belongs above the form.
 *
 * The API answers a validation failure with RFC 7807 problem details whose
 * `errors` member keys messages by property name — the SDK's interceptor
 * carries that through as `ApiFailure.fieldErrors`. Those keys are the API's
 * property names (PascalCase, as FluentValidation and ASP.NET Core emit them),
 * while a form's values are camelCase; `@bc-solutions-coder/api-errors`'
 * `splitFieldErrors` reconciles the two.
 *
 * Layer 0 of the package: `src/core/` imports nothing from `src/fields/` or
 * `src/form/`.
 */

import {
  isApiFailure,
  resolveFailureMessage,
  type SplitFieldErrors,
  splitFieldErrors,
} from "@bc-solutions-coder/api-errors";

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
 * The split's counterpart for everywhere a failed call has no fields to
 * distribute messages across — a failed read, or a write whose failure is shown
 * outside a form. The `isApiFailure` brand check is the gate: anything that did
 * not come through the SDK's error interceptor contributes no copy of its own,
 * so an arbitrary object cannot dictate user-facing text by merely carrying a
 * `detail` member. Never `title` or `message`: a title can be a raw machine
 * token or the parser's own placeholder, and the message is the log line
 * `[<status> <code>] <title>`, not a sentence for a banner.
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
