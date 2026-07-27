import { cva, type VariantProps } from "class-variance-authority";

/*
 * The error banner's class recipes. Style decisions live here and nowhere else
 * — this file holds no JSX and imports no React. Every utility is a semantic
 * token class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * The banner is TWO styled parts even though it has one public component: the
 * wrapper `<div>` the caller's `className` and `data-testid` land on, and the
 * inner `<p>` the caller cannot reach. Splitting them into two recipes is what
 * keeps a caller override off the paragraph.
 */

/** The banner surface — the outer `<div>`, the only part a caller can style. */
export const errorBannerRecipe = cva("rounded-md border border-destructive bg-destructive/10 p-3");

/** The surface recipe's variant props, mixed into `ErrorBannerProps`. */
export type ErrorBannerRecipeProps = VariantProps<typeof errorBannerRecipe>;

/** The message text — the inner `<p>` wrapping the banner's children. */
export const errorBannerTextRecipe = cva("text-sm text-destructive");

/** The text recipe's variant props. */
export type ErrorBannerTextRecipeProps = VariantProps<typeof errorBannerTextRecipe>;
