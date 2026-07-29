import { Combobox as BaseCombobox } from "@base-ui/react/combobox";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  comboboxArrowRecipe,
  type ComboboxArrowRecipeProps,
  comboboxBackdropRecipe,
  type ComboboxBackdropRecipeProps,
  comboboxChipRecipe,
  type ComboboxChipRecipeProps,
  comboboxChipRemoveRecipe,
  type ComboboxChipRemoveRecipeProps,
  comboboxChipsRecipe,
  type ComboboxChipsRecipeProps,
  comboboxClearRecipe,
  type ComboboxClearRecipeProps,
  comboboxEmptyRecipe,
  type ComboboxEmptyRecipeProps,
  comboboxGroupLabelRecipe,
  type ComboboxGroupLabelRecipeProps,
  comboboxGroupRecipe,
  type ComboboxGroupRecipeProps,
  comboboxIconRecipe,
  type ComboboxIconRecipeProps,
  comboboxInputGroupRecipe,
  type ComboboxInputGroupRecipeProps,
  comboboxInputRecipe,
  type ComboboxInputRecipeProps,
  comboboxItemIndicatorRecipe,
  type ComboboxItemIndicatorRecipeProps,
  comboboxItemRecipe,
  type ComboboxItemRecipeProps,
  comboboxLabelRecipe,
  type ComboboxLabelRecipeProps,
  comboboxListRecipe,
  type ComboboxListRecipeProps,
  comboboxPopupRecipe,
  type ComboboxPopupRecipeProps,
  comboboxPositionerRecipe,
  type ComboboxPositionerRecipeProps,
  comboboxRowRecipe,
  type ComboboxRowRecipeProps,
  comboboxSeparatorRecipe,
  type ComboboxSeparatorRecipeProps,
  comboboxStatusRecipe,
  type ComboboxStatusRecipeProps,
  comboboxTriggerRecipe,
  type ComboboxTriggerRecipeProps,
} from "./combobox.styles";

/**
 * Six of Base UI's twenty-eight namespace members are re-exported UNWRAPPED,
 * because none of them can carry a recipe — the Dialog exemplar's rule, applied
 * to the largest namespace in the catalog:
 *
 *   - `Root` renders no HTML element at all (it is the state container), and it
 *     is generic over the item value type and the multiple-selection flag —
 *     wrapping it would either drop the generics or add an element the DOM does
 *     not want.
 *   - `Value` renders no element either: it is a render-prop window onto the
 *     SELECTED value. It has no `className` prop to merge into.
 *   - `Collection` is the same shape for the FILTERED items — a mandatory
 *     function child, no element, no `className`.
 *   - `Portal` takes Floating UI's portal props (`container`, `keepMounted`) and
 *     renders only Base UI's structural `data-base-ui-portal` wrapper.
 *   - `useFilter` and `useFilteredItems` are hooks, not parts.
 *
 * The other twenty-two parts are wrapped so their recipes travel with them.
 */

/** Every Base UI `Combobox.Root` prop, generic over the item value type. */
export type ComboboxRootProps<
  Value = string,
  Multiple extends boolean | undefined = false,
> = Parameters<typeof BaseCombobox.Root<Value, Multiple>>[0];

/** Every Base UI `Combobox.Value` prop. This part renders no element. */
export type ComboboxValueProps = ComponentProps<typeof BaseCombobox.Value>;

/** Every Base UI `Combobox.Collection` prop. This part renders no element. */
export type ComboboxCollectionProps = ComponentProps<typeof BaseCombobox.Collection>;

/** Every Base UI `Combobox.Portal` prop. This part takes no `className`. */
export type ComboboxPortalProps = ComponentProps<typeof BaseCombobox.Portal>;

/*
 * `className` is deliberately narrowed back to `string` on every wrapped part:
 * Base UI widens it to `string | ((state) => string | undefined)`, and the
 * callback form cannot be merged with a recipe through `cn()`. Every component
 * in this catalog makes the same narrowing.
 */

