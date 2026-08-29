/** The oracle's `UpdatePasswordStrength`, ported predicate-for-predicate. */
export interface PasswordStrength {
  readonly label: string;
  readonly percent: number;
  readonly indicatorClass: string;
}

/** The oracle's two `UpdatePasswordStrength` length thresholds. */
const STRONG_MIN_LENGTH = 12;
const FAIR_MIN_LENGTH = 8;

/**
 * `null` for an empty password — the oracle's
 * `@if (!string.IsNullOrEmpty(_password))` gate on the whole meter.
 *
 * The character classes mirror `char.IsUpper` / `IsLower` / `IsDigit` /
 * `!IsLetterOrDigit`, which are Unicode-aware in .NET, so the Unicode property
 * escapes are used rather than `[A-Z]` — a port that narrowed to ASCII would
 * rate a perfectly strong non-Latin password Weak.
 */
export function passwordStrength(password: string): PasswordStrength | null {
  if (password === "") {
    return null;
  }

  const hasUpper: boolean = /\p{Lu}/u.test(password);
  const hasLower: boolean = /\p{Ll}/u.test(password);
  const hasDigit: boolean = /\p{Nd}/u.test(password);
  const hasSpecial: boolean = /[^\p{L}\p{N}]/u.test(password);
  const hasMix: boolean = hasUpper && hasLower && (hasDigit || hasSpecial);

  // Length ALONE is not enough: the oracle's `Length >= 12 && hasMix`. A 12-char
  // all-lowercase password falls through to Fair.
  if (password.length >= STRONG_MIN_LENGTH && hasMix) {
    return { label: "Strong", percent: 100, indicatorClass: "bg-green-500" };
  }

  if (password.length >= FAIR_MIN_LENGTH) {
    return { label: "Fair", percent: 50, indicatorClass: "bg-yellow-500" };
  }

  return { label: "Weak", percent: 25, indicatorClass: "bg-red-500" };
}
