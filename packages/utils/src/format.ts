/**
 * Date display formatting with the en-US locale and the host time zone.
 */

/** The locale every formatter in this module pins. */
const LOCALE: string = "en-US";

/**
 * Format a date as en-US month, day, and year, such as January 5, 2026.
 *
 * @param value Date, parseable date string, or epoch milliseconds. Formatting uses the host time
 * zone.
 *
 * @returns The formatted date, or Invalid Date when the input cannot be parsed.
 */
export function formatLongDate(value: string | number | Date): string {
  return new Date(value).toLocaleDateString(LOCALE, {
    month: "long",
    day: "numeric",
    year: "numeric",
  });
}
