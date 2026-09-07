import { cva, type VariantProps } from "class-variance-authority";

/*
 * The list card's class recipes. Style decisions live here and nowhere else —
 * this file holds no JSX and imports no React. Every utility is a semantic token
 * class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Two parts, one public component: the card surface and the divided `<ul>` it
 * clips. Only the surface is caller-styleable; the list recipe stays sealed,
 * because the hairline rhythm between rows is what makes this a list card
 * rather than a card that happens to hold a list.
 */

/** Bordered list-card surface with clipped overflow; caller attributes apply to this outer div. */
export const listCardRecipe = cva(
  "bg-card rounded-lg shadow-sm border border-border overflow-hidden",
);

/** The surface recipe's variant props, mixed into `ListCardProps`. */
export type ListCardRecipeProps = VariantProps<typeof listCardRecipe>;

/**
 * The `<ul>` inside the surface: hairlines between rows and nothing else. The
 * padding belongs to each row (`ListRow`'s `px-6 py-4`) rather than to the list,
 * so a divider bleeds to both card edges.
 */
export const listCardListRecipe = cva("divide-y divide-border");

/** The list recipe's variant props. */
export type ListCardListRecipeProps = VariantProps<typeof listCardListRecipe>;
