import { cva, type VariantProps } from "class-variance-authority";

/*
 * One recipe per styled part of the combobox. The class lists are not invented
 * here — combobox.test.tsx declares each part's exact utility set as an
 * order-free `*_CLASSES` constant and asserts it THROUGH the rendered component,
 * so that spec is the source of truth for everything below.
 *
 * One recipe per VISIBLE part — twenty-two of Base UI's twenty-eight namespace
 * members. The other six (`Root`, `Value`, `Collection`, `Portal`, `useFilter`,
 * `useFilteredItems`) render no visible element, so they carry no recipe; see
 * combobox.tsx for that split.
 *
 * No recipe takes a cva VARIANT, for the same reason Select's do not: a combobox
 * has no visual variant axis in this catalog. Open/closed, highlighted, selected,
 * disabled, placeholder, empty-list and clear-visible are all STATES, and Base UI
 * publishes states as `data-*` attributes, so they belong in the base string as
 * `data-[popup-open]:` / `data-[highlighted]:` / … modifiers rather than as cva
 * variants nobody would pass by hand. The `VariantProps` types are still exported
 * so each part's props keep the catalog-wide shape and a later variant axis stays
 * a non-breaking addition.
 */

/** The field label above the control — Base UI's `Combobox.Label`, a `<div>`. */
export const comboboxLabelRecipe = cva("text-sm font-medium text-foreground");

/** The label recipe's variant props, mixed into `ComboboxLabelProps`. */
export type ComboboxLabelRecipeProps = VariantProps<typeof comboboxLabelRecipe>;

/**
 * The bordered field shell wrapping the input and its controls — Base UI's
 * `Combobox.InputGroup`, a `<div role="group">` carrying `data-popup-open`,
 * `data-pressed`, `data-popup-side`, `data-placeholder` and `data-list-empty`.
 */
export const comboboxInputGroupRecipe = cva(
  "flex w-full items-center gap-2 rounded-md border border-input bg-background px-3 py-2 text-sm text-foreground data-[popup-open]:border-ring data-[disabled]:opacity-50",
);

/** The input-group recipe's variant props, mixed into `ComboboxInputGroupProps`. */
export type ComboboxInputGroupRecipeProps = VariantProps<typeof comboboxInputGroupRecipe>;

/**
 * The text field itself — Base UI's `Combobox.Input`, an
 * `<input role="combobox" aria-autocomplete="list">`. The border and background
 * live on the InputGroup, so this recipe stays transparent and borderless.
 */
export const comboboxInputRecipe = cva(
  "min-w-0 flex-1 bg-transparent text-sm text-foreground outline-none placeholder:text-muted-foreground",
);

/** The input recipe's variant props, mixed into `ComboboxInputProps`. */
export type ComboboxInputRecipeProps = VariantProps<typeof comboboxInputRecipe>;

/**
 * The button that opens the popup — Base UI's `Combobox.Trigger`, a `<button>`.
 * Alongside an `Input` it is a chevron affordance (`tabindex="-1"`,
 * `aria-hidden` while open, because the input owns the combobox role); used
 * without an `Input` it becomes the whole control.
 */
export const comboboxTriggerRecipe = cva(
  "inline-flex shrink-0 cursor-default items-center justify-center text-muted-foreground outline-none data-[disabled]:opacity-50",
);

/** The trigger recipe's variant props, mixed into `ComboboxTriggerProps`. */
export type ComboboxTriggerRecipeProps = VariantProps<typeof comboboxTriggerRecipe>;

/**
 * The chevron glyph inside the trigger — Base UI's `Combobox.Icon`, an
 * `aria-hidden` `<span>`.
 *
 * MEASURED, AND THE ONE PLACE THIS DIVERGES FROM SELECT: `ComboboxIconState` is
 * the EMPTY interface, and the rendered `<span>` carries no `data-*` attribute at
 * all — not even `data-popup-open`. Select's `data-[popup-open]:rotate-180` flip
 * is therefore impossible on this part; a `data-[popup-open]:` modifier here
 * would be well-formed CSS that can never match. Any fork wanting the flip must
 * drive it from `Combobox.Trigger`, which does carry `data-popup-open`.
 */
export const comboboxIconRecipe = cva("flex size-4 shrink-0 items-center justify-center");

/** The icon recipe's variant props, mixed into `ComboboxIconProps`. */
export type ComboboxIconRecipeProps = VariantProps<typeof comboboxIconRecipe>;

