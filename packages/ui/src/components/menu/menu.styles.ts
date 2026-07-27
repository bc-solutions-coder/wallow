import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the menu. The class lists are not invented here
 * — menu.test.tsx declares each part's exact utility set as a top-of-file
 * `*_CLASSES` constant and asserts it as an order-free set through the rendered
 * component, so that spec is the source of truth for everything below.
 *
 * Five of Base UI's twenty-two namespace members have no recipe, because they
 * render no visible element: `Root` and `SubmenuRoot` render no DOM at all,
 * `Portal` renders only the structural container Base UI appends to `<body>`,
 * and `Handle`/`createHandle` are the imperative API (see menu.tsx).
 *
 * No recipe takes a cva VARIANT. A menu has no visual variant axis in this
 * catalog: open/closed, highlighted, checked, disabled and the entering/exiting
 * transition phases are all STATES, and Base UI publishes states as `data-*`
 * attributes, so they belong in the base string as `data-[highlighted]:` /
 * `data-[starting-style]:` / `data-[ending-style]:` / `data-[popup-open]:` /
 * `data-[disabled]:` modifiers rather than as cva variants nobody would pass by
 * hand. The `VariantProps` types are still exported so each part's props keep
 * the catalog-wide shape and a later variant axis stays a non-breaking addition.
 */

/**
 * The button that opens the menu — Base UI's `Menu.Trigger`, a `<button>`.
 * Deliberately COLOURLESS for the same reason as `dialogTriggerRecipe`: a
 * trigger is routinely composed onto a real `Button` through Base UI's `render`
 * prop, and a `bg-*` here would be merged away by tailwind-merge and silently
 * beat the Button's own background.
 */
