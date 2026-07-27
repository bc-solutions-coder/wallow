import { twMerge } from "tailwind-merge";

/**
 * Merge class values into a single Tailwind-conflict-resolved class string.
 *
 * Every component part runs its recipe output plus the caller's `className`
 * through this helper, so a consumer can always override a recipe utility: the
 * value passed last wins, and utilities the caller never mentions survive.
 *
 * Layer 0 of the package layering — `src/core/` imports nothing from
 * `src/components/`.
 */
export function cn(...values: ReadonlyArray<string | false | null | undefined>): string {
  return twMerge(values.filter(Boolean).join(" "));
}
