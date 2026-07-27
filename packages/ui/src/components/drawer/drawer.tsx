import { Drawer as BaseDrawer } from "@base-ui/react/drawer";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  drawerBackdropRecipe,
  type DrawerBackdropRecipeProps,
  drawerCloseRecipe,
  type DrawerCloseRecipeProps,
  drawerContentRecipe,
  type DrawerContentRecipeProps,
  drawerDescriptionRecipe,
  type DrawerDescriptionRecipeProps,
  drawerIndentBackgroundRecipe,
  type DrawerIndentBackgroundRecipeProps,
  drawerIndentRecipe,
  type DrawerIndentRecipeProps,
  drawerPopupRecipe,
  type DrawerPopupRecipeProps,
  drawerSwipeAreaRecipe,
  type DrawerSwipeAreaRecipeProps,
  drawerTitleRecipe,
  type DrawerTitleRecipeProps,
  drawerTriggerRecipe,
  type DrawerTriggerRecipeProps,
  drawerViewportRecipe,
  type DrawerViewportRecipeProps,
} from "./drawer.styles";

/**
 * Six of Base UI's seventeen namespace members are re-exported UNWRAPPED,
 * because none of them can carry a recipe:
 *
 *   - `Root` renders no HTML element at all (it is the state container), and it
 *     is generic over the trigger payload type — wrapping it would either drop
 *     the generic or add an element the DOM does not want.
 *   - `Provider` renders no element either. It is the scope `Indent` and
 *     `IndentBackground` read to learn whether ANY drawer beneath it is open.
 *   - `VirtualKeyboardProvider` renders no element. It opts a drawer into
 *     keyboard-aware focus and scroll handling for software keyboards, and it
 *     MUST sit INSIDE a `Drawer.Root` — see the namespace comment below.
 *   - `Portal` renders only the structural `<div data-base-ui-portal>` Base UI
 *     appends to `<body>`. It accepts a `className`, but it has no visual role:
 *     a recipe here would put a styled box between the backdrop/popup and the
 *     document. The caller's `className` still reaches the element, because the
 *     part is re-exported unchanged.
 *   - `Handle` is a class and `createHandle` a factory — the imperative
 *     open/close API for detached triggers. Neither renders anything.
 */

/** Every Base UI `Drawer.Root` prop, generic over the trigger payload type. */
export type DrawerRootProps<Payload = unknown> = Parameters<typeof BaseDrawer.Root<Payload>>[0];

/** Every Base UI `Drawer.Provider` prop. Re-exported unwrapped, so no recipe props. */
export type DrawerProviderProps = ComponentProps<typeof BaseDrawer.Provider>;

/** Every Base UI `Drawer.VirtualKeyboardProvider` prop. Re-exported unwrapped. */
export type DrawerVirtualKeyboardProviderProps = ComponentProps<
  typeof BaseDrawer.VirtualKeyboardProvider
>;

/** Every Base UI `Drawer.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type DrawerPortalProps = ComponentProps<typeof BaseDrawer.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

/** Every Base UI `Drawer.Trigger` prop, with `className` narrowed to `string`. */
export interface DrawerTriggerProps<Payload = unknown>
  extends
    Omit<Parameters<typeof BaseDrawer.Trigger<Payload>>[0], "className">,
    DrawerTriggerRecipeProps {
  readonly className?: string;
}

/**
 * Every Base UI `Drawer.SwipeArea` prop plus the recipe's `side`.
 *
 * Note the two different meanings of "direction" on this part: `side` is the
 * screen edge the strip sits on, while Base UI's own `swipeDirection` prop (and
 * the `data-swipe-direction` it stamps) name the direction the user swipes to
 * OPEN, which is the opposite one.
 */
export interface DrawerSwipeAreaProps
  extends
    Omit<ComponentProps<typeof BaseDrawer.SwipeArea>, "className">,
    DrawerSwipeAreaRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.Backdrop` prop, with `className` narrowed to `string`. */
export interface DrawerBackdropProps
  extends Omit<ComponentProps<typeof BaseDrawer.Backdrop>, "className">, DrawerBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.Viewport` prop plus the recipe's `side`. */
export interface DrawerViewportProps
  extends Omit<ComponentProps<typeof BaseDrawer.Viewport>, "className">, DrawerViewportRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.Popup` prop plus the recipe's `side`. */
export interface DrawerPopupProps
  extends Omit<ComponentProps<typeof BaseDrawer.Popup>, "className">, DrawerPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.Content` prop, with `className` narrowed to `string`. */
export interface DrawerContentProps
  extends Omit<ComponentProps<typeof BaseDrawer.Content>, "className">, DrawerContentRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.Title` prop, with `className` narrowed to `string`. */
export interface DrawerTitleProps
  extends Omit<ComponentProps<typeof BaseDrawer.Title>, "className">, DrawerTitleRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.Description` prop, with `className` narrowed to `string`. */
