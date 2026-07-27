import { cva, type VariantProps } from "class-variance-authority";

/*
 * No part takes a cva VARIANT. An OTP field has no visual variant axis in this
 * catalog — everything it looks like (disabled, read-only, a slot holding a
 * character, the code being complete) is a STATE, and Base UI publishes states
 * as `data-*` attributes, so they belong in the base string as `data-[...]`
 * modifiers rather than as cva variants nobody would pass by hand. The
 * `VariantProps` types are still exported so the prop interfaces keep the
 * catalog-wide shape and a later variant axis is a non-breaking addition.
 */

/**
 * The slot row — Base UI's `OTPField.Root`, a `<div role="group">`.
 *
 * The disabled dimming lives here and nowhere else: the slots carry
 * `data-disabled` too, so repeating the rule on them would compound the
 * opacity where they overlap the row.
 */
export const otpFieldRootRecipe = cva("flex items-center gap-2 data-[disabled]:opacity-50");

/** The recipe's variant props, mixed into `OTPFieldRootProps`. */
export type OTPFieldRootRecipeProps = VariantProps<typeof otpFieldRootRecipe>;

/**
 * One character slot — Base UI's `OTPField.Input`, an `<input>`.
 *
 * `data-[filled]` is the only state treatment: Base UI stamps `data-focused` on
 * every slot whenever any one of them holds focus, so a `data-[focused]:` rule
 * here would light up the whole row instead of the caret's slot.
 */
export const otpFieldInputRecipe = cva(
  "size-10 shrink-0 rounded-md border border-border bg-background text-center text-sm text-foreground data-[filled]:border-primary",
);

/** The recipe's variant props, mixed into `OTPFieldInputProps`. */
export type OTPFieldInputRecipeProps = VariantProps<typeof otpFieldInputRecipe>;

/**
 * The rule drawn between slot groups — Base UI's `OTPField.Separator`, a `<div>`.
 *
 * Its two sizes hang off Base UI's own `data-orientation` rather than a cva
 * variant, so `orientation` is set once on the part — where it also drives
 * `aria-orientation` — instead of being kept in step in two places.
 */
export const otpFieldSeparatorRecipe = cva(
  "shrink-0 bg-border data-[orientation=horizontal]:h-px data-[orientation=horizontal]:w-2 data-[orientation=vertical]:h-4 data-[orientation=vertical]:w-px",
);

/** The recipe's variant props, mixed into `OTPFieldSeparatorProps`. */
export type OTPFieldSeparatorRecipeProps = VariantProps<typeof otpFieldSeparatorRecipe>;
