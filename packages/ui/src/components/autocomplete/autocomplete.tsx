import { Autocomplete as BaseAutocomplete } from "@base-ui/react/autocomplete";
import type {
  AutocompleteRootProps as BaseAutocompleteRootProps,
  AutocompleteValueProps as BaseAutocompleteValueProps,
} from "@base-ui/react/autocomplete";

import { Combobox } from "../combobox/combobox";
import type {
  ComboboxArrowProps,
  ComboboxBackdropProps,
  ComboboxClearProps,
  ComboboxCollectionProps,
  ComboboxEmptyProps,
  ComboboxGroupLabelProps,
  ComboboxGroupProps,
  ComboboxIconProps,
  ComboboxInputGroupProps,
  ComboboxInputProps,
  ComboboxItemProps,
  ComboboxListProps,
  ComboboxPopupProps,
  ComboboxPortalProps,
  ComboboxPositionerProps,
  ComboboxRowProps,
  ComboboxSeparatorProps,
  ComboboxStatusProps,
  ComboboxTriggerProps,
} from "../combobox/combobox";

/**
 * Every Base UI `Autocomplete.Root` prop, generic over the item value type.
 * Re-exported unwrapped: `Root` renders no HTML element, so no recipe props.
 *
 * Note the ONE generic parameter, against the combobox root's two — an
 * autocomplete has no `multiple`, because there is only ever one input value.
 */
export type AutocompleteRootProps<ItemValue = string> = BaseAutocompleteRootProps<ItemValue>;

/**
 * Every Base UI `Autocomplete.Value` prop. This part renders no element; its
 * render-prop child receives the input's `string` value, where the combobox's
 * receives the selected item.
 */
export type AutocompleteValueProps = BaseAutocompleteValueProps;

/*
 * The twenty shared parts' prop types, aliased onto autocomplete names. Base UI
 * publishes the very same aliases on this subpath (`AutocompletePopupProps` and
 * friends in autocomplete/index.d.ts), so a caller writing a wrapper around
 * `Autocomplete.Popup` never has to reach into the combobox folder to name its
 * props. They are type aliases, not new types: `AutocompletePopupProps` and
 * `ComboboxPopupProps` are interchangeable, exactly as the components are.
 */

/** Every `Autocomplete.Trigger` prop — the catalog `Combobox.Trigger`'s, unchanged. */
export type AutocompleteTriggerProps = ComboboxTriggerProps;

/** Every `Autocomplete.Input` prop — the catalog `Combobox.Input`'s, unchanged. */
export type AutocompleteInputProps = ComboboxInputProps;

/** Every `Autocomplete.InputGroup` prop — the catalog `Combobox.InputGroup`'s, unchanged. */
export type AutocompleteInputGroupProps = ComboboxInputGroupProps;

/** Every `Autocomplete.Icon` prop — the catalog `Combobox.Icon`'s, unchanged. */
export type AutocompleteIconProps = ComboboxIconProps;

/** Every `Autocomplete.Clear` prop — the catalog `Combobox.Clear`'s, unchanged. */
export type AutocompleteClearProps = ComboboxClearProps;

/** Every `Autocomplete.List` prop — the catalog `Combobox.List`'s, unchanged. */
export type AutocompleteListProps = ComboboxListProps;

/** Every `Autocomplete.Status` prop — the catalog `Combobox.Status`'s, unchanged. */
export type AutocompleteStatusProps = ComboboxStatusProps;

/** Every `Autocomplete.Portal` prop — the catalog `Combobox.Portal`'s, unchanged. */
export type AutocompletePortalProps = ComboboxPortalProps;

/** Every `Autocomplete.Backdrop` prop — the catalog `Combobox.Backdrop`'s, unchanged. */
export type AutocompleteBackdropProps = ComboboxBackdropProps;

/** Every `Autocomplete.Positioner` prop — the catalog `Combobox.Positioner`'s, unchanged. */
export type AutocompletePositionerProps = ComboboxPositionerProps;

