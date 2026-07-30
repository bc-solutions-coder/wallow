/**
 * The catalog's boolean field: a ui `Checkbox` with its label to the right,
 * inside the same `Field` row every other catalog field uses so its message
 * renders in the same place.
 *
 * The box and its label sit in a `Field.Item`, Base UI's horizontal grouping for
 * a control that reads BESIDE its label rather than under it. `Field.Item` also
 * scopes the association, so the label names the box even though the row's other
 * parts (the message) stay outside it.
 */

import { Checkbox } from "@bc-solutions-coder/ui/checkbox";
import { Field } from "@bc-solutions-coder/ui/field";
import type { ReactElement } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

export interface CheckboxFieldProps {
  /** The visible label, sitting to the right of the box and naming it. */
  readonly label: string;
  /** Secondary text under the label, e.g. what agreeing actually means. */
  readonly description?: string;
  /**
   * Overrides the derived `{testIdPrefix}-{field name}` testid (and its `-error`
   * id). It names the BOX — the element every suite clicks.
   */
  readonly testId?: string;
}

/**
 * The tick mark, as inline SVG.
 *
 * This package ships no icon library and must not gain one, and the text glyph
 * the ui stories use sits off the baseline of the `size-4` box and renders
 * differently on every platform — the same reasoning behind the ui `Select`'s
 * own chevron. `currentColor` lets the indicator recipe keep driving the colour.
 */
function CheckTick(): ReactElement {
  return (
    <svg
      className="size-3"
      fill="none"
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="3"
      viewBox="0 0 24 24"
    >
      <path d="m5 13 4 4 10-10" />
    </svg>
  );
}

/** The label, and the optional secondary line under it, to the right of the box. */
function CheckboxFieldText({
  label,
  description,
}: {
  readonly label: string;
  readonly description: string | undefined;
}): ReactElement {
  return (
    <div className="space-y-1">
      <CatalogFieldLabel label={label} />
      {description === undefined ? null : <Field.Description>{description}</Field.Description>}
    </div>
  );
}

/**
 * The box itself, one nesting level down from the row.
 *
 * Its own component for the same reason `SelectField`'s popup tree is split
 * into one component per level: `react/jsx-max-depth` is 2 and `pnpm lint` runs
 * `--deny-warnings`, so `Field > Field.Item > Checkbox.Root > Checkbox.Indicator`
 * cannot be written as one tree.
 */
function CheckboxFieldBox({
  checked,
  disabled,
  testId,
  onCheckedChange,
  onBlur,
}: {
  readonly checked: boolean;
  readonly disabled: boolean;
  readonly testId: string;
  readonly onCheckedChange: (checked: boolean) => void;
  readonly onBlur: () => void;
}): ReactElement {
  return (
    <Checkbox.Root
      checked={checked}
      disabled={disabled}
      data-testid={testId}
      onCheckedChange={onCheckedChange}
      onBlur={onBlur}
    >
      <Checkbox.Indicator>
        <CheckTick />
      </Checkbox.Indicator>
    </Checkbox.Root>
  );
}

export function CheckboxField({ label, description, testId }: CheckboxFieldProps): ReactElement {
  const { field, pending, error, controlTestId, errorTestId } = useCatalogField<boolean>(testId);

  return (
    <Field invalid={error !== undefined}>
      <Field.Item>
        <CheckboxFieldBox
          checked={field.state.value}
          disabled={pending}
          testId={controlTestId}
          onCheckedChange={(checked: boolean) => {
            field.handleChange(checked);
          }}
          onBlur={field.handleBlur}
        />
        <CheckboxFieldText label={label} description={description} />
      </Field.Item>
      <CatalogFieldError message={error} testId={errorTestId} />
    </Field>
  );
}
