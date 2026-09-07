/**
 * String-valued select field. An empty string means no selection; displayed labels come from
 * options while form state stores option values.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import { Select } from "@bc-solutions-coder/ui/select";
import type { ReactElement } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

/** One choice: `value` travels on the wire, `label` is what a user reads. */
export interface SelectFieldOption {
  /** Value stored in the form when selected. Use an empty string for no selection. */
  readonly value: string;
  readonly label: string;
}

/**
 * Labels and presentation for SelectField. The field value and validation come from the
 * surrounding AppField.
 */
export interface SelectFieldProps {
  /** The visible label, associated with the trigger by the ui `Field` row. */
  readonly label: string;
  /** Available value/label pairs in display order. Values identify options and should be unique. */
  readonly options: readonly SelectFieldOption[];
  /** Shown on the trigger while nothing is chosen. */
  readonly placeholder?: string;
  /** Add an optional marker to the label. Does not change schema validation. */
  readonly optional?: boolean;
  /**
   * Overrides the derived `{testIdPrefix}-{field name}` testid (and its `-error`
   * id). It names the TRIGGER — the element every suite clicks.
   */
  readonly testId?: string;
}

/** One option row in the popup — `role="option"`, named by its label. */
function SelectFieldOptionRow({ option }: { readonly option: SelectFieldOption }): ReactElement {
  return (
    <Select.Item value={option.value}>
      <Select.ItemText>{option.label}</Select.ItemText>
    </Select.Item>
  );
}

/** The listbox itself. */
function SelectFieldOptionList({
  options,
}: {
  /** Available value/label pairs in display order. Values identify options and should be unique. */
  readonly options: readonly SelectFieldOption[];
}): ReactElement {
  return (
    <Select.List>
      {options.map((option: SelectFieldOption) => (
        <SelectFieldOptionRow key={option.value} option={option} />
      ))}
    </Select.List>
  );
}

/** The popup card the list sits on. */
function SelectFieldPopupCard({
  options,
}: {
  /** Available value/label pairs in display order. Values identify options and should be unique. */
  readonly options: readonly SelectFieldOption[];
}): ReactElement {
  return (
    <Select.Popup>
      <SelectFieldOptionList options={options} />
    </Select.Popup>
  );
}

/**
 * Render the popup outside the form container. Browser tests query the page to find portalled
 * options.
 */
function SelectFieldPopupLayer({
  options,
}: {
  /** Available value/label pairs in display order. Values identify options and should be unique. */
  readonly options: readonly SelectFieldOption[];
}): ReactElement {
  return (
    <Select.Portal>
      <Select.Positioner>
        <SelectFieldPopupCard options={options} />
      </Select.Positioner>
    </Select.Portal>
  );
}

/** The closed control: the chosen label (or the placeholder) and a chevron. */
function SelectFieldTrigger({
  testId,
  placeholder,
  onBlur,
}: {
  readonly testId: string;
  readonly placeholder: string | undefined;
  readonly onBlur: () => void;
}): ReactElement {
  return (
    <Select.Trigger data-testid={testId} onBlur={onBlur}>
      <Select.Value placeholder={placeholder} />
      <Select.Icon />
    </Select.Trigger>
  );
}

/**
 * Render a string-valued select inside AppForm and a matching AppField. Empty string means no
 * selection. Option labels are displayed while option values are submitted; the control disables
 * while pending.
 */
export function SelectField({
  label,
  options,
  placeholder,
  optional = false,
  testId,
}: SelectFieldProps): ReactElement {
  const { field, pending, error, controlTestId, errorTestId } = useCatalogField<string>(testId);

  return (
    <Field invalid={error !== undefined}>
      <CatalogFieldLabel label={label} optional={optional} />
      <Select.Root
        items={options}
        disabled={pending}
        value={field.state.value === "" ? null : field.state.value}
        onValueChange={(next: string | null) => {
          field.handleChange(next ?? "");
        }}
      >
        <SelectFieldTrigger
          testId={controlTestId}
          placeholder={placeholder}
          onBlur={field.handleBlur}
        />
        <SelectFieldPopupLayer options={options} />
      </Select.Root>
      <CatalogFieldError message={error} testId={errorTestId} />
    </Field>
  );
}
