/**
 * The catalog's fixed-choice field: the ui `Select`'s seven-part portal tree
 * (Root > Trigger(Value, Icon) plus Portal > Positioner > Popup > List >
 * Item(ItemText)) collapsed behind an `options` prop, inside the same `Field` row
 * every other catalog field uses.
 *
 * TWO TRANSLATIONS HAPPEN HERE, both at this boundary rather than in callers:
 *
 *   - "nothing chosen" is `""` in form state (TanStack Form's default for a
 *     required select) and `null` in Base UI.
 *   - the trigger shows the option's LABEL while the WIRE VALUE is what lands in
 *     form state — which is what `items` buys: without it Base UI's
 *     `Select.Value` renders the raw value, so a `web-app` / "Web application"
 *     pair would show the wire value to the user.
 *
 * The tree is split into one component per nesting level for the same reason
 * wallow-web's `SelectControl` is: spelled out inline it blows the repo's
 * `react/jsx-max-depth` budget.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import { Select } from "@bc-solutions-coder/ui/select";
import type { ReactElement } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

/** One choice: `value` travels on the wire, `label` is what a user reads. */
export interface SelectFieldOption {
  readonly value: string;
  readonly label: string;
}

export interface SelectFieldProps {
  /** The visible label, associated with the trigger by the ui `Field` row. */
  readonly label: string;
  readonly options: readonly SelectFieldOption[];
  /** Shown on the trigger while nothing is chosen. */
  readonly placeholder?: string;
  /** Marks the field optional in its label, for a form where most fields are not. */
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
  readonly options: readonly SelectFieldOption[];
}): ReactElement {
  return (
    <Select.Popup>
      <SelectFieldOptionList options={options} />
    </Select.Popup>
  );
}

/**
 * The portalled half of the select. Nothing below this exists in the DOM while
 * the select is closed — Base UI mounts it on open and unmounts it on close,
 * which is why a spec has to reach for it through `page` rather than the render
 * container.
 */
function SelectFieldPopupLayer({
  options,
}: {
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
