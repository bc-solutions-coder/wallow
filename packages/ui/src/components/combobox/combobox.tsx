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
 * An editable item picker with single or multiple selection. Compose Root with InputGroup,
 * Input, Trigger, and a portalled Positioner > Popup > List. Chips and ChipRemove display and
 * remove multiple selections.
 */
export const Combobox = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BaseCombobox.Root,
  /**
   * Provides the control's accessible label.
   */
  Label: ComboboxLabel,
  /**
   * Displays the current value using the part's children or formatting options.
   */
  Value: BaseCombobox.Value,
  /**
   * The editable input connected to Root state.
   */
  Input: ComboboxInput,
  /**
   * Groups the input with its trigger, clear button, and icon.
   */
  InputGroup: ComboboxInputGroup,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: ComboboxTrigger,
  /**
   * Contains selectable items.
   */
  List: ComboboxList,
  /**
   * Announces status changes such as loading or result counts.
   */
  Status: ComboboxStatus,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: BaseCombobox.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: ComboboxBackdrop,
  /**
   * Positions the popup relative to its anchor; render inside Portal.
   */
  Positioner: ComboboxPositioner,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: ComboboxPopup,
  /**
   * Draws the popup's pointer toward its anchor.
   */
  Arrow: ComboboxArrow,
  /**
   * Visual indicator beside the input or selected value.
   */
  Icon: ComboboxIcon,
  /**
   * Groups related items or controls.
   */
  Group: ComboboxGroup,
  /**
   * Labels a group of items.
   */
  GroupLabel: ComboboxGroupLabel,
  /**
   * Groups one item and its associated content.
   */
  Item: ComboboxItem,
  /**
   * Displays content when its item is selected.
   */
  ItemIndicator: ComboboxItemIndicator,
  /**
   * Contains the chips for multiple selected items.
   */
  Chips: ComboboxChips,
  /**
   * Displays one selected item in a multiple-selection input.
   */
  Chip: ComboboxChip,
  /**
   * Removes its selected chip when activated.
   */
  ChipRemove: ComboboxChipRemove,
  /**
   * Groups items in a list row.
   */
  Row: ComboboxRow,
  /**
   * Renders items from the collection supplied to Root.
   */
  Collection: BaseCombobox.Collection,
  /**
   * Displays content when no items match.
   */
  Empty: ComboboxEmpty,
  /**
   * Clears the current input or selection.
   */
  Clear: ComboboxClear,
  /**
   * Separates adjacent groups or content.
   */
  Separator: ComboboxSeparator,
  /**
   * Creates locale-aware contains, startsWith, and endsWith string matchers.
   */
  useFilter: BaseCombobox.useFilter,
  /**
   * Reads the filtered items from the surrounding collection context.
   */
  useFilteredItems: BaseCombobox.useFilteredItems,
};
