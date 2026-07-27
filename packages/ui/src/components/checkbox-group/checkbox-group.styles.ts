import { cva, type VariantProps } from "class-variance-authority";

/*
 * No cva variant, for the same reason as the checkbox recipes: the only thing
 * that changes a group's appearance is its disabled STATE, which Base UI
 * publishes as `data-disabled` and the base string handles with a
 * `data-[disabled]:` modifier.
 */

/**
 * The group wrapper — Base UI's `CheckboxGroup`, which renders a
 * `<div role="group">` and shares one value array with the checkboxes inside it.
 */
export const checkboxGroupRecipe = cva("flex flex-col gap-2 data-[disabled]:opacity-50");

/** The recipe's variant props, mixed into `CheckboxGroupProps`. */
export type CheckboxGroupRecipeProps = VariantProps<typeof checkboxGroupRecipe>;
