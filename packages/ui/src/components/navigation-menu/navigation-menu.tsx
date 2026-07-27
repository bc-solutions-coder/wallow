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

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
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

function NavigationMenuTrigger({ className, ...rest }: NavigationMenuTriggerProps): ReactElement {
  return (
    <BaseNavigationMenu.Trigger
      className={cn(navigationMenuTriggerRecipe(), className)}
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

function NavigationMenuLink({ className, ...rest }: NavigationMenuLinkProps): ReactElement {
  return (
    <BaseNavigationMenu.Link className={cn(navigationMenuLinkRecipe(), className)} {...rest} />
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
 * The catalog's navigation menu, as ONE namespace object whose keys mirror Base
 * UI's thirteen namespace members 1:1 — the catalog-wide convention for
 * multi-part components, so a caller who knows the Base UI docs already knows
 * this API.
 *
 * A minimal usable menu is Root > List > Item, where an Item holds either a bare
 * `Link` (a flat row that navigates) or a `Trigger` + `Content` pair (a row that
 * opens a panel), plus a portalled Positioner > Popup > Viewport for the panels
 * to appear in. Everything else is opt-in: `Icon` for the trigger's chevron,
 * `Backdrop` for a full-window outside-press catcher, and `Arrow` for a pointer
 * at the anchor.
 *
 * TWO THINGS DIFFER FROM `Menu` AND WILL SURPRISE ANYONE COMING FROM IT:
 *
 *   1. ONE POPUP SERVES EVERY ITEM. `Content` is authored inside its `Item` but
 *      Base UI MOVES it into the shared `Viewport` while that item is active, so
 *      the panel's padding belongs on `Content` and the card's paint on `Popup`.
 *   2. THE MENU OPENS ON HOVER after `Root`'s `delay` (50ms by default), not
 *      only on press. There is no prop to turn that off.
 *
 * `Root` is generic over the item-value type: `<NavigationMenu.Root<Section>>`
 * types `value`, `defaultValue` and `onValueChange` together, and inference from
 * `defaultValue` usually means the annotation can be left off.
 */
export const NavigationMenu = {
  Root: NavigationMenuRoot,
  List: NavigationMenuList,
  Item: NavigationMenuItem,
  Trigger: NavigationMenuTrigger,
  Icon: NavigationMenuIcon,
  Content: NavigationMenuContent,
  Link: NavigationMenuLink,
  Portal: BaseNavigationMenu.Portal,
  Backdrop: NavigationMenuBackdrop,
  Positioner: NavigationMenuPositioner,
  Popup: NavigationMenuPopup,
  Arrow: NavigationMenuArrow,
  Viewport: NavigationMenuViewport,
};
