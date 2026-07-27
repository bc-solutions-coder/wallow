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

/**
 * `@base-ui/react/alert-dialog` is mostly `@base-ui/react/dialog` wearing a
 * different name: `alert-dialog/index.parts.d.ts` re-exports Dialog's own
 * runtime for Backdrop, Close, Description, Popup, Portal, Title and Viewport,
 * and `AlertDialogTrigger` is literally `DialogTrigger`. Only `Root` is its own
 * component, and all it does is call Dialog's `useRenderDialogRoot(props,
 * 'alert-dialog')`, which forces three invariants a plain dialog only defaults
 * to (measured in useRenderDialogRoot.mjs):
 *
 *   - `modal` is forced true and the prop is removed from the type;
 *   - `disablePointerDismissal` is forced true and the prop is removed, so
 *     PRESSING THE BACKDROP DOES NOT CLOSE AN ALERT DIALOG — the behaviour that
 *     makes this a separate component rather than a Dialog preset;
 *   - the popup's role becomes `alertdialog` instead of `dialog`.
 *
 * Escape still closes: `useDialogRoot`'s `escapeKey` is independent of
 * `disablePointerDismissal`.
 *
 * This component nevertheless wraps the Base UI parts itself rather than
 * re-exporting the Dialog component's wrappers. Sharing wrappers would share
 * RECIPES, and the recipe layer is exactly what a fork restyles — an alert has
 * to stay independently themable from a dialog even though the two run the same
 * code underneath.
 */

/*
 * Four of the eleven namespace members are re-exported UNWRAPPED, on the same
 * rule the Dialog exemplar set: a part gets a wrapper plus a recipe only if it
 * renders a VISIBLE element. `Root` renders no element and is generic over the
 * trigger payload, `Portal` renders only the structural container Base UI
 * appends to `<body>`, and `Handle`/`createHandle` are the imperative open/close
 * API for detached triggers and render no DOM at all. Keeping all four means
 * this namespace's keys still mirror Base UI's 1:1.
 */

/** Every Base UI `AlertDialog.Root` prop, generic over the trigger payload type. */
export type AlertDialogRootProps<Payload = unknown> = Parameters<
  typeof BaseAlertDialog.Root<Payload>
>[0];

/** Every Base UI `AlertDialog.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type AlertDialogPortalProps = ComponentProps<typeof BaseAlertDialog.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

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
 * The catalog's alert dialog, as ONE namespace object whose keys mirror Base
 * UI's eleven namespace members 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * Reach for this over `Dialog` whenever the user must answer the question before
 * carrying on: it cannot be dismissed by pressing outside, and it announces
 * itself as `role="alertdialog"`. A minimal alert is Root > Trigger plus a
 * portalled Backdrop and Popup(Title, Description, Close, Close) — one `Close`
 * for the cancel and one carrying the caller's `onClick` for the confirm.
 */
export const AlertDialog = {
  Root: BaseAlertDialog.Root,
  Trigger: AlertDialogTrigger,
  Portal: BaseAlertDialog.Portal,
  Backdrop: AlertDialogBackdrop,
  Viewport: AlertDialogViewport,
  Popup: AlertDialogPopup,
  Title: AlertDialogTitle,
  Description: AlertDialogDescription,
  Close: AlertDialogClose,
  Handle: BaseAlertDialog.Handle,
  createHandle: BaseAlertDialog.createHandle,
};
