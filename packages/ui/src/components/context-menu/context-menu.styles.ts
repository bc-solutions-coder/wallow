import { cva, type VariantProps } from "class-variance-authority";

/*
 * ONE recipe is the whole of this file, and that is the point of the component.
 * `@base-ui/react/context-menu` publishes nineteen namespace members, but
 * seventeen of them (`Backdrop`, `Portal`, `Positioner`, `Popup`, `Arrow`,
 * `Group`, `GroupLabel`, `Item`, `LinkItem`, `CheckboxItem`,
 * `CheckboxItemIndicator`, `RadioGroup`, `RadioItem`, `RadioItemIndicator`,
 * `SubmenuRoot`, `SubmenuTrigger`, `Separator`) are LITERAL re-exports of
 * `@base-ui/react/menu`'s own runtime — so this catalog re-exports Menu's
 * already-wrapped, already-styled parts rather than minting a second set of
 * identical recipes. See context-menu.tsx for why that decision goes the other
 * way here than it did for Alert Dialog.
 *
 * `Root` renders no DOM at all, which leaves `Trigger` — the right-click area —
 * as the only part this component styles itself.
 */

/**
 * The area that opens the menu on right click or long press — Base UI's
 * `ContextMenu.Trigger`, a `<div>`.
 *
 * Deliberately LAYOUT-NEUTRAL and COLOURLESS. A context-menu trigger wraps
 * whatever the caller already has on the page (a card, a table row, a canvas),
 * so a `display`, a padding or a background here would restyle the caller's own
 * content rather than the menu. What is left is the two things the trigger
 * genuinely owns:
 *
 *   - `select-none`, so a right-drag or a long press opens the menu instead of
 *     starting a text selection. Base UI solves the touch half of this itself
 *     (it sets `-webkit-touch-callout: none` inline, measured); this is the
 *     matching pointer half, which Base UI leaves to the styling layer.
 *   - a `data-[popup-open]:` ring, so the area the menu belongs to is visibly
 *     marked while the menu is on screen. It is a ring rather than a background
 *     precisely because it must not repaint the caller's content, and
 *     `rounded-md` is here only to give that ring the catalog's corner.
 *
 * No cva VARIANT: open/closed and pressed are Base UI `data-*` STATES, so they
 * belong in the base string as modifiers rather than as a variant axis nobody
 * would pass by hand. The `VariantProps` type is still exported so the part's
 * props keep the catalog-wide shape and a later variant stays additive.
 */
export const contextMenuTriggerRecipe = cva(
  "select-none rounded-md outline-none data-[popup-open]:ring-2 data-[popup-open]:ring-ring",
);

/** The trigger recipe's variant props, mixed into `ContextMenuTriggerProps`. */
export type ContextMenuTriggerRecipeProps = VariantProps<typeof contextMenuTriggerRecipe>;
