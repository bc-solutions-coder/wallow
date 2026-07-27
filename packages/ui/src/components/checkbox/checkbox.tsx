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
 * The catalog's checkbox, as ONE namespace object whose keys mirror Base UI's
 * part names 1:1 (`Checkbox.Root`, `Checkbox.Indicator`) — the catalog-wide
 * convention for multi-part components, so a caller who knows the Base UI docs
 * already knows this API.
 *
 * Pair it with `CheckboxGroup` (`../checkbox-group`) to drive several boxes from
 * one value array; the two talk to each other through Base UI's own context, so
 * neither component imports the other.
 */
export const Checkbox = {
  Root: CheckboxRoot,
  Indicator: CheckboxIndicator,
};
