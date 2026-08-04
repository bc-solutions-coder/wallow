import { cva, type VariantProps } from "class-variance-authority";

/*
 * The notice banner's class recipe — the non-destructive sibling of
 * `error-banner.styles.ts`. Style decisions live here and nowhere else; this
 * file holds no JSX and imports no React. Every utility is a semantic token
 * class from `@bc-solutions-coder/styles`; no raw colour values.
 *
 * Why a sibling component rather than a `tone` arm on `ErrorBanner`: that
 * component's `surface` axis exists to keep a message the reader MUST NOT miss
 * legible on the inverted sidebar rail, where a 10% tint disappears. A notice is
 * not that message, and none of its six call sites is on the rail. Folding
 * `tone` into `ErrorBanner` would produce four `tone x surface` arms, two of
 * them unreached and unreasoned.
 *
 * There is deliberately ONE recipe here where ErrorBanner has two. ErrorBanner
 * splits a second recipe onto an inner `<p>` so a caller override cannot reach
 * the message; a notice body is not always one paragraph — LoginScreen's warning
 * banner carries a heading plus an action link — so the children pass through
 * and the caller composes `Text` inside.
 */

/** The banner surface — the outer `<div>`, the only part there is. */
export const noticeBannerRecipe = cva("rounded-md border p-3", {
  variants: {
    tone: {
      success: "border-success bg-success/10",
      warning: "border-warning bg-warning/10",
    },
  },
  defaultVariants: { tone: "success" },
});

/** The recipe's variant props, mixed into `NoticeBannerProps`. */
export type NoticeBannerRecipeProps = VariantProps<typeof noticeBannerRecipe>;
