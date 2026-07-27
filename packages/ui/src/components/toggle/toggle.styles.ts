import { cva, type VariantProps } from "class-variance-authority";

/**
 * The toggle button — Base UI's `Toggle`, a `<button>` that carries
 * `data-pressed` while on and `data-disabled` when off-limits.
 *
 * No cva variant: a toggle has no visual variant axis in this catalog. Pressed
 * and disabled are STATES, published by Base UI as `data-*` attributes, so they
 * belong in the base string as `data-[pressed]:` / `data-[disabled]:` modifiers
 * rather than as cva variants nobody would pass by hand. `VariantProps` is still
 * exported so the part keeps the catalog-wide props shape and a later variant
 * axis stays a non-breaking addition.
 */
export const toggleRecipe = cva(
  "inline-flex items-center justify-center gap-2 rounded-md px-3 py-2 text-sm font-medium text-foreground transition-colors data-[pressed]:bg-accent data-[pressed]:text-accent-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `ToggleProps`. */
export type ToggleRecipeProps = VariantProps<typeof toggleRecipe>;
