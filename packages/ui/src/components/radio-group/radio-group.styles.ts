import { cva, type VariantProps } from "class-variance-authority";

/**
 * The radio group's class recipe. Style decisions live here and nowhere else —
 * no JSX, no React import.
 *
 * The group is a layout element, so the recipe only owns direction, spacing and
 * the disabled treatment; every colour belongs to the radios inside it. The
 * disabled rule hangs off Base UI's `data-disabled` attribute rather than a CSS
 * pseudo-class, because the group renders a `<div>` (or whatever `render`
 * substitutes) and `:disabled` does not apply to either.
 */
export const radioGroupRecipe = cva("flex data-[disabled]:opacity-50", {
  variants: {
    orientation: {
      vertical: "flex-col gap-2",
      horizontal: "flex-row gap-4",
    },
  },
  defaultVariants: { orientation: "vertical" },
});

/** The recipe's variant props, mixed into `RadioGroupProps`. */
export type RadioGroupRecipeProps = VariantProps<typeof radioGroupRecipe>;
