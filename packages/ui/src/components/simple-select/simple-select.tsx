import type { ReactElement } from "react";

import { Field } from "../field/field";
import { Label } from "../label/label";
import { Select } from "../select/select";

/**
 * The whole select in one component: a labelled trigger over a fixed list of
 * options, for the call sites that need exactly the same seven-part portal tree
 * (Root > Trigger > Value/Icon, plus Portal > Positioner > Popup > List > Item >
 * ItemText) rather than a bespoke arrangement of it.
 *
 * `Select` stays the composable API — reach for it when a call site needs
 * groups, separators, an arrow, or a trigger that is not a `Field`. This is the
 * default shape, and it lives in the catalog rather than in an app because every
 * app needs the identical tree, and spelling it out per call site also blows the
 * repo's `react/jsx-max-depth` budget at each of them. That budget is why the
 * parts below are split into one component per nesting level.
 *
 * TWO TRANSLATIONS HAPPEN HERE, both at this boundary rather than in callers:
 *
 *   - "nothing chosen" is `""` on the caller's side (TanStack Form's default for
 *     a required select) and `null` in Base UI.
 *   - the trigger reports the LABEL rather than the value, which is what `items`
 *     buys: without it Base UI's `Select.Value` renders the raw value, so a
 *     `web-app` / "Web Application" pair would show the wire value to the user.
 *
 * `label` is REQUIRED: the trigger is a `role="combobox"` button with no text
 * content of its own (only the chosen option's label, which is empty until
 * something is picked), so without an explicit name a screen reader announces it
 * as unlabelled. `Field`/`Label` wrap the trigger the same way every other form
 * control in the catalog is named.
 */

/** One option: `value` travels on the wire, `label` is what a user reads. */
export interface SimpleSelectOption {
  readonly value: string;
  readonly label: string;
}

export interface SimpleSelectProps {
  /** Names the TRIGGER — the element an E2E suite or a spec clicks. */
  readonly testId: string;
  /** The accessible name, rendered through the catalog `Label`. */
  readonly label: string;
  /** The chosen option's value; `""` means nothing is chosen. */
  readonly value: string;
  readonly options: readonly SimpleSelectOption[];
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
