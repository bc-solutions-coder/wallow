import { cva, type VariantProps } from "class-variance-authority";

/**
 * The Fieldset anatomy's class recipes. JSX-free, token-only, no variant axis —
 * the same rules the rest of the catalog's `*.styles.ts` files follow.
 */

/**
 * The group box. Wider spacing than the `space-y-2` of a single field row,
 * because what it stacks is whole fields rather than a label over a control.
 */
export const fieldsetRootRecipe = cva("space-y-4");

/** The recipe's variant props, mixed into `FieldsetProps`. */
export type FieldsetRootRecipeProps = VariantProps<typeof fieldsetRootRecipe>;

/**
 * The group's heading. One step larger than a field label so the hierarchy
 * "this legend names these labels" is visible, and it carries the same disabled
 * treatment because `Fieldset.Root disabled` propagates to it.
 */
export const fieldsetLegendRecipe = cva("text-base font-medium text-foreground");

/** The recipe's variant props, mixed into `FieldsetLegendProps`. */
export type FieldsetLegendRecipeProps = VariantProps<typeof fieldsetLegendRecipe>;
