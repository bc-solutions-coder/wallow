import { cva, type VariantProps } from "class-variance-authority";

/** Arranges an empty state’s icon, message, supporting copy, and actions in a centered column. */

export const emptyStateRecipe = cva("p-12 flex flex-col items-center gap-2 text-center");

/** The spacing recipe's variant props, mixed into `EmptyStateProps`. */
export type EmptyStateRecipeProps = VariantProps<typeof emptyStateRecipe>;

/** Decorative icon spacing with a compact line height. */
export const emptyStateIconRecipe = cva("text-7xl leading-none mb-2");

/** The icon recipe's variant props. */
export type EmptyStateIconRecipeProps = VariantProps<typeof emptyStateIconRecipe>;

/**
 * The action slot below the copy — the "create your first one" call to action.
 * `mt-4` sets it apart from the sentence it answers; the row exists so a caller
 * can pass two buttons.
 */
export const emptyStateActionRecipe = cva("mt-4 flex items-center justify-center gap-3");

/** The action recipe's variant props. */
export type EmptyStateActionRecipeProps = VariantProps<typeof emptyStateActionRecipe>;
