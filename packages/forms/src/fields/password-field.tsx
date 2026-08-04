/**
 * The catalog's masked text field. It is `TextField`'s anatomy with the type
 * pinned to `"password"` rather than a `type` prop a caller could get wrong: a
 * password control that silently renders unmasked is the kind of defect no test
 * in a migrated screen would notice. `TextFieldType` therefore does not offer
 * `"password"` at all — this field is the only way to render a masked control.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import type { ReactElement, ReactNode } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

export interface PasswordFieldProps {
  /** The visible label, associated with the control by the ui `Field` row. */
  readonly label: string;
  readonly placeholder?: string;
  /** e.g. `"new-password"` on a reset screen, `"current-password"` on sign-in. */
  readonly autoComplete?: string;
  /**
   * An affordance that shares the label's line — the sign-in screen's "Forgot
   * password?" link, which sits opposite the label rather than under the control.
   *
   * BESIDE the label, never inside it: a label names its control, so an anchor
   * folded into one would both make "Forgot password?" part of the field's
   * accessible name and put a navigation target inside the box's own click area.
   * That is also why it is not simply a `ReactNode` label.
   */
  readonly labelAction?: ReactNode;
  /** Overrides the derived `{testIdPrefix}-{field name}` testid and its `-error` id. */
  readonly testId?: string;
}

/**
 * The label line when something shares it. Its own component because
 * `react/jsx-max-depth` is 2 here, and the row would otherwise put the label one
 * level deeper than the rule allows.
 */
function LabelRow({ label, action }: { readonly label: string; readonly action: ReactNode }) {
  return (
    <div className="flex items-center justify-between">
      <CatalogFieldLabel label={label} />
      {action}
    </div>
  );
}

export function PasswordField({
  label,
  placeholder,
  autoComplete,
  labelAction,
  testId,
}: PasswordFieldProps): ReactElement {
  const { field, pending, error, controlTestId, errorTestId } = useCatalogField<string>(testId);

  return (
    <Field invalid={error !== undefined}>
      {labelAction === undefined ? (
        <CatalogFieldLabel label={label} />
      ) : (
        <LabelRow label={label} action={labelAction} />
      )}
      <Field.Control
        type="password"
        placeholder={placeholder}
        autoComplete={autoComplete}
        disabled={pending}
        data-testid={controlTestId}
        value={field.state.value}
        onValueChange={(value: string) => {
          field.handleChange(value);
        }}
        onBlur={field.handleBlur}
      />
      <CatalogFieldError message={error} testId={errorTestId} />
    </Field>
  );
}
