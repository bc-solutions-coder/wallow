import { Switch as BaseSwitch } from "@base-ui/react/switch";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { switchRootRecipe, switchThumbRecipe } from "./switch.styles";

/**
 * Every Base UI `Switch.Root` prop (`checked`, `defaultChecked`,
 * `onCheckedChange`, `disabled`, `readOnly`, `required`, `name`, `value`,
 * `render`, `nativeButton`).
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes
 * the same narrowing (Wallow-m5aq.2.1 established it).
 */
export interface SwitchRootProps extends Omit<ComponentProps<typeof BaseSwitch.Root>, "className"> {
  readonly className?: string;
}

/** Every Base UI `Switch.Thumb` prop, with `className` narrowed the same way. */
export interface SwitchThumbProps extends Omit<
  ComponentProps<typeof BaseSwitch.Thumb>,
  "className"
> {
  readonly className?: string;
}

/**
 * The switch track. Renders a `<span role="switch">` plus a visually hidden
 * `<input type="checkbox">` that carries `name`/`value` into form submissions.
 */
function SwitchRoot({ className, ...rest }: SwitchRootProps): ReactElement {
  return <BaseSwitch.Root className={cn(switchRootRecipe(), className)} {...rest} />;
}

/**
 * The sliding knob. Must be rendered inside a `SwitchRoot`: it reads the root's
 * checked/disabled state from context and mirrors it onto its own `data-*`
 * attributes.
 */
function SwitchThumb({ className, ...rest }: SwitchThumbProps): ReactElement {
  return <BaseSwitch.Thumb className={cn(switchThumbRecipe(), className)} {...rest} />;
}

/**
 * The catalog's switch, as a namespace whose keys mirror Base UI's part names
 * 1:1 (`Switch.Root`, `Switch.Thumb`) so a reader can move between Base UI's
 * docs and this catalog without a translation step.
 */
export const Switch = {
  Root: SwitchRoot,
  Thumb: SwitchThumb,
};
