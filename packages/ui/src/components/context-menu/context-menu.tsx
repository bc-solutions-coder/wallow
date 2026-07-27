import { ContextMenu as BaseContextMenu } from "@base-ui/react/context-menu";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { Menu } from "../menu/menu";
import type {
  MenuArrowProps,
  MenuBackdropProps,
  MenuCheckboxItemIndicatorProps,
  MenuCheckboxItemProps,
  MenuGroupLabelProps,
  MenuGroupProps,
  MenuItemProps,
  MenuLinkItemProps,
  MenuPopupProps,
  MenuPortalProps,
  MenuPositionerProps,
  MenuRadioGroupProps,
  MenuRadioItemIndicatorProps,
  MenuRadioItemProps,
  MenuSeparatorProps,
  MenuSubmenuRootProps,
  MenuSubmenuTriggerProps,
} from "../menu/menu";
import {
  contextMenuTriggerRecipe,
  type ContextMenuTriggerRecipeProps,
} from "./context-menu.styles";

/**
 * A context menu is a menu that a right click or a long press opens, at the
 * pointer rather than at a button. Base UI models that as literally the same
 * component: `context-menu/index.parts.d.ts` re-exports `@base-ui/react/menu`'s
 * own runtime for SEVENTEEN of its nineteen members, and only `Root` and
 * `Trigger` are context-menu code. `Root` swaps the trigger machinery (it
 * provides a virtual anchor at the cursor point, drops `modal`, `openOnHover`,
 * `delay` and `closeDelay` from the prop type and re-types `onOpenChange`'s
 * event details onto its own reason union), and `Trigger` is
 * a plain `<div>` listening for `contextmenu`, touch long-press and the mouse-up
 * that cancels it.
 *
 * So this component re-exports the `Menu` catalog component's ALREADY-WRAPPED
 * parts for those seventeen members and wraps only `Trigger` itself.
 *
 * *** THIS IS THE OPPOSITE CALL TO THE ONE ALERT DIALOG MADE, ON PURPOSE. ***
 * Alert Dialog re-wraps Dialog's parts so that its recipe layer stays
 * independently themable, because an alert is different chrome from a dialog —
 * a fork restyling one usually does not mean the other. A context menu is not
 * different chrome from a menu: it is the SAME menu card, opened from somewhere
 * else. A fork that rounds its menu corners or changes its row height means
 * both, and two identical recipe sets would let the two drift apart with no
 * fork ever asking for it. Sharing the wrappers makes that impossible by
 * construction.
 *
 * The practical consequence for callers: `ContextMenu.Popup` IS `Menu.Popup`,
 * so anything the Menu docs say about a part is true of the same part here, and
 * a row styled for one is styled for the other.
 */

/*
 * Two of the nineteen namespace members are context-menu code, and only one of
 * those gets a wrapper — the Dialog exemplar's rule, unchanged: a member gets a
 * wrapper plus a recipe only if it renders a VISIBLE element. `Root` renders no
 * DOM at all (it is the state container and the cursor anchor), so it is
 * re-exported as-is and the namespace keys still mirror Base UI's 1:1.
 *
 * Note what Base UI does NOT publish on this subpath, unlike `menu`: there is no
 * `Viewport` and no `Handle`/`createHandle`. A context menu has one trigger area
 * and no detached imperative opener, so nineteen members is the whole surface.
 */

/**
 * Every Base UI `ContextMenu.Root` prop. Re-exported unwrapped, so no recipe
 * props. This is `Menu.Root`'s prop set minus `modal`, `openOnHover`, `delay`
 * and `closeDelay`, which a cursor-anchored menu has no meaning for — so
 * `defaultOpen`, `open`, `onOpenChange`, `actionsRef` and the rest are still here.
 */
export type ContextMenuRootProps = ComponentProps<typeof BaseContextMenu.Root>;

/*
 * `className` is deliberately narrowed back to `string` on the wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

/** Every Base UI `ContextMenu.Trigger` prop, with `className` narrowed to `string`. */
export interface ContextMenuTriggerProps
  extends
    Omit<ComponentProps<typeof BaseContextMenu.Trigger>, "className">,
    ContextMenuTriggerRecipeProps {
  readonly className?: string;
}

/*
 * The seventeen shared parts' prop types, aliased onto context-menu names. Base
 * UI publishes the very same aliases on this subpath (`ContextMenuPopupProps`
 * and friends in context-menu/index.d.ts), so a caller writing a wrapper around
 * `ContextMenu.Popup` never has to reach into the menu subpath to name its
 * props. They are type aliases, not new types: `ContextMenuPopupProps` and
 * `MenuPopupProps` are interchangeable, exactly as the components are.
 */

/** Every Base UI `ContextMenu.Backdrop` prop — `Menu.Backdrop`'s, unchanged. */
export type ContextMenuBackdropProps = MenuBackdropProps;

/** Every Base UI `ContextMenu.Portal` prop — `Menu.Portal`'s, unchanged. */
export type ContextMenuPortalProps = MenuPortalProps;

