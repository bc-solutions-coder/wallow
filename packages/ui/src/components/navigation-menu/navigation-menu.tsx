import { NavigationMenu as BaseNavigationMenu } from "@base-ui/react/navigation-menu";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  navigationMenuArrowRecipe,
  type NavigationMenuArrowRecipeProps,
  navigationMenuBackdropRecipe,
  type NavigationMenuBackdropRecipeProps,
  navigationMenuContentRecipe,
  type NavigationMenuContentRecipeProps,
  navigationMenuIconRecipe,
  type NavigationMenuIconRecipeProps,
  navigationMenuItemRecipe,
  type NavigationMenuItemRecipeProps,
  navigationMenuLinkRecipe,
  type NavigationMenuLinkRecipeProps,
  navigationMenuListRecipe,
  type NavigationMenuListRecipeProps,
  navigationMenuPopupRecipe,
  type NavigationMenuPopupRecipeProps,
  navigationMenuPositionerRecipe,
  type NavigationMenuPositionerRecipeProps,
  navigationMenuRootRecipe,
  type NavigationMenuRootRecipeProps,
  navigationMenuTriggerRecipe,
  type NavigationMenuTriggerRecipeProps,
  navigationMenuViewportRecipe,
  type NavigationMenuViewportRecipeProps,
} from "./navigation-menu.styles";

/**
 * Exactly ONE of Base UI's thirteen namespace members is re-exported UNWRAPPED:
 *
 *   - `Portal` renders only the structural `<div data-base-ui-portal>` Base UI
 *     appends to `<body>`. It accepts a `className`, but it has no visual role,
 *     and the caller's `className` still reaches it because the part is
 *     re-exported unchanged.
 *
 * The other twelve all render a visible element, so all twelve get a wrapper
 * plus a recipe — including `Root`, which unlike `Menu.Root` is a real `<nav>`
 * landmark rather than a state container, and `Item`, which is a real `<li>`.
 * This is the rule Dialog established for the catalog, and the namespace keys
 * still mirror Base UI 1:1.
 */

/**
 * Every Base UI `NavigationMenu.Root` prop, generic over the item-value type and
 * with `className` narrowed to `string`.
 */
export interface NavigationMenuRootProps<Value = unknown>
  extends
    Omit<Parameters<typeof BaseNavigationMenu.Root<Value>>[0], "className">,
    NavigationMenuRootRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type NavigationMenuPortalProps = ComponentProps<typeof BaseNavigationMenu.Portal>;

/** Every Base UI `NavigationMenu.List` prop, with `className` narrowed to `string`. */
export interface NavigationMenuListProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.List>, "className">,
    NavigationMenuListRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Item` prop, with `className` narrowed to `string`. */
export interface NavigationMenuItemProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Item>, "className">,
    NavigationMenuItemRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Trigger` prop, with `className` narrowed to `string`. */
export interface NavigationMenuTriggerProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Trigger>, "className">,
    NavigationMenuTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Icon` prop, with `className` narrowed to `string`. */
export interface NavigationMenuIconProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Icon>, "className">,
    NavigationMenuIconRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Content` prop, with `className` narrowed to `string`. */
export interface NavigationMenuContentProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Content>, "className">,
    NavigationMenuContentRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Link` prop, with `className` narrowed to `string`. */
export interface NavigationMenuLinkProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Link>, "className">,
    NavigationMenuLinkRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Backdrop` prop, with `className` narrowed to `string`. */
export interface NavigationMenuBackdropProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Backdrop>, "className">,
    NavigationMenuBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Positioner` prop, with `className` narrowed to `string`. */
export interface NavigationMenuPositionerProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Positioner>, "className">,
    NavigationMenuPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Popup` prop, with `className` narrowed to `string`. */
