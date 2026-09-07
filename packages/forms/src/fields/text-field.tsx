/**
 * Single-line string field bound to the surrounding AppField and AppForm contexts.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import type { InputHTMLAttributes, ReactElement } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

/** The input types this field offers. Masked input lives in `PasswordField`. */
export type TextFieldType = "text" | "email" | "tel" | "url";

/**
 * Labels and presentation for TextField. The field value and validation come from the surrounding
 * AppField.
 */
export interface TextFieldProps {
  /** The visible label, associated with the control by the ui `Field` row. */
  readonly label: string;
  /** The native input type. Defaults to `"text"`. */
  readonly type?: TextFieldType;
  readonly placeholder?: string;
  /** Add an optional marker to the label. Does not change schema validation. */
  readonly optional?: boolean;
  readonly autoComplete?: string;
  /**
   * The virtual keyboard a touch device should offer. Kept separate from
   * `type`, because the two are not interchangeable for a digits-only value:
   * `type="number"` would eat the leading zero of a zero-padded one-time code,
   * so such a field stays `type="text"` and asks for the keypad here instead.
   */
  readonly inputMode?: InputHTMLAttributes<HTMLInputElement>["inputMode"];
  /**
   * Override the control test ID. The error test ID uses this value plus -error; otherwise both
   * IDs derive from AppForm testIdPrefix and the field name.
   */
  readonly testId?: string;
  /**
   * Prevent user edits while retaining the value in form state and submitted variables. Schema
   * validation still applies.
   */
  readonly readOnly?: boolean;
}

/**
 * Render a single-line string input inside AppForm and a matching AppField. The control disables
 * while pending and displays the first field error.
 */
export function TextField({
  label,
  type = "text",
  placeholder,
  optional = false,
  autoComplete,
  inputMode,
  testId,
  readOnly = false,
}: TextFieldProps): ReactElement {
  const { field, pending, error, controlTestId, errorTestId } = useCatalogField<string>(testId);

  return (
    <Field invalid={error !== undefined}>
      <CatalogFieldLabel label={label} optional={optional} />
      <Field.Control
        type={type}
        placeholder={placeholder}
        autoComplete={autoComplete}
        inputMode={inputMode}
        readOnly={readOnly}
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
