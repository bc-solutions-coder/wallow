import { cva, type VariantProps } from "class-variance-authority";

/**
 * The multi-line text control's class recipe. Style decisions live here and
 * nowhere else — this file holds no JSX and imports no React, so a recipe can be
 * read (and diffed) without the component around it.
 *
 * The base is `inputRecipe`'s string verbatim: the one measured call site
 * (CreateInquiryForm's `inquiry-message`) hand-carries exactly that string on a
 * bare `<textarea>` because no catalog Textarea existed, so sharing it is the
 * compat guarantee — the single-line and multi-line controls must not drift.
 * Only `min-h-20` (an unstyled textarea stands ~2 rows tall) and `resize-y`
 * (keep the user's drag off the horizontal axis so a long answer never breaks
 * the form's column width) are added on top.
 *
 * Like the input, this control has no variant axis, so `TextareaRecipeProps` is
 * an empty prop set today; it stays in `TextareaProps` so the component keeps
 * the catalog's uniform shape and a future variant is a one-file change.
 *
 * Every utility is a semantic token class from `@bc-solutions-coder/styles`; no
 * raw colour values. The disabled treatment hangs off the `data-disabled` state
 * attribute the component stamps itself rather than the `:disabled`
 * pseudo-class, matching how every Base UI-backed part in this catalog styles
 * state.
 */
export const textareaRecipe = cva(
  "w-full min-h-20 resize-y rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `TextareaProps`. */
export type TextareaRecipeProps = VariantProps<typeof textareaRecipe>;
