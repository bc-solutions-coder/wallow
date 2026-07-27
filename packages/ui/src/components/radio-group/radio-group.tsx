import { RadioGroup as BaseRadioGroup } from "@base-ui/react/radio-group";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { radioGroupRecipe, type RadioGroupRecipeProps } from "./radio-group.styles";

/** The directions a radio group lays its radios out in. `vertical` is the default. */
export type RadioGroupOrientation = NonNullable<RadioGroupRecipeProps["orientation"]>;

/**
 * Every Base UI `RadioGroup` prop (`value`, `defaultValue`, `onValueChange`,
 * `name`, `disabled`, `readOnly`, `required`, `render` and the native div
 * attributes) plus the recipe's `orientation`.
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`.
 */
export interface RadioGroupProps
  extends Omit<ComponentProps<typeof BaseRadioGroup>, "className">, RadioGroupRecipeProps {
  readonly className?: string;
}

/**
 * The catalog's radio group: the shared state a series of `Radio.Root`s select
 * within, and the element that owns the submitted `name`/value pair.
 */
export function RadioGroup({ orientation, className, ...rest }: RadioGroupProps): ReactElement {
  return <BaseRadioGroup className={cn(radioGroupRecipe({ orientation }), className)} {...rest} />;
}
