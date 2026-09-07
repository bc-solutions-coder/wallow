import { cva, type VariantProps } from "class-variance-authority";

/**
 * Small muted-link classes that switch to the normal foreground on hover.
 */
export const quietLinkRecipe = cva("text-sm text-muted-foreground hover:text-foreground");

/**
 * VariantProps for the quiet-link recipe, which currently defines no variant axes.
 */
export type QuietLinkRecipeProps = VariantProps<typeof quietLinkRecipe>;
