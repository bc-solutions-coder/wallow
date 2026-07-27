import { cva, type VariantProps } from "class-variance-authority";

/*
 * Neither part takes a cva VARIANT. A checkbox has no visual variant axis in
 * this catalog — everything a checkbox looks like (ticked, mixed, disabled,
 * read-only) is a STATE, and Base UI publishes states as `data-*` attributes,
 * so they belong in the base string as `data-[checked]:` / `data-[disabled]:`
 * modifiers rather than as cva variants nobody would ever pass by hand. The
 * `VariantProps` types are still exported so `CheckboxRootProps` keeps the
 * catalog-wide shape and a later variant axis is a non-breaking addition.
 */

/**
 * The checkbox box itself — Base UI's `Checkbox.Root`, which renders a `<span>`
 * with `role="checkbox"` plus a hidden `<input>` beside it.
 */
export const checkboxRootRecipe = cva(
  "inline-flex size-4 shrink-0 items-center justify-center rounded-sm border border-input bg-background data-[checked]:border-primary data-[checked]:bg-primary data-[indeterminate]:border-primary data-[indeterminate]:bg-primary data-[disabled]:opacity-50",
);

/** The root recipe's variant props, mixed into `CheckboxRootProps`. */
export type CheckboxRootRecipeProps = VariantProps<typeof checkboxRootRecipe>;

/**
 * The tick mark — Base UI's `Checkbox.Indicator`, a `<span>` that is only in the
 * DOM while the checkbox is ticked or mixed (unless `keepMounted` is set).
 */
export const checkboxIndicatorRecipe = cva(
  "flex items-center justify-center text-primary-foreground",
);

/** The indicator recipe's variant props, mixed into `CheckboxIndicatorProps`. */
export type CheckboxIndicatorRecipeProps = VariantProps<typeof checkboxIndicatorRecipe>;