export interface DrawerDescriptionProps
  extends
    Omit<ComponentProps<typeof BaseDrawer.Description>, "className">,
    DrawerDescriptionRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.Close` prop, with `className` narrowed to `string`. */
export interface DrawerCloseProps
  extends Omit<ComponentProps<typeof BaseDrawer.Close>, "className">, DrawerCloseRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.Indent` prop, with `className` narrowed to `string`. */
export interface DrawerIndentProps
  extends Omit<ComponentProps<typeof BaseDrawer.Indent>, "className">, DrawerIndentRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Drawer.IndentBackground` prop, with `className` narrowed to `string`. */
export interface DrawerIndentBackgroundProps
  extends
    Omit<ComponentProps<typeof BaseDrawer.IndentBackground>, "className">,
    DrawerIndentBackgroundRecipeProps {
  readonly className?: string;
}

function DrawerTrigger<Payload>({ className, ...rest }: DrawerTriggerProps<Payload>): ReactElement {
  return <BaseDrawer.Trigger className={cn(drawerTriggerRecipe(), className)} {...rest} />;
}

function DrawerSwipeArea({ className, side, ...rest }: DrawerSwipeAreaProps): ReactElement {
  return (
    <BaseDrawer.SwipeArea className={cn(drawerSwipeAreaRecipe({ side }), className)} {...rest} />
  );
}

function DrawerBackdrop({ className, ...rest }: DrawerBackdropProps): ReactElement {
  return <BaseDrawer.Backdrop className={cn(drawerBackdropRecipe(), className)} {...rest} />;
}

function DrawerViewport({ className, side, ...rest }: DrawerViewportProps): ReactElement {
  return (
    <BaseDrawer.Viewport className={cn(drawerViewportRecipe({ side }), className)} {...rest} />
  );
}

function DrawerPopup({ className, side, ...rest }: DrawerPopupProps): ReactElement {
  return <BaseDrawer.Popup className={cn(drawerPopupRecipe({ side }), className)} {...rest} />;
}

function DrawerContent({ className, ...rest }: DrawerContentProps): ReactElement {
  return <BaseDrawer.Content className={cn(drawerContentRecipe(), className)} {...rest} />;
}

function DrawerTitle({ className, ...rest }: DrawerTitleProps): ReactElement {
  return <BaseDrawer.Title className={cn(drawerTitleRecipe(), className)} {...rest} />;
}

function DrawerDescription({ className, ...rest }: DrawerDescriptionProps): ReactElement {
  return <BaseDrawer.Description className={cn(drawerDescriptionRecipe(), className)} {...rest} />;
}

function DrawerClose({ className, ...rest }: DrawerCloseProps): ReactElement {
  return <BaseDrawer.Close className={cn(drawerCloseRecipe(), className)} {...rest} />;
}

function DrawerIndent({ className, ...rest }: DrawerIndentProps): ReactElement {
  return <BaseDrawer.Indent className={cn(drawerIndentRecipe(), className)} {...rest} />;
}

function DrawerIndentBackground({ className, ...rest }: DrawerIndentBackgroundProps): ReactElement {
  return (
    <BaseDrawer.IndentBackground
      className={cn(drawerIndentBackgroundRecipe(), className)}
      {...rest}
    />
  );
}

/**
 * The catalog's drawer, as ONE namespace object whose keys mirror Base UI's
 * seventeen namespace members 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable drawer is Root > Trigger plus a portalled Backdrop and
 * Viewport(Popup(Content(Title, Description, Close))). The `Viewport` is NOT
 * optional the way `Dialog.Viewport` is: Base UI warns and turns off swipe
 * handling and touch scroll locking without it.
 *
 * The opt-in parts:
 *   - `SwipeArea` adds swipe-to-OPEN from the screen edge;
 *   - `Provider` + `IndentBackground` + `Indent` add the iOS-style effect where
 *     the app UI behind the drawer scales back while it is open;
 *   - `VirtualKeyboardProvider` adds software-keyboard-aware focus and scroll
 *     handling for a sheet containing form fields. It reads the drawer's root
 *     store, so it MUST be rendered INSIDE `Drawer.Root` (measured: placed
 *     outside, it throws "Cannot destructure property 'store' of
 *     useDialogRootContext(...) as it is undefined"). Base UI's published
 *     anatomy does not show it, so this is the placement to copy;
 *   - `Handle`/`createHandle` are the imperative API for detached triggers.
 */
export const Drawer = {
  Root: BaseDrawer.Root,
  Provider: BaseDrawer.Provider,
  VirtualKeyboardProvider: BaseDrawer.VirtualKeyboardProvider,
  Trigger: DrawerTrigger,
  SwipeArea: DrawerSwipeArea,
  Portal: BaseDrawer.Portal,
  Backdrop: DrawerBackdrop,
  Viewport: DrawerViewport,
  Popup: DrawerPopup,
  Content: DrawerContent,
  Title: DrawerTitle,
  Description: DrawerDescription,
  Close: DrawerClose,
  Indent: DrawerIndent,
  IndentBackground: DrawerIndentBackground,
  Handle: BaseDrawer.Handle,
  createHandle: BaseDrawer.createHandle,
};