/**
 * The button that empties the input — Base UI's `Combobox.Clear`, a `<button>`.
 * It is unmounted while there is nothing to clear unless `keepMounted` is set,
 * in which case it stays in the DOM and Base UI marks the useful state with
 * `data-visible` — which is why this recipe fades on that attribute rather than
 * assuming presence means visible.
 */
export const comboboxClearRecipe = cva(
  "flex size-4 shrink-0 cursor-default items-center justify-center rounded-sm text-muted-foreground outline-none data-[visible]:opacity-100",
);

/** The clear recipe's variant props, mixed into `ComboboxClearProps`. */
export type ComboboxClearRecipeProps = VariantProps<typeof comboboxClearRecipe>;

/** The overlay behind an open popup — Base UI's `Combobox.Backdrop`, a `<div>`. */
export const comboboxBackdropRecipe = cva("fixed inset-0");

/** The backdrop recipe's variant props, mixed into `ComboboxBackdropProps`. */
export type ComboboxBackdropRecipeProps = VariantProps<typeof comboboxBackdropRecipe>;

/**
 * The anchored wrapper Base UI positions — `Combobox.Positioner`. It owns the
 * inline `position`/`transform` styles and the `--anchor-*` custom properties, so
 * the recipe may only add stacking and focus concerns, never layout that would
 * fight the positioning engine. (Same rule as `Select.Positioner`; the opposite
 * of `Dialog.Popup`, which positions itself.)
 */
export const comboboxPositionerRecipe = cva("z-50 outline-none");

/** The positioner recipe's variant props, mixed into `ComboboxPositionerProps`. */
export type ComboboxPositionerRecipeProps = VariantProps<typeof comboboxPositionerRecipe>;

/**
 * The popup card itself — Base UI's `Combobox.Popup`, a `<div>` that also gains
 * `data-empty` when filtering leaves no items.
 *
 * Deliberately carries NO enter/exit transition, following `Select.Popup` rather
 * than `Dialog.Popup`. A combobox popup reopens on every keystroke-driven state
 * change, and a transition there both feels wrong and re-creates the Wave-2
 * gotcha where a story's `toBeVisible()` races a 150ms fade.
 */
export const comboboxPopupRecipe = cva(
  "min-w-[var(--anchor-width)] rounded-md border border-border bg-popover py-1 text-popover-foreground shadow-md",
);

/** The popup recipe's variant props, mixed into `ComboboxPopupProps`. */
export type ComboboxPopupRecipeProps = VariantProps<typeof comboboxPopupRecipe>;

/** The pointer triangle — Base UI's `Combobox.Arrow`, an `aria-hidden` `<div>`. */
export const comboboxArrowRecipe = cva("flex text-popover-foreground");

/** The arrow recipe's variant props, mixed into `ComboboxArrowProps`. */
export type ComboboxArrowRecipeProps = VariantProps<typeof comboboxArrowRecipe>;

/**
 * The scroll container holding the items — Base UI's `Combobox.List`, a
 * `<div role="listbox">` that gains `data-empty` when nothing matches.
 *
 * Unlike `Select.List`, Base UI adds NO class of its own here (no
 * `base-ui-disable-scrollbar`), so the rendered class set is the recipe alone.
 */
export const comboboxListRecipe = cva("max-h-64 overflow-y-auto outline-none");

/** The list recipe's variant props, mixed into `ComboboxListProps`. */
export type ComboboxListRecipeProps = VariantProps<typeof comboboxListRecipe>;

/**
 * The politely-announced status line for an asynchronously loading list — Base
 * UI's `Combobox.Status`, a `<div role="status" aria-live="polite">`. It must
 * stay mounted to announce reliably, so callers change its children rather than
 * conditionally rendering it.
 */
export const comboboxStatusRecipe = cva("px-3 py-1.5 text-sm text-muted-foreground");

/** The status recipe's variant props, mixed into `ComboboxStatusProps`. */
export type ComboboxStatusRecipeProps = VariantProps<typeof comboboxStatusRecipe>;

/**
 * The "nothing matched" row — Base UI's `Combobox.Empty`, also a
 * `<div role="status" aria-live="polite">`. Base UI renders its children only
 * while the filtered list is empty, so this recipe styles a centred message
 * block rather than a list row.
 */
export const comboboxEmptyRecipe = cva("px-3 py-6 text-center text-sm text-muted-foreground");

/** The empty recipe's variant props, mixed into `ComboboxEmptyProps`. */
export type ComboboxEmptyRecipeProps = VariantProps<typeof comboboxEmptyRecipe>;

