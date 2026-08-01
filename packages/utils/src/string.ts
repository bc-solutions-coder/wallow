/**
 * String shaping that more than one surface needs, kept dependency-free.
 */

/**
 * `value` reduced to a lowercase hyphen-joined token: `Microsoft Entra ID` becomes
 * `microsoft-entra-id`.
 *
 * Every run of non-alphanumerics collapses to a single hyphen and both ends are
 * trimmed, so the result never begins, ends or doubles a separator. This derives a
 * stable identifier — a testid, a fragment — from prose. It is not a URL-safety
 * guarantee and it transliterates nothing, so a string of entirely non-ASCII
 * letters reduces to the empty string rather than to their romanisation.
 */
export function toSlug(value: string): string {
  return value
    .toLowerCase()
    .replaceAll(/[^a-z0-9]+/gu, "-")
    .replaceAll(/^-+|-+$/gu, "");
}
