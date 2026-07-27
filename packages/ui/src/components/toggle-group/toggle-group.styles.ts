import { cva, type VariantProps } from "class-variance-authority";

/**
 * The group wrapper — Base UI's `ToggleGroup`, which renders a
 * `<div role="group">` and shares one value array with the toggles inside it.
 *
 * No cva variant, for the same reason as the toggle recipe: orientation and
 * disabled arrive as `data-orientation` / `data-disabled` attributes, so the
 * horizontal/vertical axis is a `data-[orientation=vertical]:` modifier in the
 * base string rather than a cva variant a caller would have to keep in step with
 * the `orientation` prop.
 */
export const toggleGroupRecipe = cva(
  "inline-flex items-center gap-1 rounded-md data-[orientation=vertical]:flex-col data-[orientation=vertical]:items-stretch data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `ToggleGroupProps`. */
export type ToggleGroupRecipeProps = VariantProps<typeof toggleGroupRecipe>;
