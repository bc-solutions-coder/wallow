import { cva, type VariantProps } from "class-variance-authority";

/* List rows use muted hover color and a keyboard-focus ring. The color transition respects reduced-motion preferences. */
/**
 * Classes for a padded flex row with muted hover color and a visible keyboard-focus ring.
 */
export const listRowRecipe = cva(
  "flex items-center justify-between px-6 py-4 outline-none motion-safe:transition-colors hover:bg-muted focus-visible:ring-2 focus-visible:ring-ring",
);

/** The row recipe's variant props, mixed into `ListRowProps`. */
export type ListRowRecipeProps = VariantProps<typeof listRowRecipe>;