export interface NavigationMenuPopupProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Popup>, "className">,
    NavigationMenuPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Arrow` prop, with `className` narrowed to `string`. */
export interface NavigationMenuArrowProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Arrow>, "className">,
    NavigationMenuArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `NavigationMenu.Viewport` prop, with `className` narrowed to `string`. */
export interface NavigationMenuViewportProps
  extends
    Omit<ComponentProps<typeof BaseNavigationMenu.Viewport>, "className">,
    NavigationMenuViewportRecipeProps {
  readonly className?: string;
}

function NavigationMenuRoot<Value>({
  className,
  ...rest
}: NavigationMenuRootProps<Value>): ReactElement {
  return (
    <BaseNavigationMenu.Root className={cn(navigationMenuRootRecipe(), className)} {...rest} />
  );
}

function NavigationMenuList({ className, ...rest }: NavigationMenuListProps): ReactElement {
  return (
    <BaseNavigationMenu.List className={cn(navigationMenuListRecipe(), className)} {...rest} />
  );
}

function NavigationMenuItem({ className, ...rest }: NavigationMenuItemProps): ReactElement {
  return (
    <BaseNavigationMenu.Item className={cn(navigationMenuItemRecipe(), className)} {...rest} />
  );
}

function NavigationMenuTrigger({
  className,
  surface,
  ...rest
}: NavigationMenuTriggerProps): ReactElement {
  // `surface` is destructured, never spread — same reason as the link below: it
  // is a recipe axis, not an attribute, and a forgotten one lands in the markup
  // as `surface="sidebar"`.
  return (
    <BaseNavigationMenu.Trigger
      className={cn(navigationMenuTriggerRecipe({ surface }), className)}
      {...rest}
    />
  );
}

function NavigationMenuIcon({ className, ...rest }: NavigationMenuIconProps): ReactElement {
  return (
    <BaseNavigationMenu.Icon className={cn(navigationMenuIconRecipe(), className)} {...rest} />
  );
}

function NavigationMenuContent({ className, ...rest }: NavigationMenuContentProps): ReactElement {
  return (
    <BaseNavigationMenu.Content
      className={cn(navigationMenuContentRecipe(), className)}
      {...rest}
    />
  );
}

function NavigationMenuLink({
  surface,
  className,
  ...rest
}: NavigationMenuLinkProps): ReactElement {
  // `surface` is destructured, never spread: it is a recipe axis, not an
  // attribute, and a forgotten one lands in the markup as `surface="sidebar"`.
  return (
    <BaseNavigationMenu.Link
      className={cn(navigationMenuLinkRecipe({ surface }), className)}
      {...rest}
    />
  );
}

function NavigationMenuBackdrop({ className, ...rest }: NavigationMenuBackdropProps): ReactElement {
  return (
    <BaseNavigationMenu.Backdrop
      className={cn(navigationMenuBackdropRecipe(), className)}
      {...rest}
    />
  );
}

function NavigationMenuPositioner({
  className,
  ...rest
}: NavigationMenuPositionerProps): ReactElement {
  return (
    <BaseNavigationMenu.Positioner
      className={cn(navigationMenuPositionerRecipe(), className)}
      {...rest}
    />
  );
}

function NavigationMenuPopup({ className, ...rest }: NavigationMenuPopupProps): ReactElement {
  return (
    <BaseNavigationMenu.Popup className={cn(navigationMenuPopupRecipe(), className)} {...rest} />
  );
}

function NavigationMenuArrow({ className, ...rest }: NavigationMenuArrowProps): ReactElement {
  return (
    <BaseNavigationMenu.Arrow className={cn(navigationMenuArrowRecipe(), className)} {...rest} />
  );
}

function NavigationMenuViewport({ className, ...rest }: NavigationMenuViewportProps): ReactElement {
  return (
    <BaseNavigationMenu.Viewport
      className={cn(navigationMenuViewportRecipe(), className)}
      {...rest}
    />
  );
}

/**
 * Site navigation with optional popup content. Root contains List and Item parts; links
 * navigate directly and Trigger opens Content through the shared popup structure.
 */
export const NavigationMenu = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: NavigationMenuRoot,
  /**
   * Contains selectable items.
   */
  List: NavigationMenuList,
  /**
   * Groups one item and its associated content.
   */
  Item: NavigationMenuItem,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: NavigationMenuTrigger,
  /**
   * Visual indicator beside the input or selected value.
   */
  Icon: NavigationMenuIcon,
  /**
   * Contains the component's content.
   */
  Content: NavigationMenuContent,
  /**
   * A navigation link within the composite widget.
   */
  Link: NavigationMenuLink,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BaseNavigationMenu.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: NavigationMenuBackdrop,
  /**
   * Positions the popup relative to its anchor; render inside Portal.
   */
  Positioner: NavigationMenuPositioner,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: NavigationMenuPopup,
  /**
   * Draws the popup's pointer toward its anchor.
   */
  Arrow: NavigationMenuArrow,
  /**
   * Contains the visible popup region and its layout.
   */
  Viewport: NavigationMenuViewport,
};
