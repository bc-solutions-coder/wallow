/**
 * Dependency-free string helpers for display identifiers.
 */

/**
 * Convert text to a lowercase ASCII token with hyphens between alphanumeric runs. Removes leading
 * and trailing hyphens and does not transliterate Unicode letters. Inputs with no ASCII letters or
 * digits return an empty string.
 */
export function toSlug(value: string): string {
  return value
    .toLowerCase()
    .replaceAll(/[^a-z0-9]+/gu, "-")
    .replaceAll(/^-+|-+$/gu, "");
}
