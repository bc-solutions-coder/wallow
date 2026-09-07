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
 * A swipeable sheet. Compose Root with Trigger and Portal > Backdrop plus Viewport > Popup >
 * Content. Viewport is required for swipe and touch-scroll handling; VirtualKeyboardProvider
 * belongs inside Root.
 */
export const Drawer = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BaseDrawer.Root,
  /**
   * Shares behavior settings with descendant component roots.
   */
  Provider: BaseDrawer.Provider,
  /**
   * Enables software-keyboard handling; render inside Drawer.Root.
   */
  VirtualKeyboardProvider: BaseDrawer.VirtualKeyboardProvider,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: DrawerTrigger,
  /**
   * Opens the drawer from an edge swipe; side places the edge strip.
   */
  SwipeArea: DrawerSwipeArea,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BaseDrawer.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: DrawerBackdrop,
  /**
   * Contains the visible popup region and its layout.
   */
  Viewport: DrawerViewport,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: DrawerPopup,
  /**
   * Contains the component's content.
   */
  Content: DrawerContent,
  /**
   * Provides the popup's accessible title.
   */
  Title: DrawerTitle,
  /**
   * Provides supporting text associated with the control or popup.
   */
  Description: DrawerDescription,
  /**
   * Closes the associated popup when activated.
   */
  Close: DrawerClose,
  /**
   * Scales the page behind open drawers inside a shared Drawer.Provider.
   */
  Indent: DrawerIndent,
  /**
   * Paints the background exposed by Drawer.Indent.
   */
  IndentBackground: DrawerIndentBackground,
  /**
   * Handle class for sharing popup state with detached triggers.
   */
  Handle: BaseDrawer.Handle,
  /**
   * Creates a handle that connects a popup to triggers outside its Root.
   */
  createHandle: BaseDrawer.createHandle,
};
