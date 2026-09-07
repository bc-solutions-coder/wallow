import { AlertDialog as BaseAlertDialog } from "@base-ui/react/alert-dialog";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  alertDialogBackdropRecipe,
  type AlertDialogBackdropRecipeProps,
  alertDialogCloseRecipe,
  type AlertDialogCloseRecipeProps,
  alertDialogDescriptionRecipe,
  type AlertDialogDescriptionRecipeProps,
  alertDialogPopupRecipe,
  type AlertDialogPopupRecipeProps,
  alertDialogTitleRecipe,
  type AlertDialogTitleRecipeProps,
  alertDialogTriggerRecipe,
  type AlertDialogTriggerRecipeProps,
  alertDialogViewportRecipe,
  type AlertDialogViewportRecipeProps,
} from "./alert-dialog.styles";

/** Every Base UI `AlertDialog.Root` prop, generic over the trigger payload type. */
export type AlertDialogRootProps<Payload = unknown> = Parameters<
  typeof BaseAlertDialog.Root<Payload>
>[0];

/** Every Base UI `AlertDialog.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type AlertDialogPortalProps = ComponentProps<typeof BaseAlertDialog.Portal>;

/** Every Base UI `AlertDialog.Trigger` prop, with `className` narrowed to `string`. */
export interface AlertDialogTriggerProps<Payload = unknown>
  extends
    Omit<Parameters<typeof BaseAlertDialog.Trigger<Payload>>[0], "className">,
    AlertDialogTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `AlertDialog.Backdrop` prop, with `className` narrowed to `string`. */
export interface AlertDialogBackdropProps
  extends
    Omit<ComponentProps<typeof BaseAlertDialog.Backdrop>, "className">,
    AlertDialogBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `AlertDialog.Viewport` prop, with `className` narrowed to `string`. */
export interface AlertDialogViewportProps
  extends
    Omit<ComponentProps<typeof BaseAlertDialog.Viewport>, "className">,
    AlertDialogViewportRecipeProps {
  readonly className?: string;
}

/** Every Base UI `AlertDialog.Popup` prop, with `className` narrowed to `string`. */
export interface AlertDialogPopupProps
  extends
    Omit<ComponentProps<typeof BaseAlertDialog.Popup>, "className">,
    AlertDialogPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `AlertDialog.Title` prop, with `className` narrowed to `string`. */
export interface AlertDialogTitleProps
  extends
    Omit<ComponentProps<typeof BaseAlertDialog.Title>, "className">,
    AlertDialogTitleRecipeProps {
  readonly className?: string;
}

/** Every Base UI `AlertDialog.Description` prop, with `className` narrowed to `string`. */
export interface AlertDialogDescriptionProps
  extends
    Omit<ComponentProps<typeof BaseAlertDialog.Description>, "className">,
    AlertDialogDescriptionRecipeProps {
  readonly className?: string;
}

/**
 * Every Base UI `AlertDialog.Close` prop, with `className` narrowed to `string`
 * and the button's `variant` axis mixed in — an alert dialog's footer is made of
 * `Close` parts, so this is where `variant="destructive"` marks the confirm
 * apart from the cancel.
 */
export interface AlertDialogCloseProps
  extends
    Omit<ComponentProps<typeof BaseAlertDialog.Close>, "className">,
    AlertDialogCloseRecipeProps {
  readonly className?: string;
}

function AlertDialogTrigger<Payload>({
  className,
  ...rest
}: AlertDialogTriggerProps<Payload>): ReactElement {
  return (
    <BaseAlertDialog.Trigger className={cn(alertDialogTriggerRecipe(), className)} {...rest} />
  );
}

function AlertDialogBackdrop({ className, ...rest }: AlertDialogBackdropProps): ReactElement {
  return (
    <BaseAlertDialog.Backdrop className={cn(alertDialogBackdropRecipe(), className)} {...rest} />
  );
}

function AlertDialogViewport({ className, ...rest }: AlertDialogViewportProps): ReactElement {
  return (
    <BaseAlertDialog.Viewport className={cn(alertDialogViewportRecipe(), className)} {...rest} />
  );
}

function AlertDialogPopup({ className, ...rest }: AlertDialogPopupProps): ReactElement {
  return <BaseAlertDialog.Popup className={cn(alertDialogPopupRecipe(), className)} {...rest} />;
}

function AlertDialogTitle({ className, ...rest }: AlertDialogTitleProps): ReactElement {
  return <BaseAlertDialog.Title className={cn(alertDialogTitleRecipe(), className)} {...rest} />;
}

function AlertDialogDescription({ className, ...rest }: AlertDialogDescriptionProps): ReactElement {
  return (
    <BaseAlertDialog.Description
      className={cn(alertDialogDescriptionRecipe(), className)}
      {...rest}
    />
  );
}

function AlertDialogClose({ className, variant, ...rest }: AlertDialogCloseProps): ReactElement {
  return (
    <BaseAlertDialog.Close
      className={cn(alertDialogCloseRecipe({ variant }), className)}
      {...rest}
    />
  );
}

/**
 * A modal confirmation dialog that ignores outside presses. Compose Root and Trigger with
 * Portal containing Backdrop and Popup, then Title, Description, and Close actions. Escape can
 * dismiss it.
 */
export const AlertDialog = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BaseAlertDialog.Root,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: AlertDialogTrigger,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BaseAlertDialog.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: AlertDialogBackdrop,
  /**
   * Contains the visible popup region and its layout.
   */
  Viewport: AlertDialogViewport,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: AlertDialogPopup,
  /**
   * Provides the popup's accessible title.
   */
  Title: AlertDialogTitle,
  /**
   * Provides supporting text associated with the control or popup.
   */
  Description: AlertDialogDescription,
  /**
   * Closes the associated popup when activated.
   */
  Close: AlertDialogClose,
  /**
   * Handle class for sharing popup state with detached triggers.
   */
  Handle: BaseAlertDialog.Handle,
  /**
   * Creates a handle that connects a popup to triggers outside its Root.
   */
  createHandle: BaseAlertDialog.createHandle,
};