/** Every `Autocomplete.Popup` prop — the catalog `Combobox.Popup`'s, unchanged. */
export type AutocompletePopupProps = ComboboxPopupProps;

/** Every `Autocomplete.Arrow` prop — the catalog `Combobox.Arrow`'s, unchanged. */
export type AutocompleteArrowProps = ComboboxArrowProps;

/** Every `Autocomplete.Group` prop — the catalog `Combobox.Group`'s, unchanged. */
export type AutocompleteGroupProps = ComboboxGroupProps;

/** Every `Autocomplete.GroupLabel` prop — the catalog `Combobox.GroupLabel`'s, unchanged. */
export type AutocompleteGroupLabelProps = ComboboxGroupLabelProps;

/** Every `Autocomplete.Item` prop — the catalog `Combobox.Item`'s, unchanged. */
export type AutocompleteItemProps = ComboboxItemProps;

/** Every `Autocomplete.Row` prop — the catalog `Combobox.Row`'s, unchanged. */
export type AutocompleteRowProps = ComboboxRowProps;

/** Every `Autocomplete.Collection` prop — the catalog `Combobox.Collection`'s, unchanged. */
export type AutocompleteCollectionProps = ComboboxCollectionProps;

/** Every `Autocomplete.Empty` prop — the catalog `Combobox.Empty`'s, unchanged. */
export type AutocompleteEmptyProps = ComboboxEmptyProps;

/** Every `Autocomplete.Separator` prop — the catalog `Combobox.Separator`'s, unchanged. */
export type AutocompleteSeparatorProps = ComboboxSeparatorProps;

/**
 * Text suggestions without an item selection. Root owns the input value; Value reads that
 * text. Shared list and popup parts use Combobox styling; useFilter provides locale-aware
 * string matching.
 */
export const Autocomplete = {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: BaseAutocomplete.Root,
  /**
   * Displays the current value using the part's children or formatting options.
   */
  Value: BaseAutocomplete.Value,
  /**
   * Opens or toggles the associated content; render can compose it onto another control.
   */
  Trigger: Combobox.Trigger,
  /**
   * The editable input connected to Root state.
   */
  Input: Combobox.Input,
  /**
   * Groups the input with its trigger, clear button, and icon.
   */
  InputGroup: Combobox.InputGroup,
  /**
   * Visual indicator beside the input or selected value.
   */
  Icon: Combobox.Icon,
  /**
   * Clears the current input or selection.
   */
  Clear: Combobox.Clear,
  /**
   * Contains selectable items.
   */
  List: Combobox.List,
  /**
   * Announces status changes such as loading or result counts.
   */
  Status: Combobox.Status,
  /**
   * Renders popup content outside the parent DOM hierarchy.
   */
  Portal: Combobox.Portal,
  /**
   * Covers the surrounding page behind the popup.
   */
  Backdrop: Combobox.Backdrop,
  /**
   * Positions the popup relative to its anchor; render inside Portal.
   */
  Positioner: Combobox.Positioner,
  /**
   * The visible popup container; place labeled content and actions inside it.
   */
  Popup: Combobox.Popup,
  /**
   * Draws the popup's pointer toward its anchor.
   */
  Arrow: Combobox.Arrow,
  /**
   * Groups related items or controls.
   */
  Group: Combobox.Group,
  /**
   * Labels a group of items.
   */
  GroupLabel: Combobox.GroupLabel,
  /**
   * Groups one item and its associated content.
   */
  Item: Combobox.Item,
  /**
   * Groups items in a list row.
   */
  Row: Combobox.Row,
  /**
   * Renders items from the collection supplied to Root.
   */
  Collection: Combobox.Collection,
  /**
   * Displays content when no items match.
   */
  Empty: Combobox.Empty,
  /**
   * Separates adjacent groups or content.
   */
  Separator: Combobox.Separator,
  /**
   * Creates locale-aware contains, startsWith, and endsWith string matchers.
   */
  useFilter: BaseAutocomplete.useFilter,
  /**
   * Reads the filtered items from the surrounding collection context.
   */
  useFilteredItems: Combobox.useFilteredItems,
};
