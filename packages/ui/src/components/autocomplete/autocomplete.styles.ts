/*
 * An autocomplete is a combobox whose selection is the TEXT the user typed
 * rather than an item object, so it wears the combobox's chrome — the same
 * field, the same popup card, the same option rows. Base UI models it that way
 * literally: seventeen of the twenty-three members on `@base-ui/react/autocomplete`
 * are the combobox's own runtime re-exported, and `autocomplete/index.d.ts`
 * re-exports the combobox's PROP TYPES under autocomplete names rather than
 * declaring new ones.
 *
 * This file mirrors that shape for the recipe layer: every recipe below is the
 * combobox recipe under an autocomplete name, not a copy of it.
 *
 * *** THAT IS DELIBERATE, AND IT IS THE CONTEXT MENU CALL, NOT THE ALERT DIALOG
 * ONE. *** Alert Dialog gets its own recipes because an alert is different
 * chrome from a dialog and a fork restyling one rarely means the other. An
 * autocomplete is not different chrome from a combobox: it is the same control
 * with a different notion of "value". A fork that changes its popup radius or
 * its option row height means BOTH, and a duplicated recipe set would let the
 * two drift apart with no fork ever asking for it. Aliasing makes that drift
 * impossible by construction — `autocompleteItemRecipe` IS
 * `comboboxItemRecipe`, and a reference-equality assertion in
 * autocomplete.test.tsx holds it there.
 *
 * The file exists at all (rather than the component importing combobox.styles
 * directly) so that the folder keeps the catalog's five-file shape and so a fork
 * has one obvious place to break the aliasing if it ever does want the two to
 * diverge.
 *
 * Seventeen recipes, not twenty-two: an autocomplete has no `Label`,
 * no `ItemIndicator` and no `Chips`/`Chip`/`ChipRemove`, because it selects text
 * rather than a set of item objects.
 */

export {
  comboboxArrowRecipe as autocompleteArrowRecipe,
  comboboxBackdropRecipe as autocompleteBackdropRecipe,
  comboboxClearRecipe as autocompleteClearRecipe,
  comboboxEmptyRecipe as autocompleteEmptyRecipe,
  comboboxGroupLabelRecipe as autocompleteGroupLabelRecipe,
  comboboxGroupRecipe as autocompleteGroupRecipe,
  comboboxIconRecipe as autocompleteIconRecipe,
  comboboxInputGroupRecipe as autocompleteInputGroupRecipe,
  comboboxInputRecipe as autocompleteInputRecipe,
  comboboxItemRecipe as autocompleteItemRecipe,
  comboboxListRecipe as autocompleteListRecipe,
  comboboxPopupRecipe as autocompletePopupRecipe,
  comboboxPositionerRecipe as autocompletePositionerRecipe,
  comboboxRowRecipe as autocompleteRowRecipe,
  comboboxSeparatorRecipe as autocompleteSeparatorRecipe,
  comboboxStatusRecipe as autocompleteStatusRecipe,
  comboboxTriggerRecipe as autocompleteTriggerRecipe,
} from "../combobox/combobox.styles";

export type {
  ComboboxArrowRecipeProps as AutocompleteArrowRecipeProps,
  ComboboxBackdropRecipeProps as AutocompleteBackdropRecipeProps,
  ComboboxClearRecipeProps as AutocompleteClearRecipeProps,
  ComboboxEmptyRecipeProps as AutocompleteEmptyRecipeProps,
  ComboboxGroupLabelRecipeProps as AutocompleteGroupLabelRecipeProps,
  ComboboxGroupRecipeProps as AutocompleteGroupRecipeProps,
  ComboboxIconRecipeProps as AutocompleteIconRecipeProps,
  ComboboxInputGroupRecipeProps as AutocompleteInputGroupRecipeProps,
  ComboboxInputRecipeProps as AutocompleteInputRecipeProps,
  ComboboxItemRecipeProps as AutocompleteItemRecipeProps,
  ComboboxListRecipeProps as AutocompleteListRecipeProps,
  ComboboxPopupRecipeProps as AutocompletePopupRecipeProps,
  ComboboxPositionerRecipeProps as AutocompletePositionerRecipeProps,
  ComboboxRowRecipeProps as AutocompleteRowRecipeProps,
  ComboboxSeparatorRecipeProps as AutocompleteSeparatorRecipeProps,
  ComboboxStatusRecipeProps as AutocompleteStatusRecipeProps,
  ComboboxTriggerRecipeProps as AutocompleteTriggerRecipeProps,
} from "../combobox/combobox.styles";
