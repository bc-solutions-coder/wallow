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

/** Every Base UI `Menu.Root` prop, generic over the trigger payload type. */
export type MenuRootProps<Payload = unknown> = Parameters<typeof BaseMenu.Root<Payload>>[0];

/** Every Base UI `Menu.SubmenuRoot` prop. Re-exported unwrapped, so no recipe props. */
export type MenuSubmenuRootProps = ComponentProps<typeof BaseMenu.SubmenuRoot>;

/** Every Base UI `Menu.Portal` prop. Re-exported unwrapped, so no recipe props. */
export type MenuPortalProps = ComponentProps<typeof BaseMenu.Portal>;

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
 * A menu of actions and optional selection items. Compose Root and Trigger with Portal >
 * Positioner > Popup; use Group and GroupLabel for sections, and SubmenuRoot with
 * SubmenuTrigger for nested menus.
 */
export const Menu = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BaseMenu.Root,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: MenuTrigger,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BaseMenu.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: MenuBackdrop,
  /**
   * Positions the popup relative to its anchor; render inside Portal.
   */
  Positioner: MenuPositioner,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: MenuPopup,
  /**
   * Draws the popup's pointer toward its anchor.
   */
  Arrow: MenuArrow,
  /**
   * Contains the visible popup region and its layout.
   */
  Viewport: MenuViewport,
  /**
   * Groups related items or controls.
   */
  Group: MenuGroup,
  /**
   * Labels a group of items.
   */
  GroupLabel: MenuGroupLabel,
  /**
   * Groups one item and its associated content.
   */
  Item: MenuItem,
  /**
   * A menu item that navigates through a native link.
   */
  LinkItem: MenuLinkItem,
  /**
   * A menu item with independent checked state.
   */
  CheckboxItem: MenuCheckboxItem,
  /**
   * Displays content while its checkbox menu item is checked.
   */
  CheckboxItemIndicator: MenuCheckboxItemIndicator,
  /**
   * Shares one selected value across radio menu items.
   */
  RadioGroup: MenuRadioGroup,
  /**
   * A menu item representing one value in its radio group.
   */
  RadioItem: MenuRadioItem,
  /**
   * Displays content while its radio menu item is selected.
   */
  RadioItemIndicator: MenuRadioItemIndicator,
  /**
   * Separates adjacent groups or content.
   */
  Separator: MenuSeparator,
  /**
   * Owns the state of a nested menu.
   */
  SubmenuRoot: BaseMenu.SubmenuRoot,
  /**
   * Opens a nested menu from its parent menu.
   */
  SubmenuTrigger: MenuSubmenuTrigger,
  /**
   * Handle class for sharing popup state with detached triggers.
   */
  Handle: BaseMenu.Handle,
  /**
   * Creates a handle that connects a popup to triggers outside its Root.
   */
  createHandle: BaseMenu.createHandle,
};