/** A set of related options — Base UI's `Combobox.Group`, a `<div role="group">`. */
export const comboboxGroupRecipe = cva("py-1");

/** The group recipe's variant props, mixed into `ComboboxGroupProps`. */
export type ComboboxGroupRecipeProps = VariantProps<typeof comboboxGroupRecipe>;

/** A group's heading — Base UI's `Combobox.GroupLabel`, a `<div>`. */
export const comboboxGroupLabelRecipe = cva(
  "px-3 py-1.5 text-xs font-medium text-muted-foreground",
);

/** The group-label recipe's variant props, mixed into `ComboboxGroupLabelProps`. */
export type ComboboxGroupLabelRecipeProps = VariantProps<typeof comboboxGroupLabelRecipe>;

/**
 * A grid row of items — Base UI's `Combobox.Row`, a `<div role="row">`.
 *
 * MEASURED: wrapping items in a `Row` re-roles every `Combobox.Item` inside it
 * from `option` to `gridcell`. It is opt-in for grid-shaped pickers (a colour
 * swatch palette, an emoji picker) and changes nothing about the item recipe.
 */
export const comboboxRowRecipe = cva("flex gap-1");

/** The row recipe's variant props, mixed into `ComboboxRowProps`. */
export type ComboboxRowRecipeProps = VariantProps<typeof comboboxRowRecipe>;

/**
 * One option row — Base UI's `Combobox.Item`, a `<div role="option">` carrying
 * `data-selected`, `data-highlighted` and `data-disabled`.
 */
export const comboboxItemRecipe = cva(
  "flex cursor-default select-none items-center gap-2 px-3 py-1.5 text-sm outline-none data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground data-[disabled]:opacity-50",
);

/** The item recipe's variant props, mixed into `ComboboxItemProps`. */
export type ComboboxItemRecipeProps = VariantProps<typeof comboboxItemRecipe>;

/**
 * The tick beside a selected option — Base UI's `Combobox.ItemIndicator`, a
 * `<span>` that is only in the DOM while its item is selected unless
 * `keepMounted` is set.
 */
export const comboboxItemIndicatorRecipe = cva(
  "flex size-4 shrink-0 items-center justify-center text-primary",
);

/** The item-indicator recipe's variant props, mixed into `ComboboxItemIndicatorProps`. */
export type ComboboxItemIndicatorRecipeProps = VariantProps<typeof comboboxItemIndicatorRecipe>;

/**
 * The container for the selected-value chips of a multi-select combobox — Base
 * UI's `Combobox.Chips`, a `<div role="toolbar">`.
 */
export const comboboxChipsRecipe = cva("flex flex-wrap items-center gap-1");

/** The chips recipe's variant props, mixed into `ComboboxChipsProps`. */
export type ComboboxChipsRecipeProps = VariantProps<typeof comboboxChipsRecipe>;

/** One selected-value pill — Base UI's `Combobox.Chip`, a roving-focus `<div>`. */
export const comboboxChipRecipe = cva(
  "inline-flex cursor-default items-center gap-1 rounded-sm bg-secondary px-2 py-0.5 text-xs text-secondary-foreground outline-none data-[disabled]:opacity-50",
);

/** The chip recipe's variant props, mixed into `ComboboxChipProps`. */
export type ComboboxChipRecipeProps = VariantProps<typeof comboboxChipRecipe>;

/** A chip's dismiss button — Base UI's `Combobox.ChipRemove`, a `<button>`. */
export const comboboxChipRemoveRecipe = cva(
  "inline-flex size-3 shrink-0 cursor-default items-center justify-center rounded-sm text-secondary-foreground outline-none",
);

/** The chip-remove recipe's variant props, mixed into `ComboboxChipRemoveProps`. */
export type ComboboxChipRemoveRecipeProps = VariantProps<typeof comboboxChipRemoveRecipe>;

/**
 * The rule between groups — `Combobox.Separator`, a `<div role="separator">`.
 *
 * Base UI re-exports the standalone `@base-ui/react/separator` component under
 * this name, exactly as `Select` ships its own embedded separator: this is the
 * combobox's INTERNAL divider and its recipe is sized for a popup list, not the
 * catalog's standalone `Separator` component.
 */
export const comboboxSeparatorRecipe = cva("my-1 h-px bg-border");

/** The separator recipe's variant props, mixed into `ComboboxSeparatorProps`. */
export type ComboboxSeparatorRecipeProps = VariantProps<typeof comboboxSeparatorRecipe>;
