/**
 * Narrowing helpers for values that arrive as `unknown`: a JSON-parsed URL search
 * param, a member read off an error cause, a message plucked out of a form
 * library's untyped error array.
 *
 * Every helper answers `undefined` rather than throwing. Each call site these were
 * extracted from is reachable by a junk link or a malformed response, and the
 * required behaviour there is a usable screen — not a validation error and not a
 * blank page.
 */

/** `value` when it is a string, `undefined` for anything else. */
export function asString(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

/**
 * `value` as the text a URL actually carried, when it is a JSON scalar.
 *
 * TanStack Router JSON-parses search values before `validateSearch` sees them, so
 * `?error=true` arrives as the BOOLEAN `true` and `?error=1` as a NUMBER. A param
 * that is compared against literal tokens has to be re-stringified rather than
 * dropped: plain {@link asString} would silently swallow the hand-back and show a
 * user a clean form after a failure.
 *
 * Non-scalars read as absent instead of erroring. `?error=[1,2]` parses to an
 * array, which matches no literal anyway — reaching that answer without throwing
 * is what keeps a junk link rendering a page.
 */
export function scalarToString(value: unknown): string | undefined {
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  return asString(value);
}