/** Every Base UI `Combobox.Label` prop, with `className` narrowed to `string`. */
export interface ComboboxLabelProps
  extends Omit<ComponentProps<typeof BaseCombobox.Label>, "className">, ComboboxLabelRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.InputGroup` prop, with `className` narrowed to `string`. */
export interface ComboboxInputGroupProps
  extends
    Omit<ComponentProps<typeof BaseCombobox.InputGroup>, "className">,
    ComboboxInputGroupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Input` prop, with `className` narrowed to `string`. */
export interface ComboboxInputProps
  extends Omit<ComponentProps<typeof BaseCombobox.Input>, "className">, ComboboxInputRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Trigger` prop, with `className` narrowed to `string`. */
export interface ComboboxTriggerProps
  extends
    Omit<ComponentProps<typeof BaseCombobox.Trigger>, "className">,
    ComboboxTriggerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Icon` prop, with `className` narrowed to `string`. */
export interface ComboboxIconProps
  extends Omit<ComponentProps<typeof BaseCombobox.Icon>, "className">, ComboboxIconRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Clear` prop, with `className` narrowed to `string`. */
export interface ComboboxClearProps
  extends Omit<ComponentProps<typeof BaseCombobox.Clear>, "className">, ComboboxClearRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Backdrop` prop, with `className` narrowed to `string`. */
export interface ComboboxBackdropProps
  extends
    Omit<ComponentProps<typeof BaseCombobox.Backdrop>, "className">,
    ComboboxBackdropRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Positioner` prop, with `className` narrowed to `string`. */
export interface ComboboxPositionerProps
  extends
    Omit<ComponentProps<typeof BaseCombobox.Positioner>, "className">,
    ComboboxPositionerRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Popup` prop, with `className` narrowed to `string`. */
export interface ComboboxPopupProps
  extends Omit<ComponentProps<typeof BaseCombobox.Popup>, "className">, ComboboxPopupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Arrow` prop, with `className` narrowed to `string`. */
export interface ComboboxArrowProps
  extends Omit<ComponentProps<typeof BaseCombobox.Arrow>, "className">, ComboboxArrowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.List` prop, with `className` narrowed to `string`. */
export interface ComboboxListProps
  extends Omit<ComponentProps<typeof BaseCombobox.List>, "className">, ComboboxListRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Status` prop, with `className` narrowed to `string`. */
export interface ComboboxStatusProps
  extends Omit<ComponentProps<typeof BaseCombobox.Status>, "className">, ComboboxStatusRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Empty` prop, with `className` narrowed to `string`. */
export interface ComboboxEmptyProps
  extends Omit<ComponentProps<typeof BaseCombobox.Empty>, "className">, ComboboxEmptyRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Group` prop, with `className` narrowed to `string`. */
export interface ComboboxGroupProps
  extends Omit<ComponentProps<typeof BaseCombobox.Group>, "className">, ComboboxGroupRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.GroupLabel` prop, with `className` narrowed to `string`. */
export interface ComboboxGroupLabelProps
  extends
    Omit<ComponentProps<typeof BaseCombobox.GroupLabel>, "className">,
    ComboboxGroupLabelRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Row` prop, with `className` narrowed to `string`. */
export interface ComboboxRowProps
  extends Omit<ComponentProps<typeof BaseCombobox.Row>, "className">, ComboboxRowRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Item` prop, with `className` narrowed to `string`. */
export interface ComboboxItemProps
  extends Omit<ComponentProps<typeof BaseCombobox.Item>, "className">, ComboboxItemRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.ItemIndicator` prop, with `className` narrowed to `string`. */
export interface ComboboxItemIndicatorProps
  extends
    Omit<ComponentProps<typeof BaseCombobox.ItemIndicator>, "className">,
    ComboboxItemIndicatorRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Chips` prop, with `className` narrowed to `string`. */
export interface ComboboxChipsProps
  extends Omit<ComponentProps<typeof BaseCombobox.Chips>, "className">, ComboboxChipsRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Chip` prop, with `className` narrowed to `string`. */
export interface ComboboxChipProps
  extends Omit<ComponentProps<typeof BaseCombobox.Chip>, "className">, ComboboxChipRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.ChipRemove` prop, with `className` narrowed to `string`. */
export interface ComboboxChipRemoveProps
  extends
    Omit<ComponentProps<typeof BaseCombobox.ChipRemove>, "className">,
    ComboboxChipRemoveRecipeProps {
  readonly className?: string;
}

