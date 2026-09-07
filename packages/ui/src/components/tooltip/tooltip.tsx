import { Tooltip as BaseTooltip } from "@base-ui/react/tooltip";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  tooltipArrowRecipe,
  type TooltipArrowRecipeProps,
  tooltipPopupRecipe,
  type TooltipPopupRecipeProps,
  tooltipPositionerRecipe,
  type TooltipPositionerRecipeProps,
  tooltipTriggerRecipe,
  type TooltipTriggerRecipeProps,
  tooltipViewportRecipe,
  type TooltipViewportRecipeProps,
} from "./tooltip.styles";

/**
 * Every Base UI `Tooltip.Provider` prop — `delay`, `closeDelay` and `timeout`,
 * all in milliseconds. Surfaced as a named type so a consumer can hold a
 * fork-wide delay policy in one typed object rather than repeating literals.
 */
export type TooltipProviderProps = ComponentProps<typeof BaseTooltip.Provider>;

/** Every Base UI `Tooltip.Root` prop, generic over the trigger payload type. */
export type TooltipRootProps<Payload = unknown> = Parameters<typeof BaseTooltip.Root<Payload>>[0];

/** Every Base UI `Tooltip.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type TooltipPortalProps = ComponentProps<typeof BaseTooltip.Portal>;

/** Every Base UI `Tooltip.Trigger` prop, with `className` narrowed to `string`. */
export interface TooltipTriggerProps<Payload = unknown>
  extends
    Omit<Parameters<typeof BaseTooltip.Trigger<Payload>>[0], "className">,
    TooltipTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Tooltip.Positioner` prop, with `className` narrowed to `string`. */
export interface TooltipPositionerProps
  extends
    Omit<ComponentProps<typeof BaseTooltip.Positioner>, "className">,
    TooltipPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Tooltip.Popup` prop, with `className` narrowed to `string`. */
export interface TooltipPopupProps
  extends Omit<ComponentProps<typeof BaseTooltip.Popup>, "className">, TooltipPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Tooltip.Arrow` prop, with `className` narrowed to `string`. */
export interface TooltipArrowProps
  extends Omit<ComponentProps<typeof BaseTooltip.Arrow>, "className">, TooltipArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Tooltip.Viewport` prop, with `className` narrowed to `string`. */
export interface TooltipViewportProps
  extends
    Omit<ComponentProps<typeof BaseTooltip.Viewport>, "className">,
    TooltipViewportRecipeProps {
  readonly className?: string;
}

function TooltipTrigger<Payload>({
  className,
  ...rest
}: TooltipTriggerProps<Payload>): ReactElement {
  return <BaseTooltip.Trigger className={cn(tooltipTriggerRecipe(), className)} {...rest} />;
}

function TooltipPositioner({ className, ...rest }: TooltipPositionerProps): ReactElement {
  return <BaseTooltip.Positioner className={cn(tooltipPositionerRecipe(), className)} {...rest} />;
}

function TooltipPopup({ className, ...rest }: TooltipPopupProps): ReactElement {
  return <BaseTooltip.Popup className={cn(tooltipPopupRecipe(), className)} {...rest} />;
}

function TooltipArrow({ className, ...rest }: TooltipArrowProps): ReactElement {
  return <BaseTooltip.Arrow className={cn(tooltipArrowRecipe(), className)} {...rest} />;
}

function TooltipViewport({ className, ...rest }: TooltipViewportProps): ReactElement {
  return <BaseTooltip.Viewport className={cn(tooltipViewportRecipe(), className)} {...rest} />;
}

/**
 * Supplementary text anchored to a trigger. Compose Root with Trigger and Portal > Positioner
 * > Popup; Provider shares delay settings. Give the trigger its own accessible name and keep
 * essential information outside the tooltip.
 */
export const Tooltip = {
  /**
   * Shares behavior settings with descendant component roots.
   */
  Provider: BaseTooltip.Provider,
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BaseTooltip.Root,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: TooltipTrigger,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BaseTooltip.Portal,
  /**
   * Positions the popup relative to its anchor; render inside Portal.
   */
  Positioner: TooltipPositioner,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: TooltipPopup,
  /**
   * Draws the popup's pointer toward its anchor.
   */
  Arrow: TooltipArrow,
  /**
   * Contains the visible popup region and its layout.
   */
  Viewport: TooltipViewport,
  /**
   * Handle class for sharing popup state with detached triggers.
   */
  Handle: BaseTooltip.Handle,
  /**
   * Creates a handle that connects a popup to triggers outside its Root.
   */
  createHandle: BaseTooltip.createHandle,
};
