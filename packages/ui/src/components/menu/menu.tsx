import { Menu as BaseMenu } from "@base-ui/react/menu";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  menuArrowRecipe,
  type MenuArrowRecipeProps,
  menuBackdropRecipe,
  type MenuBackdropRecipeProps,
  menuCheckboxItemIndicatorRecipe,
  type MenuCheckboxItemIndicatorRecipeProps,
  menuCheckboxItemRecipe,
  type MenuCheckboxItemRecipeProps,
  menuGroupLabelRecipe,
  type MenuGroupLabelRecipeProps,
  menuGroupRecipe,
  type MenuGroupRecipeProps,
  menuItemRecipe,
  type MenuItemRecipeProps,
  menuLinkItemRecipe,
  type MenuLinkItemRecipeProps,
  menuPopupRecipe,
  type MenuPopupRecipeProps,
  menuPositionerRecipe,
  type MenuPositionerRecipeProps,
  menuRadioGroupRecipe,
  type MenuRadioGroupRecipeProps,
  menuRadioItemIndicatorRecipe,
  type MenuRadioItemIndicatorRecipeProps,
  menuRadioItemRecipe,
  type MenuRadioItemRecipeProps,
  menuSeparatorRecipe,
  type MenuSeparatorRecipeProps,
  menuSubmenuTriggerRecipe,
  type MenuSubmenuTriggerRecipeProps,
  menuTriggerRecipe,
  type MenuTriggerRecipeProps,
  menuViewportRecipe,
  type MenuViewportRecipeProps,
} from "./menu.styles";

/**
 * Five of Base UI's twenty-two namespace members are re-exported UNWRAPPED,
 * because none of them can carry a recipe:
 *
 *   - `Root` renders no HTML element at all (it is the state container), and it
 *     is generic over the trigger payload type — wrapping it would either drop
 *     the generic or add an element the DOM does not want.
 *   - `SubmenuRoot` is the same thing one level down: it groups a nested menu
 *     and renders no element of its own.
 *   - `Portal` renders only the structural `<div data-base-ui-portal>` Base UI
 *     appends to `<body>`. It accepts a `className`, but it has no visual role,
 *     and the caller's `className` still reaches it because the part is
 *     re-exported unchanged.
 *   - `Handle` is a class and `createHandle` a factory — the imperative
 *     open/close API for detached triggers. Neither renders anything.
 *
 * This is the same rule Dialog established for the catalog: a part gets a
 * wrapper plus a recipe only if it renders a VISIBLE element, so the namespace
 * keys still mirror Base UI 1:1.
 */

/** Every Base UI `Menu.Root` prop, generic over the trigger payload type. */
export type MenuRootProps<Payload = unknown> = Parameters<typeof BaseMenu.Root<Payload>>[0];

/** Every Base UI `Menu.SubmenuRoot` prop. Re-exported unwrapped, so no recipe props. */
export type MenuSubmenuRootProps = ComponentProps<typeof BaseMenu.SubmenuRoot>;

/** Every Base UI `Menu.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type MenuPortalProps = ComponentProps<typeof BaseMenu.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

/** Every Base UI `Menu.Trigger` prop, with `className` narrowed to `string`. */
export interface MenuTriggerProps<Payload = unknown>
  extends
    Omit<Parameters<typeof BaseMenu.Trigger<Payload>>[0], "className">,
    MenuTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.Backdrop` prop, with `className` narrowed to `string`. */
export interface MenuBackdropProps
  extends Omit<ComponentProps<typeof BaseMenu.Backdrop>, "className">, MenuBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.Positioner` prop, with `className` narrowed to `string`. */
export interface MenuPositionerProps
  extends Omit<ComponentProps<typeof BaseMenu.Positioner>, "className">, MenuPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.Popup` prop, with `className` narrowed to `string`. */
export interface MenuPopupProps
  extends Omit<ComponentProps<typeof BaseMenu.Popup>, "className">, MenuPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.Arrow` prop, with `className` narrowed to `string`. */
export interface MenuArrowProps
  extends Omit<ComponentProps<typeof BaseMenu.Arrow>, "className">, MenuArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.Viewport` prop, with `className` narrowed to `string`. */
export interface MenuViewportProps
  extends Omit<ComponentProps<typeof BaseMenu.Viewport>, "className">, MenuViewportRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.Group` prop, with `className` narrowed to `string`. */