/** Every Base UI `ContextMenu.Positioner` prop — `Menu.Positioner`'s, unchanged. */
export type ContextMenuPositionerProps = MenuPositionerProps;

/** Every Base UI `ContextMenu.Popup` prop — `Menu.Popup`'s, unchanged. */
export type ContextMenuPopupProps = MenuPopupProps;

/** Every Base UI `ContextMenu.Arrow` prop — `Menu.Arrow`'s, unchanged. */
export type ContextMenuArrowProps = MenuArrowProps;

/** Every Base UI `ContextMenu.Group` prop — `Menu.Group`'s, unchanged. */
export type ContextMenuGroupProps = MenuGroupProps;

/** Every Base UI `ContextMenu.GroupLabel` prop — `Menu.GroupLabel`'s, unchanged. */
export type ContextMenuGroupLabelProps = MenuGroupLabelProps;

/** Every Base UI `ContextMenu.Item` prop — `Menu.Item`'s, unchanged. */
export type ContextMenuItemProps = MenuItemProps;

/** Every Base UI `ContextMenu.LinkItem` prop — `Menu.LinkItem`'s, unchanged. */
export type ContextMenuLinkItemProps = MenuLinkItemProps;

/** Every Base UI `ContextMenu.CheckboxItem` prop — `Menu.CheckboxItem`'s, unchanged. */
export type ContextMenuCheckboxItemProps = MenuCheckboxItemProps;

/** Every Base UI `ContextMenu.CheckboxItemIndicator` prop — `Menu.CheckboxItemIndicator`'s, unchanged. */
export type ContextMenuCheckboxItemIndicatorProps = MenuCheckboxItemIndicatorProps;

/** Every Base UI `ContextMenu.RadioGroup` prop — `Menu.RadioGroup`'s, unchanged. */
export type ContextMenuRadioGroupProps = MenuRadioGroupProps;

/** Every Base UI `ContextMenu.RadioItem` prop — `Menu.RadioItem`'s, unchanged. */
export type ContextMenuRadioItemProps = MenuRadioItemProps;

/** Every Base UI `ContextMenu.RadioItemIndicator` prop — `Menu.RadioItemIndicator`'s, unchanged. */
export type ContextMenuRadioItemIndicatorProps = MenuRadioItemIndicatorProps;

/** Every Base UI `ContextMenu.Separator` prop — `Menu.Separator`'s, unchanged. */
export type ContextMenuSeparatorProps = MenuSeparatorProps;

/** Every Base UI `ContextMenu.SubmenuRoot` prop — `Menu.SubmenuRoot`'s, unchanged. */
export type ContextMenuSubmenuRootProps = MenuSubmenuRootProps;

/** Every Base UI `ContextMenu.SubmenuTrigger` prop — `Menu.SubmenuTrigger`'s, unchanged. */
export type ContextMenuSubmenuTriggerProps = MenuSubmenuTriggerProps;

function ContextMenuTrigger({ className, ...rest }: ContextMenuTriggerProps): ReactElement {
  return (
    <BaseContextMenu.Trigger className={cn(contextMenuTriggerRecipe(), className)} {...rest} />
  );
}

/**
 * The catalog's context menu, as ONE namespace object whose keys mirror Base
 * UI's nineteen namespace members 1:1 — the catalog-wide convention for
 * multi-part components, so a caller who knows the Base UI docs already knows
 * this API.
 *
 * A minimal context menu is Root > Trigger (the right-clickable area) plus a
 * portalled Positioner > Popup of Items. Everything past the trigger is the
 * `Menu` component's own part, so the anatomy, the keyboard behaviour and the
 * styling are the menu's: `Group`/`GroupLabel`/`Separator` for sections,
 * `CheckboxItem`/`RadioGroup` for state-carrying rows, `SubmenuRoot`/
 * `SubmenuTrigger` for nested menus, `Backdrop` for a full-window outside-press
 * catcher and `Arrow` for a pointer at the anchor.
 *
 * The trigger is a plain `<div>` and carries no ARIA of its own — Base UI's
 * design, because a right-click area is not a button and has no accessible
 * "expanded" state to announce. Reach for `Menu` instead whenever the menu needs
 * a keyboard-reachable opener.
 */
export const ContextMenu = {
  Root: BaseContextMenu.Root,
  Trigger: ContextMenuTrigger,
  Backdrop: Menu.Backdrop,
  Portal: Menu.Portal,
  Positioner: Menu.Positioner,
  Popup: Menu.Popup,
  Arrow: Menu.Arrow,
  Group: Menu.Group,
  GroupLabel: Menu.GroupLabel,
  Item: Menu.Item,
  LinkItem: Menu.LinkItem,
  CheckboxItem: Menu.CheckboxItem,
  CheckboxItemIndicator: Menu.CheckboxItemIndicator,
  RadioGroup: Menu.RadioGroup,
  RadioItem: Menu.RadioItem,
  RadioItemIndicator: Menu.RadioItemIndicator,
  Separator: Menu.Separator,
  SubmenuRoot: Menu.SubmenuRoot,
  SubmenuTrigger: Menu.SubmenuTrigger,
};