/** Every Base UI `Combobox.Separator` prop, with `className` narrowed to `string`. */
export interface ComboboxSeparatorProps
  extends
    Omit<ComponentProps<typeof BaseCombobox.Separator>, "className">,
    ComboboxSeparatorRecipeProps {
  readonly className?: string;
}

function ComboboxLabel({ className, ...rest }: ComboboxLabelProps): ReactElement {
  return <BaseCombobox.Label className={cn(comboboxLabelRecipe(), className)} {...rest} />;
}

function ComboboxInputGroup({ className, ...rest }: ComboboxInputGroupProps): ReactElement {
  return (
    <BaseCombobox.InputGroup className={cn(comboboxInputGroupRecipe(), className)} {...rest} />
  );
}

function ComboboxInput({ className, ...rest }: ComboboxInputProps): ReactElement {
  return <BaseCombobox.Input className={cn(comboboxInputRecipe(), className)} {...rest} />;
}

function ComboboxTrigger({ className, ...rest }: ComboboxTriggerProps): ReactElement {
  return <BaseCombobox.Trigger className={cn(comboboxTriggerRecipe(), className)} {...rest} />;
}

/**
 * The chevron `Combobox.Icon` falls back to when a caller passes no children —
 * the same default `Select.Icon` gets, for the same reasons: this package ships
 * no icon library and must not gain one, and a text glyph sits off the baseline
 * of the `size-4` icon box. `Autocomplete` re-exports these parts verbatim, so
 * this one default covers that component too.
 *
 * It does not rotate: `ComboboxIconState` is the empty interface, so the icon
 * carries no `data-popup-open` for a modifier to hang off.
 */
function DefaultChevron(): ReactElement {
  return (
    <svg
      className="size-4"
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="2"
      viewBox="0 0 24 24"
    >
      <path d="m6 9 6 6 6-6" />
    </svg>
  );
}

function ComboboxIcon({ className, children, ...rest }: ComboboxIconProps): ReactElement {
  return (
    <BaseCombobox.Icon className={cn(comboboxIconRecipe(), className)} {...rest}>
      {children ?? <DefaultChevron />}
    </BaseCombobox.Icon>
  );
}

function ComboboxClear({ className, ...rest }: ComboboxClearProps): ReactElement {
  return <BaseCombobox.Clear className={cn(comboboxClearRecipe(), className)} {...rest} />;
}

function ComboboxBackdrop({ className, ...rest }: ComboboxBackdropProps): ReactElement {
  return <BaseCombobox.Backdrop className={cn(comboboxBackdropRecipe(), className)} {...rest} />;
}

function ComboboxPositioner({ className, ...rest }: ComboboxPositionerProps): ReactElement {
  return (
    <BaseCombobox.Positioner className={cn(comboboxPositionerRecipe(), className)} {...rest} />
  );
}

function ComboboxPopup({ className, ...rest }: ComboboxPopupProps): ReactElement {
  return <BaseCombobox.Popup className={cn(comboboxPopupRecipe(), className)} {...rest} />;
}

function ComboboxArrow({ className, ...rest }: ComboboxArrowProps): ReactElement {
  return <BaseCombobox.Arrow className={cn(comboboxArrowRecipe(), className)} {...rest} />;
}

function ComboboxList({ className, ...rest }: ComboboxListProps): ReactElement {
  return <BaseCombobox.List className={cn(comboboxListRecipe(), className)} {...rest} />;
}

function ComboboxStatus({ className, ...rest }: ComboboxStatusProps): ReactElement {
  return <BaseCombobox.Status className={cn(comboboxStatusRecipe(), className)} {...rest} />;
}

function ComboboxEmpty({ className, ...rest }: ComboboxEmptyProps): ReactElement {
  return <BaseCombobox.Empty className={cn(comboboxEmptyRecipe(), className)} {...rest} />;
}

function ComboboxGroup({ className, ...rest }: ComboboxGroupProps): ReactElement {
  return <BaseCombobox.Group className={cn(comboboxGroupRecipe(), className)} {...rest} />;
}

function ComboboxGroupLabel({ className, ...rest }: ComboboxGroupLabelProps): ReactElement {
  return (
    <BaseCombobox.GroupLabel className={cn(comboboxGroupLabelRecipe(), className)} {...rest} />
  );
}

