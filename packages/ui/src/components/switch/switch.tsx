import { Switch as BaseSwitch } from "@base-ui/react/switch";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { switchRootRecipe, switchThumbRecipe } from "./switch.styles";

/**
 * Every Base UI `Switch.Root` prop (`checked`, `defaultChecked`,
 * `onCheckedChange`, `disabled`, `readOnly`, `required`, `name`, `value`,
 * `render`, `nativeButton`).
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
 * An on/off input composed from Root and Thumb. Root owns checked state and needs an
 * accessible label; Thumb follows the checked state.
 */
export const Switch = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: SwitchRoot,
  /**
   * The movable or state-positioned part of the control.
   */
  Thumb: SwitchThumb,
};
