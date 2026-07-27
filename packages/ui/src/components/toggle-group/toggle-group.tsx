import { ToggleGroup as BaseToggleGroup } from "@base-ui/react/toggle-group";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { toggleGroupRecipe, type ToggleGroupRecipeProps } from "./toggle-group.styles";

/**
 * Every Base UI `ToggleGroup` prop — `value`/`defaultValue` (the values of the
 * pressed toggles), `onValueChange`, `multiple`, `disabled`, `orientation`,
 * `loopFocus` and `render`.
 *
 * `className` is deliberately narrowed back to `string`, as everywhere in this
 * catalog: Base UI widens it to `string | ((state) => string | undefined)` and
 * the callback form cannot be merged with a recipe through `cn()`.
 */
export interface ToggleGroupProps
  extends Omit<ComponentProps<typeof BaseToggleGroup>, "className">, ToggleGroupRecipeProps {
  readonly className?: string;
}

/**
 * Shares one value array across several `Toggle`s (`../toggle`), each identified
 * by its `value`. This is a SINGLE-part component in Base UI — there is no
 * `ToggleGroup.Root` — so it is exported as a plain component rather than a
 * namespace object, mirroring `@base-ui/react/toggle-group` 1:1, the same shape
 * as `../checkbox-group` and `../radio-group`.
 *
 * It reaches the toggles through Base UI's own context, so this module never
 * imports `Toggle` and the two can be tree-shaken apart.
 */
export function ToggleGroup({ className, ...rest }: ToggleGroupProps): ReactElement {
  return <BaseToggleGroup className={cn(toggleGroupRecipe(), className)} {...rest} />;
}
