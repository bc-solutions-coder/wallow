import { NumberField as BaseNumberField } from "@base-ui/react/number-field";
import type { ComponentProps, ReactElement } from "react";
import { cn } from "../../core/cn";
import {
  numberFieldDecrementRecipe,
  numberFieldGroupRecipe,
  numberFieldIncrementRecipe,
  numberFieldInputRecipe,
  numberFieldRootRecipe,
  numberFieldScrubAreaCursorRecipe,
  numberFieldScrubAreaRecipe,
} from "./number-field.styles";

/**
 * Every Base UI `NumberField.Root` prop (`value`, `defaultValue`,
 * `onValueChange`, `onValueCommitted`, `min`, `max`, `step`, `smallStep`,
 * `largeStep`, `snapOnStep`, `allowOutOfRange`, `allowWheelScrub`, `format`,
 * `locale`, `disabled`, `readOnly`, `required`, `name`, `form`, `id`,
 * `inputRef`, `render`).
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes
 * the same narrowing (Wallow-m5aq.2.1 established it).
 */
export interface NumberFieldRootProps extends Omit<
  ComponentProps<typeof BaseNumberField.Root>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `NumberField.Group` prop, with `className` narrowed the same way. */
export interface NumberFieldGroupProps extends Omit<
  ComponentProps<typeof BaseNumberField.Group>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `NumberField.Decrement` prop, with `className` narrowed the same way. */
export interface NumberFieldDecrementProps extends Omit<
  ComponentProps<typeof BaseNumberField.Decrement>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `NumberField.Input` prop, with `className` narrowed the same way. */
export interface NumberFieldInputProps extends Omit<
  ComponentProps<typeof BaseNumberField.Input>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `NumberField.Increment` prop, with `className` narrowed the same way. */
export interface NumberFieldIncrementProps extends Omit<
  ComponentProps<typeof BaseNumberField.Increment>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `NumberField.ScrubArea` prop, with `className` narrowed the same way. */
export interface NumberFieldScrubAreaProps extends Omit<
  ComponentProps<typeof BaseNumberField.ScrubArea>,
  "className"
> {
  readonly className?: string;
}

/** Every Base UI `NumberField.ScrubAreaCursor` prop, with `className` narrowed the same way. */
export interface NumberFieldScrubAreaCursorProps extends Omit<
  ComponentProps<typeof BaseNumberField.ScrubAreaCursor>,
  "className"
> {
  readonly className?: string;
}

/**
 * The wrapper that owns the value and hands it to every other part through
 * context. Renders a `<div>`.
 */
function NumberFieldRoot({ className, ...rest }: NumberFieldRootProps): ReactElement {
  return <BaseNumberField.Root className={cn(numberFieldRootRecipe(), className)} {...rest} />;
}

/**
 * The stepper shell: the box drawn around the decrement button, the input and
 * the increment button. Renders a `<div>`.
 */
function NumberFieldGroup({ className, ...rest }: NumberFieldGroupProps): ReactElement {
  return <BaseNumberField.Group className={cn(numberFieldGroupRecipe(), className)} {...rest} />;
}

/** The step-down button. Renders a `<button>`. */
function NumberFieldDecrement({ className, ...rest }: NumberFieldDecrementProps): ReactElement {
  return (
    <BaseNumberField.Decrement className={cn(numberFieldDecrementRecipe(), className)} {...rest} />
  );
}

/** The text control the user types into. Renders an `<input>`. */
function NumberFieldInput({ className, ...rest }: NumberFieldInputProps): ReactElement {
  return <BaseNumberField.Input className={cn(numberFieldInputRecipe(), className)} {...rest} />;
}

/** The step-up button. Renders a `<button>`. */
function NumberFieldIncrement({ className, ...rest }: NumberFieldIncrementProps): ReactElement {
  return (
    <BaseNumberField.Increment className={cn(numberFieldIncrementRecipe(), className)} {...rest} />
  );
}

/**
 * A surface the user can drag horizontally (or vertically) to change the value.
 * Renders a `<span>`; usually wraps the field's label.
 */
function NumberFieldScrubArea({ className, ...rest }: NumberFieldScrubAreaProps): ReactElement {
  return (
    <BaseNumberField.ScrubArea className={cn(numberFieldScrubAreaRecipe(), className)} {...rest} />
  );
}

/**
 * The custom cursor shown while scrubbing, in place of the native one. Must be
 * rendered inside a `NumberFieldScrubArea`. Renders a `<span>`.
 */
function NumberFieldScrubAreaCursor({
  className,
  ...rest
}: NumberFieldScrubAreaCursorProps): ReactElement {
  return (
    <BaseNumberField.ScrubAreaCursor
      className={cn(numberFieldScrubAreaCursorRecipe(), className)}
      {...rest}
    />
  );
}

/**
 * The catalog's number field, as a namespace whose keys mirror Base UI's part
 * names 1:1 (`NumberField.Root`, `.Group`, `.Decrement`, `.Input`,
 * `.Increment`, `.ScrubArea`, `.ScrubAreaCursor`) so a reader can move between
 * Base UI's docs and this catalog without a translation step.
 */
export const NumberField = {
  Root: NumberFieldRoot,
  Group: NumberFieldGroup,
  Decrement: NumberFieldDecrement,
  Input: NumberFieldInput,
  Increment: NumberFieldIncrement,
  ScrubArea: NumberFieldScrubArea,
  ScrubAreaCursor: NumberFieldScrubAreaCursor,
};