export const menuTriggerRecipe = cva(
  "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `MenuTriggerProps`. */
export type MenuTriggerRecipeProps = VariantProps<typeof menuTriggerRecipe>;

/**
 * The outside-press catcher behind an open menu — Base UI's `Menu.Backdrop`, a
 * `<div>`. Unlike a dialog's backdrop this is NOT a scrim: a menu does not dim
 * the page, so the recipe only has to cover the window. Measured: Base UI gives
 * this element no inline positioning at all (only `user-select`), so the
 * covering is entirely the recipe's job.
 */
export const menuBackdropRecipe = cva("fixed inset-0");

/** The backdrop recipe's variant props, mixed into `MenuBackdropProps`. */
export type MenuBackdropRecipeProps = VariantProps<typeof menuBackdropRecipe>;

/**
 * The anchored wrapper Base UI positions against the trigger — `Menu.Positioner`.
 * It owns the inline `position`/`left`/`top`/`transform` styles (measured), so
 * this recipe may only add stacking and focus concerns, never layout that would
 * fight the positioning engine. Same rule as `selectPositionerRecipe`, and the
 * OPPOSITE of `dialogPopupRecipe`, which owns its own centring.
 */
export const menuPositionerRecipe = cva("z-50 outline-none");

/** The positioner recipe's variant props, mixed into `MenuPositionerProps`. */
export type MenuPositionerRecipeProps = VariantProps<typeof menuPositionerRecipe>;

/**
 * The menu card itself — Base UI's `Menu.Popup`, a `<div role="menu">`. Carries
 * `relative` so `Menu.Arrow`, which Base UI positions absolutely with an inline
 * `left` but no `top`, has this box as its containing block.
 */
export const menuPopupRecipe = cva(
  "relative min-w-32 rounded-md border border-border bg-popover p-1 text-popover-foreground shadow-md outline-none transition-all duration-150 data-[starting-style]:scale-95 data-[starting-style]:opacity-0 data-[ending-style]:scale-95 data-[ending-style]:opacity-0",
);

/** The popup recipe's variant props, mixed into `MenuPopupProps`. */
export type MenuPopupRecipeProps = VariantProps<typeof menuPopupRecipe>;

/**
 * The little pointer between the trigger and the popup — Base UI's `Menu.Arrow`,
 * a `<div aria-hidden>`. Base UI sets `position: absolute` and an inline `left`
 * but never a `top`, so the recipe supplies the cross-axis offset per side. The
 * four `data-side` values are measured, not guessed: `bottom`, `top`,
 * `inline-start`, `inline-end`.
 */
export const menuArrowRecipe = cva(
  "size-2.5 rotate-45 rounded-sm border border-border bg-popover data-[side=bottom]:-top-1 data-[side=top]:-bottom-1 data-[side=inline-start]:-right-1 data-[side=inline-end]:-left-1",
);

/** The arrow recipe's variant props, mixed into `MenuArrowProps`. */
export type MenuArrowRecipeProps = VariantProps<typeof menuArrowRecipe>;

/**
 * The optional content-transition container inside the popup — Base UI's
 * `Menu.Viewport`, a `<div>` that wraps the current content in its own
 * `<div data-current>` child. Only needed when one popup is opened by several
 * triggers, so the recipe stays to the clipping a cross-fade needs.
 */
export const menuViewportRecipe = cva("relative overflow-hidden");

/** The viewport recipe's variant props, mixed into `MenuViewportProps`. */
export type MenuViewportRecipeProps = VariantProps<typeof menuViewportRecipe>;

/** A labelled section of related items — Base UI's `Menu.Group`, a `<div role="group">`. */
export const menuGroupRecipe = cva("flex flex-col");

/** The group recipe's variant props, mixed into `MenuGroupProps`. */
export type MenuGroupRecipeProps = VariantProps<typeof menuGroupRecipe>;

/**
 * A group's heading — Base UI's `Menu.GroupLabel`, a `<div role="presentation">`
 * that Base UI wires to its group through `aria-labelledby`.
 */
export const menuGroupLabelRecipe = cva("px-2 py-1.5 text-xs font-medium text-muted-foreground");

/** The group-label recipe's variant props, mixed into `MenuGroupLabelProps`. */
export type MenuGroupLabelRecipeProps = VariantProps<typeof menuGroupLabelRecipe>;

/*
 * The row shape all five selectable parts share (`Item`, `LinkItem`,
 * `CheckboxItem`, `RadioItem`, `SubmenuTrigger`) — the mirror of
 * `ITEM_BASE_CLASSES` in menu.test.tsx.
 *
 * Horizontal padding is deliberately NOT here. The checkbox and radio rows
 * replace the symmetric `px-2` with an asymmetric gutter, and tailwind-merge
 * does not treat `pl-*` as overriding `px-*`, so a `px-2` in this shared string
 * would survive next to their `pl-8`. Each recipe adds its own instead.
 */
const ITEM_BASE =
  "flex cursor-default select-none items-center gap-2 rounded-sm py-1.5 text-sm outline-none data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground data-[disabled]:opacity-50";

/**
 * One command row — Base UI's `Menu.Item`, a `<div role="menuitem">` carrying
 * `data-highlighted` while the roving focus is on it.
 */
export const menuItemRecipe = cva(`${ITEM_BASE} px-2`);

/** The item recipe's variant props, mixed into `MenuItemProps`. */
export type MenuItemRecipeProps = VariantProps<typeof menuItemRecipe>;

/** A navigating row — Base UI's `Menu.LinkItem`, an `<a role="menuitem">`. */
export const menuLinkItemRecipe = cva(`${ITEM_BASE} px-2 no-underline`);

/** The link-item recipe's variant props, mixed into `MenuLinkItemProps`. */
export type MenuLinkItemRecipeProps = VariantProps<typeof menuLinkItemRecipe>;

/**
 * A togglable row — Base UI's `Menu.CheckboxItem`, a
 * `<div role="menuitemcheckbox">`. The left padding is a GUTTER rather than the
 * item's usual symmetric padding, because the indicator only exists in the DOM
 * while the item is checked (measured) and an in-flow indicator would shift the
 * label sideways on every toggle.
 */
export const menuCheckboxItemRecipe = cva(`${ITEM_BASE} relative pr-2 pl-8`);

/** The checkbox-item recipe's variant props, mixed into `MenuCheckboxItemProps`. */
export type MenuCheckboxItemRecipeProps = VariantProps<typeof menuCheckboxItemRecipe>;

/**
 * The tick inside a checked checkbox item — Base UI's
 * `Menu.CheckboxItemIndicator`, a `<span>` that is absent from the DOM entirely
 * while unchecked. Absolutely positioned into the item's left gutter.
 */
export const menuCheckboxItemIndicatorRecipe = cva(
  "absolute left-2 flex size-4 shrink-0 items-center justify-center text-primary",
);

/** The checkbox-indicator recipe's variant props, mixed into `MenuCheckboxItemIndicatorProps`. */
export type MenuCheckboxItemIndicatorRecipeProps = VariantProps<
  typeof menuCheckboxItemIndicatorRecipe
>;

/** A set of mutually exclusive rows — Base UI's `Menu.RadioGroup`, a `<div role="group">`. */
export const menuRadioGroupRecipe = cva("flex flex-col");

/** The radio-group recipe's variant props, mixed into `MenuRadioGroupProps`. */
export type MenuRadioGroupRecipeProps = VariantProps<typeof menuRadioGroupRecipe>;

/**
 * One choice in a radio group — Base UI's `Menu.RadioItem`, a
 * `<div role="menuitemradio">`. Same left gutter as the checkbox item, for the
 * same reason.
 */
export const menuRadioItemRecipe = cva(`${ITEM_BASE} relative pr-2 pl-8`);

/** The radio-item recipe's variant props, mixed into `MenuRadioItemProps`. */
export type MenuRadioItemRecipeProps = VariantProps<typeof menuRadioItemRecipe>;

/**
 * The dot inside the selected radio item — Base UI's `Menu.RadioItemIndicator`,
 * a `<span>` that is absent from the DOM while its item is unselected.
 */
export const menuRadioItemIndicatorRecipe = cva(
  "absolute left-2 flex size-4 shrink-0 items-center justify-center text-primary",
);

/** The radio-indicator recipe's variant props, mixed into `MenuRadioItemIndicatorProps`. */
export type MenuRadioItemIndicatorRecipeProps = VariantProps<typeof menuRadioItemIndicatorRecipe>;

/**
 * The rule between two sections — Base UI's `Menu.Separator`, a
 * `<div role="separator">`. Negative horizontal margins pull it out to the
 * popup's own padding so the rule spans the full card.
 */
export const menuSeparatorRecipe = cva("-mx-1 my-1 h-px bg-border");

/** The separator recipe's variant props, mixed into `MenuSeparatorProps`. */
export type MenuSeparatorRecipeProps = VariantProps<typeof menuSeparatorRecipe>;

/**
 * The row that opens a nested menu — Base UI's `Menu.SubmenuTrigger`, a
 * `<div role="menuitem" aria-haspopup="menu">`. It keeps the item shape and adds
 * a `data-[popup-open]:` highlight, so the parent row stays lit while its
 * submenu is on screen.
 */
export const menuSubmenuTriggerRecipe = cva(
  `${ITEM_BASE} px-2 data-[popup-open]:bg-accent data-[popup-open]:text-accent-foreground`,
);

/** The submenu-trigger recipe's variant props, mixed into `MenuSubmenuTriggerProps`. */
export type MenuSubmenuTriggerRecipeProps = VariantProps<typeof menuSubmenuTriggerRecipe>;