export interface MenuGroupProps
  extends Omit<ComponentProps<typeof BaseMenu.Group>, "className">, MenuGroupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.GroupLabel` prop, with `className` narrowed to `string`. */
export interface MenuGroupLabelProps
  extends Omit<ComponentProps<typeof BaseMenu.GroupLabel>, "className">, MenuGroupLabelRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.Item` prop, with `className` narrowed to `string`. */
export interface MenuItemProps
  extends Omit<ComponentProps<typeof BaseMenu.Item>, "className">, MenuItemRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.LinkItem` prop, with `className` narrowed to `string`. */
export interface MenuLinkItemProps
  extends Omit<ComponentProps<typeof BaseMenu.LinkItem>, "className">, MenuLinkItemRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.CheckboxItem` prop, with `className` narrowed to `string`. */
export interface MenuCheckboxItemProps
  extends
    Omit<ComponentProps<typeof BaseMenu.CheckboxItem>, "className">,
    MenuCheckboxItemRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.CheckboxItemIndicator` prop, with `className` narrowed to `string`. */
export interface MenuCheckboxItemIndicatorProps
  extends
    Omit<ComponentProps<typeof BaseMenu.CheckboxItemIndicator>, "className">,
    MenuCheckboxItemIndicatorRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.RadioGroup` prop, with `className` narrowed to `string`. */
export interface MenuRadioGroupProps
  extends Omit<ComponentProps<typeof BaseMenu.RadioGroup>, "className">, MenuRadioGroupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.RadioItem` prop, with `className` narrowed to `string`. */
export interface MenuRadioItemProps
  extends Omit<ComponentProps<typeof BaseMenu.RadioItem>, "className">, MenuRadioItemRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.RadioItemIndicator` prop, with `className` narrowed to `string`. */
export interface MenuRadioItemIndicatorProps
  extends
    Omit<ComponentProps<typeof BaseMenu.RadioItemIndicator>, "className">,
    MenuRadioItemIndicatorRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.Separator` prop, with `className` narrowed to `string`. */
export interface MenuSeparatorProps
  extends Omit<ComponentProps<typeof BaseMenu.Separator>, "className">, MenuSeparatorRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Menu.SubmenuTrigger` prop, with `className` narrowed to `string`. */
export interface MenuSubmenuTriggerProps
  extends
    Omit<ComponentProps<typeof BaseMenu.SubmenuTrigger>, "className">,
    MenuSubmenuTriggerRecipeProps {
  readonly className?: string;
}

function MenuTrigger<Payload>({ className, ...rest }: MenuTriggerProps<Payload>): ReactElement {
  return <BaseMenu.Trigger className={cn(menuTriggerRecipe(), className)} {...rest} />;
}

function MenuBackdrop({ className, ...rest }: MenuBackdropProps): ReactElement {
  return <BaseMenu.Backdrop className={cn(menuBackdropRecipe(), className)} {...rest} />;
}

function MenuPositioner({ className, ...rest }: MenuPositionerProps): ReactElement {
  return <BaseMenu.Positioner className={cn(menuPositionerRecipe(), className)} {...rest} />;
}

function MenuPopup({ className, ...rest }: MenuPopupProps): ReactElement {
  return <BaseMenu.Popup className={cn(menuPopupRecipe(), className)} {...rest} />;
}

function MenuArrow({ className, ...rest }: MenuArrowProps): ReactElement {
  return <BaseMenu.Arrow className={cn(menuArrowRecipe(), className)} {...rest} />;
}

function MenuViewport({ className, ...rest }: MenuViewportProps): ReactElement {
  return <BaseMenu.Viewport className={cn(menuViewportRecipe(), className)} {...rest} />;
}

function MenuGroup({ className, ...rest }: MenuGroupProps): ReactElement {
  return <BaseMenu.Group className={cn(menuGroupRecipe(), className)} {...rest} />;
}

function MenuGroupLabel({ className, ...rest }: MenuGroupLabelProps): ReactElement {
  return <BaseMenu.GroupLabel className={cn(menuGroupLabelRecipe(), className)} {...rest} />;
}

