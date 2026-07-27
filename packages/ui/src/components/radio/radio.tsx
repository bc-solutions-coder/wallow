import { Radio as BaseRadio } from "@base-ui/react/radio";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { radioIndicatorRecipe, radioRootRecipe } from "./radio.styles";

/**
 * Every Base UI `Radio.Root` prop (`value`, `disabled`, `readOnly`, `required`,
 * `render`, `inputRef` and the native span attributes).
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes the
 * same narrowing, so a caller's `className` always means "utilities merged over
 * the recipe, last one wins".
 */
export interface RadioRootProps extends Omit<ComponentProps<typeof BaseRadio.Root>, "className"> {
  readonly className?: string;
}

/** Every Base UI `Radio.Indicator` prop, with `className` narrowed the same way. */
export interface RadioIndicatorProps extends Omit<
  ComponentProps<typeof BaseRadio.Indicator>,
  "className"
> {
  readonly className?: string;
}

function RadioRoot({ className, ...rest }: RadioRootProps): ReactElement {
  return <BaseRadio.Root className={cn(radioRootRecipe(), className)} {...rest} />;
}

function RadioIndicator({ className, ...rest }: RadioIndicatorProps): ReactElement {
  return <BaseRadio.Indicator className={cn(radioIndicatorRecipe(), className)} {...rest} />;
}

/**
 * The catalog's radio button. Multi-part components ship a single namespace
 * object whose keys mirror Base UI's part names 1:1 — `Radio.Root` renders the
 * control (a `<span role="radio">` plus a hidden `<input type="radio">`), and
 * `Radio.Indicator` renders the dot shown while it is selected.
 *
 * A radio is only meaningful inside a `RadioGroup`, which owns the selected
 * value and the shared `name`.
 */
export const Radio = {
  Root: RadioRoot,
  Indicator: RadioIndicator,
};
