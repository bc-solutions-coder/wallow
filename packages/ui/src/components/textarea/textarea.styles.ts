import { cva, type VariantProps } from "class-variance-authority";

/** Textarea classes with a minimum height, semantic colors, focus treatment, and disabled-state styling. */
export const textareaRecipe = cva(
  "w-full min-h-20 resize-y rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `TextareaProps`. */
export type TextareaRecipeProps = VariantProps<typeof textareaRecipe>;
