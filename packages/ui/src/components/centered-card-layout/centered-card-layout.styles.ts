import { cva, type VariantProps } from "class-variance-authority";

/*
 * The centred layout's class recipes. Style decisions live here and nowhere
 * else — this file holds no JSX and imports no React. Every utility is a
 * semantic token class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Two parts, one public component: the full-viewport centring wrapper, and the
 * fixed-width column inside it that receives the caller's props. Only the
 * column is caller-styleable; the viewport recipe stays sealed.
 */

/** The full-viewport centring wrapper — the outer `<div>`. */
export const centeredCardLayoutViewportRecipe = cva(
  "min-h-screen bg-background flex flex-col items-center justify-center px-4",
);

/** The viewport recipe's variant props. */
export type CenteredCardLayoutViewportRecipeProps = VariantProps<
  typeof centeredCardLayoutViewportRecipe
>;

/** The fixed-width column — the inner `<div>` the caller's props land on. */
export const centeredCardLayoutColumnRecipe = cva("w-full max-w-[420px]");

/** The column recipe's variant props, mixed into `CenteredCardLayoutProps`. */
export type CenteredCardLayoutColumnRecipeProps = VariantProps<
  typeof centeredCardLayoutColumnRecipe
>;
