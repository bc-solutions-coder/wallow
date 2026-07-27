import { Toggle as BaseToggle } from "@base-ui/react/toggle";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { toggleRecipe, type ToggleRecipeProps } from "./toggle.styles";

/**
 * Every Base UI `Toggle` prop — `pressed`/`defaultPressed`, `onPressedChange`,
 * `disabled`, `value` (the name this button answers to inside a
 * `../toggle-group`), `render`, `nativeButton` and the native button
 * attributes.
 *
 * `className` is deliberately narrowed back to `string`, as everywhere in this
 * catalog: Base UI widens it to `string | ((state) => string | undefined)` and
 * the callback form cannot be merged with a recipe through `cn()`.
 */
export interface ToggleProps
  extends Omit<ComponentProps<typeof BaseToggle>, "className">, ToggleRecipeProps {
  readonly className?: string;
}

/**
 * A two-state button. This is a SINGLE-part component in Base UI — there is no
 * `Toggle.Root` — so it is exported as a plain component rather than a namespace
 * object, mirroring `@base-ui/react/toggle` 1:1.
 *
 * Standing alone it owns its own pressed state; given a `value` and placed
 * inside a `ToggleGroup` it hands that state to the group through Base UI's own
 * context, so this module never imports `ToggleGroup` and the two tree-shake
 * apart.
 */
export function Toggle({ className, ...rest }: ToggleProps): ReactElement {
  return <BaseToggle className={cn(toggleRecipe(), className)} {...rest} />;
}