function ComboboxRow({ className, ...rest }: ComboboxRowProps): ReactElement {
  return <BaseCombobox.Row className={cn(comboboxRowRecipe(), className)} {...rest} />;
}

function ComboboxItem({ className, ...rest }: ComboboxItemProps): ReactElement {
  return <BaseCombobox.Item className={cn(comboboxItemRecipe(), className)} {...rest} />;
}

function ComboboxItemIndicator({ className, ...rest }: ComboboxItemIndicatorProps): ReactElement {
  return (
    <BaseCombobox.ItemIndicator
      className={cn(comboboxItemIndicatorRecipe(), className)}
      {...rest}
    />
  );
}

function ComboboxChips({ className, ...rest }: ComboboxChipsProps): ReactElement {
  return <BaseCombobox.Chips className={cn(comboboxChipsRecipe(), className)} {...rest} />;
}

function ComboboxChip({ className, ...rest }: ComboboxChipProps): ReactElement {
  return <BaseCombobox.Chip className={cn(comboboxChipRecipe(), className)} {...rest} />;
}

function ComboboxChipRemove({ className, ...rest }: ComboboxChipRemoveProps): ReactElement {
  return (
    <BaseCombobox.ChipRemove className={cn(comboboxChipRemoveRecipe(), className)} {...rest} />
  );
}

function ComboboxSeparator({ className, ...rest }: ComboboxSeparatorProps): ReactElement {
  return <BaseCombobox.Separator className={cn(comboboxSeparatorRecipe(), className)} {...rest} />;
}

/**
 * The catalog's combobox, as ONE namespace object whose keys mirror Base UI's
 * twenty-eight namespace members 1:1 — the catalog-wide convention for
 * multi-part components, so a caller who knows the Base UI docs already knows
 * this API.
 *
 * A minimal usable combobox is Root(items) > InputGroup(Input, Trigger(Icon))
 * plus a portalled Positioner > Popup > List > Item(ItemIndicator). Everything
 * else is opt-in: `Label` for a field label (on a TRIGGER-only combobox — Base
 * UI's label points at the trigger, so pair it with `Input` and Base UI warns),
 * `Clear` for a reset button, `Empty` and `Status` for the no-matches and
 * loading messages, `Group`/`GroupLabel`/`Separator` for sections, `Row` for
 * grid-shaped pickers, `Chips`/`Chip`/`ChipRemove` for a multi-select's selected
 * pills, `Backdrop` and `Arrow` for popup chrome, and `Value`/`Collection` as
 * renderless windows onto the selected value and the filtered items.
 *
 * Unlike every overlay in this catalog so far, the popup is NOT modal: it does
 * not park focus, it does not lock scroll, and it lays down no pointer-events
 * blocker over the page. A pointer click lands on an item directly.
 *
 * `useFilter` returns Base UI's `contains`/`startsWith`/`endsWith` matchers for
 * the current locale, and `useFilteredItems` applies one of them to the item
 * list — together they are how typing narrows the list.
 */
export const Combobox = {
  Root: BaseCombobox.Root,
  Label: ComboboxLabel,
  Value: BaseCombobox.Value,
  Input: ComboboxInput,
  InputGroup: ComboboxInputGroup,
  Trigger: ComboboxTrigger,
  List: ComboboxList,
  Status: ComboboxStatus,
  Portal: BaseCombobox.Portal,
  Backdrop: ComboboxBackdrop,
  Positioner: ComboboxPositioner,
  Popup: ComboboxPopup,
  Arrow: ComboboxArrow,
  Icon: ComboboxIcon,
  Group: ComboboxGroup,
  GroupLabel: ComboboxGroupLabel,
  Item: ComboboxItem,
  ItemIndicator: ComboboxItemIndicator,
  Chips: ComboboxChips,
  Chip: ComboboxChip,
  ChipRemove: ComboboxChipRemove,
  Row: ComboboxRow,
  Collection: BaseCombobox.Collection,
  Empty: ComboboxEmpty,
  Clear: ComboboxClear,
  Separator: ComboboxSeparator,
  useFilter: BaseCombobox.useFilter,
  useFilteredItems: BaseCombobox.useFilteredItems,
};
