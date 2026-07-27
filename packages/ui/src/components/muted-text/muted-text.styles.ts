import { cva, type VariantProps } from "class-variance-authority";

/*
 * The muted paragraph's class recipe. Style decisions live here and nowhere
 * else — this file holds no JSX and imports no React. Every utility is a
 * semantic token class from `@bc-solutions-coder/styles`; no raw colour values.
 */

/** The muted paragraph — the strongest single recipe in the auth inventory. */
export const mutedTextRecipe = cva("text-sm text-muted-foreground");

/** The recipe's variant props, mixed into `MutedTextProps`. */
export type MutedTextRecipeProps = VariantProps<typeof mutedTextRecipe>;
