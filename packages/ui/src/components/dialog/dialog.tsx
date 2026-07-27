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

/**
 * Four of Base UI's eleven namespace members are re-exported UNWRAPPED, because
 * none of them can carry a recipe:
 *
 *   - `Root` renders no HTML element at all (it is the state container), and it
 *     is generic over the trigger payload type — wrapping it would either drop
 *     the generic or add an element the DOM does not want.
 *   - `Portal` renders only the structural `<div data-base-ui-portal>` Base UI
 *     appends to `<body>`. It accepts a `className` (unlike `Select.Portal`),
 *     but it has no visual role: a recipe here would put a styled box between
 *     the backdrop/popup and the document. The caller's `className` still
 *     reaches the element, because the part is re-exported unchanged.
 *   - `Handle` is a class and `createHandle` a factory — the imperative
 *     open/close API for detached triggers. Neither renders anything.
 *
 * The catalog-wide rule this establishes for every later overlay: a part gets a
 * wrapper plus a recipe only if it renders a VISIBLE element; parts that render
 * no element, a structural container, or no DOM at all are re-exported as-is,
 * so the namespace keys still mirror Base UI 1:1.
 */

/** Every Base UI `Dialog.Root` prop, generic over the trigger payload type. */
export type DialogRootProps<Payload = unknown> = Parameters<typeof BaseDialog.Root<Payload>>[0];

/** Every Base UI `Dialog.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type DialogPortalProps = ComponentProps<typeof BaseDialog.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

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
 * The catalog's dialog, as ONE namespace object whose keys mirror Base UI's
 * eleven namespace members 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable dialog is Root > Trigger plus a portalled Backdrop and
 * Popup(Title, Description, Close); `Viewport` is the opt-in scroll container
 * for dialogs taller than the window, and `Handle`/`createHandle` are the opt-in
 * imperative API for triggers that live outside the Root.
 */
export const Dialog = {
  Root: BaseDialog.Root,
  Trigger: DialogTrigger,
  Portal: BaseDialog.Portal,
  Backdrop: DialogBackdrop,
  Viewport: DialogViewport,
  Popup: DialogPopup,
  Title: DialogTitle,
  Description: DialogDescription,
  Close: DialogClose,
  Handle: BaseDialog.Handle,
  createHandle: BaseDialog.createHandle,
};
