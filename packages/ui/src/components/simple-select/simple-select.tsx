import type { ReactElement } from "react";

import { Field } from "../field/field";
import { Label } from "../label/label";
import { Select } from "../select/select";

/** SimpleSelect composes Field, Label, and Select. It translates the caller's empty-string selection to Base UI's null and displays option labels. */

/** One option: `value` travels on the wire, `label` is what a user reads. */
export interface SimpleSelectOption {
  readonly value: string;
  readonly label: string;
}

/**
 * A controlled, labeled string selection with a fixed option list. An empty value represents
 * no selection.
 */
export interface SimpleSelectProps {
  /** Names the TRIGGER — the element an E2E suite or a spec clicks. */
  readonly testId: string;
  /** The accessible name, rendered through the catalog `Label`. */
  readonly label: string;
  /** The chosen option's value; `""` means nothing is chosen. */
  readonly value: string;
  /**
   * Selectable value-label pairs. Values identify rows and must be unique.
   */
  readonly options: readonly SimpleSelectOption[];
  /**
   * Receives the selected value, or an empty string when selection clears.
   */
  readonly onChange: (value: string) => void;
  /** Shown on the trigger while nothing is chosen. */
  readonly placeholder?: string | undefined;
  /** Merged over the trigger's recipe, last value winning. */
  readonly className?: string | undefined;
}

/** One option row in the popup — `role="option"`, named by its label. */
function SimpleSelectOptionRow(props: { option: SimpleSelectOption }): ReactElement {
  return (
    <Select.Item value={props.option.value}>
      <Select.ItemText>{props.option.label}</Select.ItemText>
    </Select.Item>
  );
}

/** The listbox itself. */
function SimpleSelectList(props: { options: readonly SimpleSelectOption[] }): ReactElement {
  return (
    <Select.List>
      {props.options.map((option: SimpleSelectOption) => (
        <SimpleSelectOptionRow key={option.value} option={option} />
      ))}
    </Select.List>
  );
}

/** The popup card the list sits on. */
function SimpleSelectPopup(props: { options: readonly SimpleSelectOption[] }): ReactElement {
  return (
    <Select.Popup>
      <SimpleSelectList options={props.options} />
    </Select.Popup>
  );
}

/**
 * The portalled half. Nothing below this exists in the DOM while the select is
 * closed — Base UI mounts it on open and unmounts it on close.
 */
function SimpleSelectPopupLayer(props: { options: readonly SimpleSelectOption[] }): ReactElement {
  return (
    <Select.Portal>
      <Select.Positioner>
        <SimpleSelectPopup options={props.options} />
      </Select.Positioner>
    </Select.Portal>
  );
}

/** The closed control: the chosen label (or the placeholder) and a chevron. */
function SimpleSelectTrigger(props: {
  testId: string;
  placeholder?: string | undefined;
  className?: string | undefined;
}): ReactElement {
  return (
    <Select.Trigger data-testid={props.testId} className={props.className}>
      <Select.Value placeholder={props.placeholder} />
      <Select.Icon />
    </Select.Trigger>
  );
}

/**
 * Renders a labeled select with its trigger and popup list. Displays option labels while
 * onChange receives values, using an empty string for no selection.
 */
export function SimpleSelect({
  testId,
  label,
  value,
  options,
  onChange,
  placeholder,
  className,
}: SimpleSelectProps): ReactElement {
  return (
    <Field>
      <Label>{label}</Label>
      <Select.Root
        items={options}
        value={value === "" ? null : value}
        onValueChange={(next: string | null) => {
          onChange(next ?? "");
        }}
      >
        <SimpleSelectTrigger testId={testId} placeholder={placeholder} className={className} />
        <SimpleSelectPopupLayer options={options} />
      </Select.Root>
    </Field>
  );
}
