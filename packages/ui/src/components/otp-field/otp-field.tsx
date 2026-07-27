import { OTPField as BaseOTPField } from "@base-ui/react/otp-field";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  otpFieldInputRecipe,
  otpFieldRootRecipe,
  otpFieldSeparatorRecipe,
} from "./otp-field.styles";

/**
 * Every Base UI `OTPField.Root` prop (`length` — required, `value`,
 * `defaultValue`, `onValueChange`, `onValueComplete`, `onValueInvalid`,
 * `validationType`, `normalizeValue`, `mask`, `autoSubmit`, `autoComplete`,
 * `inputMode`, `disabled`, `readOnly`, `required`, `name`, `form`, `id`,
 * `render`).
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes
 * the same narrowing (Wallow-m5aq.2.1 established it).
 */
export interface OTPFieldRootProps extends Omit<
  ComponentProps<typeof BaseOTPField.Root>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `OTPField.Input` prop, with `className` narrowed the same way. */
export interface OTPFieldInputProps extends Omit<
  ComponentProps<typeof BaseOTPField.Input>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `OTPField.Separator` prop, with `className` narrowed the same way. */
export interface OTPFieldSeparatorProps extends Omit<
  ComponentProps<typeof BaseOTPField.Separator>,
  "className"
> {
  readonly className?: string;
}

/**
 * The slot row. Owns the whole code, hands each slot its character through
 * context, and renders a `<div role="group">` plus a visually hidden
 * `<input aria-hidden>` sibling that carries `name`/`value` into form
 * submissions and holds the browser's own validation state.
 */
function OTPFieldRoot({ className, ...rest }: OTPFieldRootProps): ReactElement {
  return <BaseOTPField.Root className={cn(otpFieldRootRecipe(), className)} {...rest} />;
}

/**
 * One character slot. Must be rendered inside an `OTPFieldRoot`: it takes its
 * index from its position among the root's slots — there is no `index` prop —
 * and reads its own character and `filled` state from context. Renders an
 * `<input>`.
 */
function OTPFieldInput({ className, ...rest }: OTPFieldInputProps): ReactElement {
  return <BaseOTPField.Input className={cn(otpFieldInputRecipe(), className)} {...rest} />;
}

/**
 * The rule drawn between groups of slots (the dash in `123 - 456`). Renders a
 * `<div role="separator">`; purely decorative, and it takes no part in the
 * root's slot indexing.
 */
function OTPFieldSeparator({ className, ...rest }: OTPFieldSeparatorProps): ReactElement {
  return <BaseOTPField.Separator className={cn(otpFieldSeparatorRecipe(), className)} {...rest} />;
}

/**
 * The catalog's OTP field, as a namespace whose keys mirror Base UI's part
 * names 1:1 (`OTPField.Root`, `.Input`, `.Separator`) so a reader can move
 * between Base UI's docs and this catalog without a translation step.
 */
export const OTPField = {
  Root: OTPFieldRoot,
  Input: OTPFieldInput,
  Separator: OTPFieldSeparator,
};