function MenuItem({ className, ...rest }: MenuItemProps): ReactElement {
  return <BaseMenu.Item className={cn(menuItemRecipe(), className)} {...rest} />;
}

function MenuLinkItem({ className, ...rest }: MenuLinkItemProps): ReactElement {
  return <BaseMenu.LinkItem className={cn(menuLinkItemRecipe(), className)} {...rest} />;
}

function MenuCheckboxItem({ className, ...rest }: MenuCheckboxItemProps): ReactElement {
  return <BaseMenu.CheckboxItem className={cn(menuCheckboxItemRecipe(), className)} {...rest} />;
}

function MenuCheckboxItemIndicator({
  className,
  ...rest
}: MenuCheckboxItemIndicatorProps): ReactElement {
  return (
    <BaseMenu.CheckboxItemIndicator
      className={cn(menuCheckboxItemIndicatorRecipe(), className)}
      {...rest}
    />
  );
}

function MenuRadioGroup({ className, ...rest }: MenuRadioGroupProps): ReactElement {
  return <BaseMenu.RadioGroup className={cn(menuRadioGroupRecipe(), className)} {...rest} />;
}

function MenuRadioItem({ className, ...rest }: MenuRadioItemProps): ReactElement {
  return <BaseMenu.RadioItem className={cn(menuRadioItemRecipe(), className)} {...rest} />;
}

function MenuRadioItemIndicator({ className, ...rest }: MenuRadioItemIndicatorProps): ReactElement {
  return (
    <BaseMenu.RadioItemIndicator
      className={cn(menuRadioItemIndicatorRecipe(), className)}
      {...rest}
    />
  );
}

function MenuSeparator({ className, ...rest }: MenuSeparatorProps): ReactElement {
  return <BaseMenu.Separator className={cn(menuSeparatorRecipe(), className)} {...rest} />;
}

function MenuSubmenuTrigger({ className, ...rest }: MenuSubmenuTriggerProps): ReactElement {
  return (
    <BaseMenu.SubmenuTrigger className={cn(menuSubmenuTriggerRecipe(), className)} {...rest} />
  );
}

/**
 * The catalog's menu, as ONE namespace object whose keys mirror Base UI's
 * twenty-two namespace members 1:1 — the catalog-wide convention for multi-part
 * components, so a caller who knows the Base UI docs already knows this API.
 *
 * A minimal usable menu is Root > Trigger plus a portalled Positioner > Popup of
 * Items. Everything else is opt-in: `Group`/`GroupLabel`/`Separator` for
 * sections, `CheckboxItem`/`RadioGroup` for state-carrying rows,
 * `SubmenuRoot`/`SubmenuTrigger` for nested menus, `Backdrop` for a full-window
 * outside-press catcher, `Arrow` for a pointer at the anchor, `Viewport` for
 * animated content changes, and `Handle`/`createHandle` for triggers that live
 * outside the Root.
 *
 * `Separator` is Base UI's SHARED separator, re-exported on the menu namespace
 * by Base UI itself. Context Menu and Menubar reuse these very wrappers rather
 * than re-wrapping the identical underlying parts.
 */
export const Menu = {
  Root: BaseMenu.Root,
  Trigger: MenuTrigger,
  Portal: BaseMenu.Portal,
  Backdrop: MenuBackdrop,
  Positioner: MenuPositioner,
  Popup: MenuPopup,
  Arrow: MenuArrow,
  Viewport: MenuViewport,
  Group: MenuGroup,
  GroupLabel: MenuGroupLabel,
  Item: MenuItem,
  LinkItem: MenuLinkItem,
  CheckboxItem: MenuCheckboxItem,
  CheckboxItemIndicator: MenuCheckboxItemIndicator,
  RadioGroup: MenuRadioGroup,
  RadioItem: MenuRadioItem,
  RadioItemIndicator: MenuRadioItemIndicator,
  Separator: MenuSeparator,
  SubmenuRoot: BaseMenu.SubmenuRoot,
  SubmenuTrigger: MenuSubmenuTrigger,
  Handle: BaseMenu.Handle,
  createHandle: BaseMenu.createHandle,
};
