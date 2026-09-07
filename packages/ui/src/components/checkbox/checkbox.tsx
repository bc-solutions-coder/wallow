import { Checkbox as BaseCheckbox } from "@base-ui/react/checkbox";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  checkboxIndicatorRecipe,
  type CheckboxIndicatorRecipeProps,
  checkboxRootRecipe,
  type CheckboxRootRecipeProps,
} from "./checkbox.styles";

/**
 * Every Base UI `Checkbox.Root` prop — `checked`/`defaultChecked`,
 * `indeterminate`, `onCheckedChange`, `name`/`value` for form submission,
 * `parent` for a Checkbox Group parent box, and `render`.
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes the
 * same narrowing.
 */
export interface CheckboxRootProps
  extends Omit<ComponentProps<typeof BaseCheckbox.Root>, "className">, CheckboxRootRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Checkbox.Indicator` prop, with `className` narrowed to `string`. */
export interface CheckboxIndicatorProps
  extends
    Omit<ComponentProps<typeof BaseCheckbox.Indicator>, "className">,
    CheckboxIndicatorRecipeProps {
  readonly className?: string;
}

function CheckboxRoot({ className, ...rest }: CheckboxRootProps): ReactElement {
  return <BaseCheckbox.Root className={cn(checkboxRootRecipe(), className)} {...rest} />;
}

function CheckboxIndicator({ className, ...rest }: CheckboxIndicatorProps): ReactElement {
  return <BaseCheckbox.Indicator className={cn(checkboxIndicatorRecipe(), className)} {...rest} />;
}

/**
 * Checkbox state and indicator parts. Root owns checked or indeterminate state; render the
 * check glyph inside Indicator and provide an accessible label.
 */
export const Checkbox = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: CheckboxRoot,
  /**
   * Displays the control's current state or selected extent.
   */
  Indicator: CheckboxIndicator,
};
