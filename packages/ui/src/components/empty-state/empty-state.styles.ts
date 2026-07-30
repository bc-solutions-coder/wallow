import { cva, type VariantProps } from "class-variance-authority";

/*
 * The empty state's class recipes. Style decisions live here and nowhere else —
 * this file holds no JSX and imports no React. Every utility is a semantic token
 * class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Three recipes, one public component: the spacing block handed to `Card`, the
 * icon slot above the message, and the action slot below it. The card SURFACE
 * (`rounded-lg border border-border bg-card`) is deliberately absent — that is
 * `cardRecipe`'s decision, and EmptyState composes the real `Card` rather than
 * restating it.
 *
 * No type-scale or colour utility appears here either: the message and the
 * description are rendered through `Text`, so the scale and the semantic colour
 * stay that component's decision. These recipes carry layout alone — which is
 * what erases the `text-foreground/60` the two wallow-web call sites hand-roll.
 */

/**
 * The spacing/layout block passed to `Card`'s `spacing` slot, replacing its
 * default `p-6 space-y-6`. `p-12` and `text-center` are the shape both
 * wallow-web empty states already ship; the flex column is what gives the icon,
 * the message, the description and the action one rhythm instead of four
 * hand-tuned margins.
 */
export const emptyStateRecipe = cva("p-12 flex flex-col items-center gap-2 text-center");

/** The spacing recipe's variant props, mixed into `EmptyStateProps`. */
export type EmptyStateRecipeProps = VariantProps<typeof emptyStateRecipe>;

/**
 * The icon slot. `leading-none` keeps a tall emoji from dragging its own line
 * box open, and the extra `mb-2` on top of the column gap reproduces the `mb-4`
 * both call sites set under the emoji today.
 */
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
