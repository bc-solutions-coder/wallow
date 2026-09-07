import { cva, type VariantProps } from "class-variance-authority";

import { inputRecipe } from "../input/input.styles";

/** Field parts use semantic tokens and Base UI data attributes. Field.Control composes the shared input recipe and adds validation styling. */

/** Vertical spacing between the field label, control, and supporting text. */
export const fieldRootRecipe = cva("space-y-2");

/** The recipe's variant props, mixed into `FieldProps`. */
export type FieldRootRecipeProps = VariantProps<typeof fieldRootRecipe>;

/** Field-label typography and disabled-state color. */
export const fieldLabelRecipe = cva(
  "text-sm font-medium text-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `LabelProps`. */
export type FieldLabelRecipeProps = VariantProps<typeof fieldLabelRecipe>;

/** Input classes plus the invalid-state border for a control inside Field. */
export const fieldControlRecipe = cva(`${inputRecipe()} data-[invalid]:border-destructive`);

/** The recipe's variant props, mixed into `FieldControlProps`. */
export type FieldControlRecipeProps = VariantProps<typeof fieldControlRecipe>;

/** The field's helper paragraph — the shared muted-body treatment. */
export const fieldDescriptionRecipe = cva("text-sm text-muted-foreground");

/** The recipe's variant props, mixed into `FieldDescriptionProps`. */
export type FieldDescriptionRecipeProps = VariantProps<typeof fieldDescriptionRecipe>;

/**
 * The field's validation message. Matches the destructive body text
 * `ErrorBanner` already renders, so a field-level and a form-level error read
 * as the same thing.
 */
export const fieldErrorRecipe = cva("text-sm text-destructive");

/** The recipe's variant props, mixed into `FieldErrorProps`. */
export type FieldErrorRecipeProps = VariantProps<typeof fieldErrorRecipe>;

/**
 * A single control grouped with its own label inside a field — Base UI's
 * `Field.Item`, used for the members of a checkbox or radio group. Those read
 * as a control beside its label, so this row is horizontal where the field row
 * above is vertical.
 */
export const fieldItemRecipe = cva("flex items-center gap-2");

/** The recipe's variant props, mixed into `FieldItemProps`. */
export type FieldItemRecipeProps = VariantProps<typeof fieldItemRecipe>;
