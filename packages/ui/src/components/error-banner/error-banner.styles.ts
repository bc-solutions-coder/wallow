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

/*
 * `surface` is the ONE axis both recipes take, and it says which surface the
 * banner was composed ONTO rather than anything about the banner itself. A 10%
 * destructive tint under destructive text reads on the page background and
 * very nearly disappears on the inverted sidebar surface — so the message a
 * reader must not miss becomes the least legible thing on the rail. Nothing in
 * the DOM says which surface a caller dropped the banner onto, so it is a cva
 * variant that must be passed, not a `data-*` modifier the recipe could read.
 *
 * What the `sidebar` arm does about it is drop the tint: on the page a 10% fill
 * under destructive text is enough to mark the message, but a tint only reads
 * against a light surface, so on the rail the banner takes the destructive
 * token at FULL strength and flips its text to `destructive-foreground`. Same
 * two ideas — a coloured fill and a legible message — restated in the strength
 * the inverted surface needs.
 */

/** The banner surface — the outer `<div>`, the only part a caller can style. */
export const errorBannerRecipe = cva("rounded-md border border-destructive p-3", {
  variants: {
    surface: {
      page: "bg-destructive/10",
      sidebar: "bg-destructive",
    },
  },
  defaultVariants: { surface: "page" },
});

/** The surface recipe's variant props, mixed into `ErrorBannerProps`. */
export type ErrorBannerRecipeProps = VariantProps<typeof errorBannerRecipe>;

/** The message text — the inner `<p>` wrapping the banner's children. */
export const errorBannerTextRecipe = cva("text-sm", {
  variants: {
    surface: {
      page: "text-destructive",
      sidebar: "text-destructive-foreground",
    },
  },
  defaultVariants: { surface: "page" },
});

/** The text recipe's variant props. */
export type ErrorBannerTextRecipeProps = VariantProps<typeof errorBannerTextRecipe>;
