/**
 * Locale-pinned display formatting.
 *
 * The locale is a literal here, never the host's. An ambient locale renders a
 * different string to a user than the one the surrounding copy was written and
 * reviewed against, and on a server it is whatever the container's environment
 * happens to say — which is not a property of the user at all.
 */

/** The locale every formatter in this module pins. */
const LOCALE: string = "en-US";

/**
 * A date as long-form prose: `January 5, 2026`.
 *
 * Takes what a JSON payload actually carries — an ISO string — as well as the
 * `Date` and epoch-millis forms `new Date()` accepts. An unparseable input yields
 * `"Invalid Date"`, which is `Date`'s own answer and is left alone: a caller that
 * needs a fallback has a value to test, and swallowing it here would hide a bad
 * payload behind an empty span.
 */
export function formatLongDate(value: string | number | Date): string {
  return new Date(value).toLocaleDateString(LOCALE, {
    month: "long",
    day: "numeric",
    year: "numeric",
  });
}
