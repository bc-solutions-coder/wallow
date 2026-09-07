import { cva, type VariantProps } from "class-variance-authority";

/** Text-input classes for sizing, semantic colors, focus, and disabled state. Invalid-state styling belongs to Field.Control. */
export const inputRecipe = cva(
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `InputProps`. */
export type InputRecipeProps = VariantProps<typeof inputRecipe>;
