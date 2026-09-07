import { Popover as BasePopover } from "@base-ui/react/popover";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  popoverArrowRecipe,
  type PopoverArrowRecipeProps,
  popoverBackdropRecipe,
  type PopoverBackdropRecipeProps,
  popoverCloseRecipe,
  type PopoverCloseRecipeProps,
  popoverDescriptionRecipe,
  type PopoverDescriptionRecipeProps,
  popoverPopupRecipe,
  type PopoverPopupRecipeProps,
  popoverPositionerRecipe,
  type PopoverPositionerRecipeProps,
  popoverTitleRecipe,
  type PopoverTitleRecipeProps,
  popoverTriggerRecipe,
  type PopoverTriggerRecipeProps,
  popoverViewportRecipe,
  type PopoverViewportRecipeProps,
} from "./popover.styles";

/** Every Base UI `Popover.Root` prop, generic over the trigger payload type. */
export type PopoverRootProps<Payload = unknown> = Parameters<typeof BasePopover.Root<Payload>>[0];

/** Every Base UI `Popover.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type PopoverPortalProps = ComponentProps<typeof BasePopover.Portal>;

/** Every Base UI `Popover.Trigger` prop, with `className` narrowed to `string`. */
export interface PopoverTriggerProps<Payload = unknown>
  extends
    Omit<Parameters<typeof BasePopover.Trigger<Payload>>[0], "className">,
    PopoverTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Popover.Backdrop` prop, with `className` narrowed to `string`. */
export interface PopoverBackdropProps
  extends
    Omit<ComponentProps<typeof BasePopover.Backdrop>, "className">,
    PopoverBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Popover.Positioner` prop, with `className` narrowed to `string`. */
export interface PopoverPositionerProps
  extends
    Omit<ComponentProps<typeof BasePopover.Positioner>, "className">,
    PopoverPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Popover.Popup` prop, with `className` narrowed to `string`. */
export interface PopoverPopupProps
  extends Omit<ComponentProps<typeof BasePopover.Popup>, "className">, PopoverPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Popover.Arrow` prop, with `className` narrowed to `string`. */
export interface PopoverArrowProps
  extends Omit<ComponentProps<typeof BasePopover.Arrow>, "className">, PopoverArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Popover.Viewport` prop, with `className` narrowed to `string`. */
export interface PopoverViewportProps
  extends
    Omit<ComponentProps<typeof BasePopover.Viewport>, "className">,
    PopoverViewportRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Popover.Title` prop, with `className` narrowed to `string`. */
export interface PopoverTitleProps
  extends Omit<ComponentProps<typeof BasePopover.Title>, "className">, PopoverTitleRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Popover.Description` prop, with `className` narrowed to `string`. */
export interface PopoverDescriptionProps
  extends
    Omit<ComponentProps<typeof BasePopover.Description>, "className">,
    PopoverDescriptionRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Popover.Close` prop, with `className` narrowed to `string`. */
export interface PopoverCloseProps
  extends Omit<ComponentProps<typeof BasePopover.Close>, "className">, PopoverCloseRecipeProps {
  readonly className?: string;
}

function PopoverTrigger<Payload>({
  className,
  ...rest
}: PopoverTriggerProps<Payload>): ReactElement {
  return <BasePopover.Trigger className={cn(popoverTriggerRecipe(), className)} {...rest} />;
}

function PopoverBackdrop({ className, ...rest }: PopoverBackdropProps): ReactElement {
  return <BasePopover.Backdrop className={cn(popoverBackdropRecipe(), className)} {...rest} />;
}

function PopoverPositioner({ className, ...rest }: PopoverPositionerProps): ReactElement {
  return <BasePopover.Positioner className={cn(popoverPositionerRecipe(), className)} {...rest} />;
}

function PopoverPopup({ className, ...rest }: PopoverPopupProps): ReactElement {
  return <BasePopover.Popup className={cn(popoverPopupRecipe(), className)} {...rest} />;
}

function PopoverArrow({ className, ...rest }: PopoverArrowProps): ReactElement {
  return <BasePopover.Arrow className={cn(popoverArrowRecipe(), className)} {...rest} />;
}

function PopoverViewport({ className, ...rest }: PopoverViewportProps): ReactElement {
  return <BasePopover.Viewport className={cn(popoverViewportRecipe(), className)} {...rest} />;
}

function PopoverTitle({ className, ...rest }: PopoverTitleProps): ReactElement {
  return <BasePopover.Title className={cn(popoverTitleRecipe(), className)} {...rest} />;
}

function PopoverDescription({ className, ...rest }: PopoverDescriptionProps): ReactElement {
  return (
    <BasePopover.Description className={cn(popoverDescriptionRecipe(), className)} {...rest} />
  );
}

function PopoverClose({ className, ...rest }: PopoverCloseProps): ReactElement {
  return <BasePopover.Close className={cn(popoverCloseRecipe(), className)} {...rest} />;
}

/**
 * An anchored popup for interactive content. Compose Root and Trigger with Portal > Positioner
 * > Popup. Title and Description label the content; Close dismisses it.
 */
export const Popover = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BasePopover.Root,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: PopoverTrigger,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BasePopover.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: PopoverBackdrop,
  /**
   * Positions the popup relative to its anchor; render inside Portal.
   */
  Positioner: PopoverPositioner,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: PopoverPopup,
  /**
   * Draws the popup's pointer toward its anchor.
   */
  Arrow: PopoverArrow,
  /**
   * Contains the visible popup region and its layout.
   */
  Viewport: PopoverViewport,
  /**
   * Provides the popup's accessible title.
   */
  Title: PopoverTitle,
  /**
   * Provides supporting text associated with the control or popup.
   */
  Description: PopoverDescription,
  /**
   * Closes the associated popup when activated.
   */
  Close: PopoverClose,
  /**
   * Handle class for sharing popup state with detached triggers.
   */
  Handle: BasePopover.Handle,
  /**
   * Creates a handle that connects a popup to triggers outside its Root.
   */
  createHandle: BasePopover.createHandle,
};
