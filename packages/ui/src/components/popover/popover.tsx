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

/**
 * Four of Base UI's thirteen namespace members are re-exported UNWRAPPED,
 * because none of them can carry a recipe — the same rule the Dialog exemplar
 * set for every overlay in this catalog:
 *
 *   - `Root` renders no HTML element at all (it is the state container), and it
 *     is generic over the trigger payload type — wrapping it would either drop
 *     the generic or add an element the DOM does not want.
 *   - `Portal` renders only the structural `<div data-base-ui-portal>` Base UI
 *     appends to `<body>`. It accepts a `className`, but it has no visual role,
 *     and the caller's `className` still reaches it because the part is
 *     re-exported unchanged.
 *   - `Handle` is a class and `createHandle` a factory — the imperative
 *     open/close API for triggers that live outside the `Root`. Neither renders
 *     anything.
 *
 * Everything that renders a visible element is wrapped, so `Object.keys` still
 * mirrors `@base-ui/react/popover`'s namespace 1:1.
 */

/** Every Base UI `Popover.Root` prop, generic over the trigger payload type. */
export type PopoverRootProps<Payload = unknown> = Parameters<typeof BasePopover.Root<Payload>>[0];

/** Every Base UI `Popover.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type PopoverPortalProps = ComponentProps<typeof BasePopover.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

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
 * The catalog's popover, as ONE namespace object whose keys mirror Base UI's
 * thirteen namespace members 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable popover is Root > Trigger plus a portalled
 * Positioner(Popup(Title, Description, Close)). `Arrow` is the opt-in pointer
 * aimed at the anchor, `Backdrop` the opt-in scrim, `Viewport` the opt-in
 * cross-fade container for a popup shared by several triggers, and
 * `Handle`/`createHandle` the opt-in imperative API for triggers outside the
 * Root.
 *
 * Unlike `Dialog`, a popover is NON-MODAL by default (`Root`'s `modal` prop
 * defaults to `false`): the page keeps scrolling, pointer events outside stay
 * live, focus is not trapped, and moving focus out of the popup dismisses it.
 */
export const Popover = {
  Root: BasePopover.Root,
  Trigger: PopoverTrigger,
  Portal: BasePopover.Portal,
  Backdrop: PopoverBackdrop,
  Positioner: PopoverPositioner,
  Popup: PopoverPopup,
  Arrow: PopoverArrow,
  Viewport: PopoverViewport,
  Title: PopoverTitle,
  Description: PopoverDescription,
  Close: PopoverClose,
  Handle: BasePopover.Handle,
  createHandle: BasePopover.createHandle,
};
