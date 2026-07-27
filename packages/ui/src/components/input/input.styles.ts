import { cva, type VariantProps } from "class-variance-authority";

/**
 * The text input's class recipe. Style decisions live here and nowhere else —
 * this file holds no JSX and imports no React, so a recipe can be read (and
 * diffed) without the component around it.
 *
 * The input has no variant axis: all 23 call sites across wallow-auth and
 * wallow-web render the one field treatment, so inventing a `size`/`tone` axis
 * with no consumer would be speculative. `InputRecipeProps` is therefore an
 * empty prop set today; it stays in `InputProps` so the component keeps the
 * catalog's uniform shape and a future variant is a one-file change.
 *
 * Every utility is a semantic token class from `@bc-solutions-coder/styles`; no
 * raw colour values. The disabled treatment hangs off Base UI's `data-disabled`
 * state attribute rather than the `:disabled` pseudo-class, so it still applies
 * when the caller composes the input onto another element via `render`.
 */
export const inputRecipe = cva(
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `InputProps`. */
export type InputRecipeProps = VariantProps<typeof inputRecipe>;
