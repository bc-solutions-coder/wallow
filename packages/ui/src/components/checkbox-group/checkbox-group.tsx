import { CheckboxGroup as BaseCheckboxGroup } from "@base-ui/react/checkbox-group";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { checkboxGroupRecipe, type CheckboxGroupRecipeProps } from "./checkbox-group.styles";

/**
 * Every Base UI `CheckboxGroup` prop — `value`/`defaultValue` (the names of the
 * ticked boxes), `onValueChange`, `allValues` (required to drive a `parent`
 * checkbox) and `disabled`.
 *
 * `className` is deliberately narrowed back to `string`, as everywhere in this
 * catalog: Base UI widens it to `string | ((state) => string | undefined)` and
 * the callback form cannot be merged with a recipe through `cn()`.
 */
export interface CheckboxGroupProps
  extends Omit<ComponentProps<typeof BaseCheckboxGroup>, "className">, CheckboxGroupRecipeProps {
  readonly className?: string;
}

/**
 * Shares one value array across several `Checkbox.Root`s (`../checkbox`), each
 * identified by its `name`. This is a SINGLE-part component in Base UI — there
 * is no `CheckboxGroup.Root` — so it is exported as a plain component rather
 * than a namespace object, mirroring `@base-ui/react/checkbox-group` 1:1.
 *
 * It reaches the checkboxes through Base UI's own context, so this module never
 * imports `Checkbox` and the two can be tree-shaken apart.
 */
export function CheckboxGroup({ className, ...rest }: CheckboxGroupProps): ReactElement {
  return <BaseCheckboxGroup className={cn(checkboxGroupRecipe(), className)} {...rest} />;
}
