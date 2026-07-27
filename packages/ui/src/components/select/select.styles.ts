import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the select. The class lists are not invented
 * here — select.test.tsx declares each part's exact utility set as an order-free
 * constant and asserts it through the rendered component, so that spec is the
 * source of truth for everything below.
 *
 * No recipe takes a cva VARIANT. A select has no visual variant axis in this
 * catalog: open/closed, selected, highlighted, disabled and placeholder are all
 * STATES, and Base UI publishes states as `data-*` attributes, so they belong in
 * the base string as `data-[popup-open]:` / `data-[highlighted]:` / …
 * modifiers rather than as cva variants nobody would ever pass by hand. The
 * `VariantProps` types are still exported so each part's props keep the
 * catalog-wide shape and a later variant axis stays a non-breaking addition.
 */

/** The field label above the trigger — Base UI's `Select.Label`, a `<div>`. */
export const selectLabelRecipe = cva("text-sm font-medium text-foreground");

/** The label recipe's variant props, mixed into `SelectLabelProps`. */
export type SelectLabelRecipeProps = VariantProps<typeof selectLabelRecipe>;

/**
 * The closed control — Base UI's `Select.Trigger`, a `<button role="combobox">`
 * that carries `data-popup-open` while the popup is open and `data-disabled` /
 * `data-placeholder` for the field states.
 */
export const selectTriggerRecipe = cva(
  "inline-flex w-full items-center justify-between gap-2 rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground data-[popup-open]:border-ring data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `SelectTriggerProps`. */
export type SelectTriggerRecipeProps = VariantProps<typeof selectTriggerRecipe>;

/**
 * The selected item's text inside the trigger — Base UI's `Select.Value`, a
 * `<span>` marked `data-placeholder` while nothing is selected.
 */
export const selectValueRecipe = cva("truncate text-left data-[placeholder]:text-muted-foreground");

/** The value recipe's variant props, mixed into `SelectValueProps`. */
export type SelectValueRecipeProps = VariantProps<typeof selectValueRecipe>;

/**
 * The chevron at the end of the trigger — Base UI's `Select.Icon`, an
 * `aria-hidden` `<span>` that mirrors the trigger's `data-popup-open`.
 */
export const selectIconRecipe = cva(
  "flex size-4 shrink-0 items-center justify-center text-muted-foreground transition-transform data-[popup-open]:rotate-180",
);

/** The icon recipe's variant props, mixed into `SelectIconProps`. */
export type SelectIconRecipeProps = VariantProps<typeof selectIconRecipe>;

/** The overlay behind an open popup — Base UI's `Select.Backdrop`, a `<div>`. */
export const selectBackdropRecipe = cva("fixed inset-0");

/** The backdrop recipe's variant props, mixed into `SelectBackdropProps`. */
export type SelectBackdropRecipeProps = VariantProps<typeof selectBackdropRecipe>;

/**
 * The anchored wrapper Base UI positions — `Select.Positioner`. It owns the
 * inline `position`/`transform` styles, so the recipe may only add stacking and
 * focus concerns, never layout that would fight the positioning engine.
 */
export const selectPositionerRecipe = cva("z-50 outline-none");

/** The positioner recipe's variant props, mixed into `SelectPositionerProps`. */
export type SelectPositionerRecipeProps = VariantProps<typeof selectPositionerRecipe>;

/** The popup card itself — Base UI's `Select.Popup`, a `<div>`. */
export const selectPopupRecipe = cva(
  "rounded-md border border-border bg-popover py-1 text-popover-foreground shadow-md",
);

/** The popup recipe's variant props, mixed into `SelectPopupProps`. */
export type SelectPopupRecipeProps = VariantProps<typeof selectPopupRecipe>;

/**
 * The scroll container holding the items — Base UI's `Select.List`, a
 * `<div role="listbox">`. Base UI injects its own `base-ui-disable-scrollbar`
 * class here, so this is the one part whose rendered class set is the recipe
 * PLUS a Base UI class (select.test.tsx pins that).
 */
export const selectListRecipe = cva("max-h-64 overflow-y-auto outline-none");

/** The list recipe's variant props, mixed into `SelectListProps`. */
export type SelectListRecipeProps = VariantProps<typeof selectListRecipe>;

/**
 * One option row — Base UI's `Select.Item`, a `<div role="option">` carrying
 * `data-selected`, `data-highlighted` and `data-disabled`.
 */
export const selectItemRecipe = cva(
  "flex cursor-default select-none items-center gap-2 px-3 py-1.5 text-sm outline-none data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground data-[disabled]:opacity-50",
);

/** The item recipe's variant props, mixed into `SelectItemProps`. */
export type SelectItemRecipeProps = VariantProps<typeof selectItemRecipe>;

/** An item's label — Base UI's `Select.ItemText`, a `<div>`. */
export const selectItemTextRecipe = cva("flex-1 truncate");

/** The item-text recipe's variant props, mixed into `SelectItemTextProps`. */
export type SelectItemTextRecipeProps = VariantProps<typeof selectItemTextRecipe>;

/**
 * The tick beside the selected option — Base UI's `Select.ItemIndicator`, a
 * `<span>` that is only in the DOM while its item is selected unless
 * `keepMounted` is set.
 */
export const selectItemIndicatorRecipe = cva(
  "flex size-4 shrink-0 items-center justify-center text-primary",
);

/** The item-indicator recipe's variant props, mixed into `SelectItemIndicatorProps`. */
export type SelectItemIndicatorRecipeProps = VariantProps<typeof selectItemIndicatorRecipe>;

/** The pointer triangle — Base UI's `Select.Arrow`, an `aria-hidden` `<div>`. */
export const selectArrowRecipe = cva("flex text-popover-foreground");

/** The arrow recipe's variant props, mixed into `SelectArrowProps`. */
export type SelectArrowRecipeProps = VariantProps<typeof selectArrowRecipe>;

/**
 * The hover-to-scroll affordances at the top and bottom of an overflowing
 * popup. ONE recipe drives BOTH `Select.ScrollUpArrow` and
 * `Select.ScrollDownArrow` — they are the same band, and Base UI already
 * distinguishes them with `data-direction="up" | "down"` for any caller that
 * wants to tell them apart.
 */
export const selectScrollArrowRecipe = cva(
  "flex h-6 cursor-default items-center justify-center bg-popover text-muted-foreground",
);

/** The scroll-arrow recipe's variant props, mixed into both scroll-arrow props. */
export type SelectScrollArrowRecipeProps = VariantProps<typeof selectScrollArrowRecipe>;

/** A set of related options — Base UI's `Select.Group`, a `<div role="group">`. */
export const selectGroupRecipe = cva("py-1");

/** The group recipe's variant props, mixed into `SelectGroupProps`. */
export type SelectGroupRecipeProps = VariantProps<typeof selectGroupRecipe>;

/** A group's heading — Base UI's `Select.GroupLabel`, a `<div>`. */
export const selectGroupLabelRecipe = cva("px-3 py-1.5 text-xs font-medium text-muted-foreground");

/** The group-label recipe's variant props, mixed into `SelectGroupLabelProps`. */
export type SelectGroupLabelRecipeProps = VariantProps<typeof selectGroupLabelRecipe>;

/** The rule between groups — Base UI's `Select.Separator`, a `<div role="separator">`. */
export const selectSeparatorRecipe = cva("my-1 h-px bg-border");

/** The separator recipe's variant props, mixed into `SelectSeparatorProps`. */
export type SelectSeparatorRecipeProps = VariantProps<typeof selectSeparatorRecipe>;
