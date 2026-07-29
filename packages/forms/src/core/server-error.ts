/**
 * Splitting a failed submit into the two surfaces a form has for it: the
 * per-field messages that belong next to an input, and the one banner sentence
 * that belongs above the form.
 *
 * The API answers a validation failure with RFC 7807 problem details whose
 * `errors` member keys messages by property name — `@bc-solutions-coder/sdk`
 * carries that through as `WallowError.fieldErrors`. Those keys are the API's
 * property names (PascalCase, as FluentValidation and ASP.NET Core emit them),
 * while a form's values are camelCase, so the two have to be reconciled before
 * anything can be shown.
 *
 * Layer 0 of the package: `src/core/` imports nothing from `src/fields/` or
 * `src/form/`.
 */

import { isWallowError } from "@bc-solutions-coder/sdk";

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
 * The API's property name folded onto the form's. Only the first character
 * differs between `ValidationProblemDetails`' `"Name"` and a form's `"name"`;
 * lowercasing the whole key would break `"emailAddress"`.
 */
function toFieldName(propertyName: string): string {
  return propertyName.charAt(0).toLowerCase() + propertyName.slice(1);
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
  if (!isWallowError(error)) {
    // A thrown `Error` that carries its own sentence (a network fault, say) says
    // more than the caller's generic fallback; an empty one says nothing.
    const message: string =
      error instanceof Error && error.message !== "" ? error.message : fallback;

    return { fieldErrors: {}, formError: message };
  }

  const matched: Record<string, readonly string[]> = {};
  const unmatched: string[] = [];

  for (const [propertyName, messages] of Object.entries(error.fieldErrors ?? {})) {
    const field: string = toFieldName(propertyName);

    if (knownFields.includes(field)) {
      matched[field] = messages;
    } else {
      unmatched.push(...messages);
    }
  }

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
