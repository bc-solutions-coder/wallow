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
 * An autocomplete is a combobox that commits the TEXT in its input rather than
 * an item object: the list is a set of suggestions, not a set of values, so
 * there is no selected item to tick and no chip to remove. Base UI models that
 * as literally the same component — `autocomplete/index.parts.d.ts` re-exports
 * `@base-ui/react/combobox`'s own runtime for TWENTY of its twenty-three
 * members, and only `Root`, `Value` and `useFilter` are autocomplete code:
 *
 *   - `Root` drops the whole selection surface from the combobox's prop type
 *     (`selectionMode`, `selectedValue`, `onSelectedValueChange`,
 *     `itemToStringValue`, `isItemEqualToValue`, `fillInputOnItemPress`) and adds
 *     `mode`, which chooses between filtering the list (`list`, the default),
 *     inline-completing the input (`inline`), both, or neither.
 *   - `Value` reads the INPUT value rather than the selected value — the one
 *     genuinely different runtime between the two components' same-named parts.
 *   - `useFilter` is Base UI's locale-aware `contains`/`startsWith`/`endsWith`
 *     matcher factory taking only `{ locale }`, where the combobox's takes the
 *     selection-aware `{ multiple, value, locale }`. Same return shape, different
 *     options — so they are NOT interchangeable and are not shared here.
 *
 * Every other member is the `Combobox` catalog component's ALREADY-WRAPPED part,
 * re-exported. Not a re-wrap: `Autocomplete.Popup` IS `Combobox.Popup`, the same
 * function object, so anything the combobox docs say about a part is true of the
 * same part here and a row styled for one is styled for the other. See
 * autocomplete.styles.ts for why sharing (the ContextMenu call) rather than
 * re-wrapping (the AlertDialog call) is the right trade for this pair.
 *
 * Three of those shared members are Base UI's own unwrapped re-exports on the
 * `Combobox` namespace (`Portal`, `Collection`, `useFilteredItems`), because
 * none of them renders a visible element.
 */

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
 * The catalog's autocomplete, as ONE namespace object whose keys mirror Base
 * UI's twenty-three namespace members 1:1 — the catalog-wide convention for
 * multi-part components, so a caller who knows the Base UI docs already knows
 * this API.
 *
 * A minimal usable autocomplete is Root(items) > InputGroup(Input) plus a
 * portalled Positioner > Popup > List > Item. Everything else is opt-in:
 * `Trigger`/`Icon` for a chevron that also opens the list, `Clear` for a reset
 * button, `Empty` and `Status` for the no-matches and loading messages,
 * `Group`/`GroupLabel`/`Separator` for sections, `Row` for grid-shaped
 * suggestions, `Backdrop` and `Arrow` for popup chrome, and `Value`/`Collection`
 * as renderless windows onto the input value and the filtered items.
 *
 * There is no `Label`, no `ItemIndicator` and no `Chips`/`Chip`/`ChipRemove`
 * here, in Base UI or in this catalog: nothing is "selected" to tick or to show
 * as a pill. Reach for `Combobox` when the control must commit item objects, and
 * for its `multiple` mode when it must commit several.
 */
export const Autocomplete = {
  Root: BaseAutocomplete.Root,
  Value: BaseAutocomplete.Value,
  Trigger: Combobox.Trigger,
  Input: Combobox.Input,
  InputGroup: Combobox.InputGroup,
  Icon: Combobox.Icon,
  Clear: Combobox.Clear,
  List: Combobox.List,
  Status: Combobox.Status,
  Portal: Combobox.Portal,
  Backdrop: Combobox.Backdrop,
  Positioner: Combobox.Positioner,
  Popup: Combobox.Popup,
  Arrow: Combobox.Arrow,
  Group: Combobox.Group,
  GroupLabel: Combobox.GroupLabel,
  Item: Combobox.Item,
  Row: Combobox.Row,
  Collection: Combobox.Collection,
  Empty: Combobox.Empty,
  Separator: Combobox.Separator,
  useFilter: BaseAutocomplete.useFilter,
  useFilteredItems: Combobox.useFilteredItems,
};
