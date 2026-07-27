import { cva, type VariantProps } from "class-variance-authority";

/*
 * No part takes a cva VARIANT. A number field has no visual variant axis in
 * this catalog — everything it looks like (disabled, read-only, at a boundary,
 * scrubbing) is a STATE, and Base UI publishes states as `data-*` attributes,
 * so they belong in the base string as `data-[disabled]:` modifiers rather than
 * as cva variants nobody would pass by hand. The `VariantProps` types are still
 * exported so the prop interfaces keep the catalog-wide shape and a later
 * variant axis is a non-breaking addition.
 */

/** The outer wrapper — Base UI's `NumberField.Root`, a `<div>`. */
export const numberFieldRootRecipe = cva("flex w-full flex-col gap-1.5");

/** The recipe's variant props, mixed into `NumberFieldRootProps`. */
export type NumberFieldRootRecipeProps = VariantProps<typeof numberFieldRootRecipe>;

/** The stepper shell — Base UI's `NumberField.Group`, a `<div>`. */
export const numberFieldGroupRecipe = cva(
  "inline-flex items-center overflow-hidden rounded-md border border-border bg-background data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `NumberFieldGroupProps`. */
export type NumberFieldGroupRecipeProps = VariantProps<typeof numberFieldGroupRecipe>;

/** The step-down button — Base UI's `NumberField.Decrement`, a `<button>`. */
export const numberFieldDecrementRecipe = cva(
  "inline-flex size-9 shrink-0 items-center justify-center border-r border-border text-foreground hover:bg-accent hover:text-accent-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `NumberFieldDecrementProps`. */
export type NumberFieldDecrementRecipeProps = VariantProps<typeof numberFieldDecrementRecipe>;

/** The text control — Base UI's `NumberField.Input`, an `<input>`. */
export const numberFieldInputRecipe = cva(
  "h-9 w-16 bg-transparent px-2 text-center text-sm text-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `NumberFieldInputProps`. */
export type NumberFieldInputRecipeProps = VariantProps<typeof numberFieldInputRecipe>;

/** The step-up button — Base UI's `NumberField.Increment`, a `<button>`. */
export const numberFieldIncrementRecipe = cva(
  "inline-flex size-9 shrink-0 items-center justify-center border-l border-border text-foreground hover:bg-accent hover:text-accent-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `NumberFieldIncrementProps`. */
export type NumberFieldIncrementRecipeProps = VariantProps<typeof numberFieldIncrementRecipe>;

/** The drag-to-change surface — Base UI's `NumberField.ScrubArea`, a `<span>`. */
export const numberFieldScrubAreaRecipe = cva(
  "inline-block cursor-ew-resize select-none text-sm font-medium text-foreground data-[disabled]:opacity-50",
);

/** The recipe's variant props, mixed into `NumberFieldScrubAreaProps`. */
export type NumberFieldScrubAreaRecipeProps = VariantProps<typeof numberFieldScrubAreaRecipe>;

/** The custom drag cursor — Base UI's `NumberField.ScrubAreaCursor`, a `<span>`. */
export const numberFieldScrubAreaCursorRecipe = cva(
  "pointer-events-none text-foreground drop-shadow-sm",
);

/** The recipe's variant props, mixed into `NumberFieldScrubAreaCursorProps`. */
export type NumberFieldScrubAreaCursorRecipeProps = VariantProps<
  typeof numberFieldScrubAreaCursorRecipe
>;
