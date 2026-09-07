import { Dialog as BaseDialog } from "@base-ui/react/dialog";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  dialogBackdropRecipe,
  type DialogBackdropRecipeProps,
  dialogCloseRecipe,
  type DialogCloseRecipeProps,
  dialogDescriptionRecipe,
  type DialogDescriptionRecipeProps,
  dialogPopupRecipe,
  type DialogPopupRecipeProps,
  dialogTitleRecipe,
  type DialogTitleRecipeProps,
  dialogTriggerRecipe,
  type DialogTriggerRecipeProps,
  dialogViewportRecipe,
  type DialogViewportRecipeProps,
} from "./dialog.styles";

/** Every Base UI `Dialog.Root` prop, generic over the trigger payload type. */
export type DialogRootProps<Payload = unknown> = Parameters<typeof BaseDialog.Root<Payload>>[0];

/** Every Base UI `Dialog.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type DialogPortalProps = ComponentProps<typeof BaseDialog.Portal>;

/** Every Base UI `Dialog.Trigger` prop, with `className` narrowed to `string`. */
export interface DialogTriggerProps<Payload = unknown>
  extends
    Omit<Parameters<typeof BaseDialog.Trigger<Payload>>[0], "className">,
    DialogTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Dialog.Backdrop` prop, with `className` narrowed to `string`. */
export interface DialogBackdropProps
  extends Omit<ComponentProps<typeof BaseDialog.Backdrop>, "className">, DialogBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Dialog.Viewport` prop, with `className` narrowed to `string`. */
export interface DialogViewportProps
  extends Omit<ComponentProps<typeof BaseDialog.Viewport>, "className">, DialogViewportRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Dialog.Popup` prop, with `className` narrowed to `string`. */
export interface DialogPopupProps
  extends Omit<ComponentProps<typeof BaseDialog.Popup>, "className">, DialogPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Dialog.Title` prop, with `className` narrowed to `string`. */
export interface DialogTitleProps
  extends Omit<ComponentProps<typeof BaseDialog.Title>, "className">, DialogTitleRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Dialog.Description` prop, with `className` narrowed to `string`. */
export interface DialogDescriptionProps
  extends
    Omit<ComponentProps<typeof BaseDialog.Description>, "className">,
    DialogDescriptionRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Dialog.Close` prop, with `className` narrowed to `string`. */
export interface DialogCloseProps
  extends Omit<ComponentProps<typeof BaseDialog.Close>, "className">, DialogCloseRecipeProps {
  readonly className?: string;
}

function DialogTrigger<Payload>({ className, ...rest }: DialogTriggerProps<Payload>): ReactElement {
  return <BaseDialog.Trigger className={cn(dialogTriggerRecipe(), className)} {...rest} />;
}

function DialogBackdrop({ className, ...rest }: DialogBackdropProps): ReactElement {
  return <BaseDialog.Backdrop className={cn(dialogBackdropRecipe(), className)} {...rest} />;
}

function DialogViewport({ className, ...rest }: DialogViewportProps): ReactElement {
  return <BaseDialog.Viewport className={cn(dialogViewportRecipe(), className)} {...rest} />;
}

function DialogPopup({ className, ...rest }: DialogPopupProps): ReactElement {
  return <BaseDialog.Popup className={cn(dialogPopupRecipe(), className)} {...rest} />;
}

function DialogTitle({ className, ...rest }: DialogTitleProps): ReactElement {
  return <BaseDialog.Title className={cn(dialogTitleRecipe(), className)} {...rest} />;
}

function DialogDescription({ className, ...rest }: DialogDescriptionProps): ReactElement {
  return <BaseDialog.Description className={cn(dialogDescriptionRecipe(), className)} {...rest} />;
}

function DialogClose({ className, ...rest }: DialogCloseProps): ReactElement {
  return <BaseDialog.Close className={cn(dialogCloseRecipe(), className)} {...rest} />;
}

/**
 * A modal dialog composed from Root, Trigger, and a portalled Backdrop and Popup. Include
 * Title, Description, and Close; Viewport adds an optional scroll container. createHandle
 * supports detached triggers.
 */
export const Dialog = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BaseDialog.Root,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: DialogTrigger,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BaseDialog.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: DialogBackdrop,
  /**
   * Contains the visible popup region and its layout.
   */
  Viewport: DialogViewport,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: DialogPopup,
  /**
   * Provides the popup's accessible title.
   */
  Title: DialogTitle,
  /**
   * Provides supporting text associated with the control or popup.
   */
  Description: DialogDescription,
  /**
   * Closes the associated popup when activated.
   */
  Close: DialogClose,
  /**
   * Handle class for sharing popup state with detached triggers.
   */
  Handle: BaseDialog.Handle,
  /**
   * Creates a handle that connects a popup to triggers outside its Root.
   */
  createHandle: BaseDialog.createHandle,
};
