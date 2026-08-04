/**
 * The catalog's single-line text field — the template every other field in this
 * folder follows: a ui `Field` row (label auto-associated with the control, error
 * auto-associated with both) bound to the TanStack field it renders under.
 *
 * The control is the ui `Field.Control`, which IS the ui `Input` underneath
 * (Base UI's Input renders `Field.Control`); the two differ only in that the
 * former carries `data-[invalid]:border-destructive`, the treatment that needs a
 * `Field.Root` around it to fire. A form field is exactly where that treatment
 * belongs, so the catalog uses the field-aware part.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import type { InputHTMLAttributes, ReactElement } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

/** The input types this field offers. Masked input lives in `PasswordField`. */
export type TextFieldType = "text" | "email" | "tel" | "url";

export interface TextFieldProps {
  /** The visible label, associated with the control by the ui `Field` row. */
  readonly label: string;
  /** The native input type. Defaults to `"text"`. */
  readonly type?: TextFieldType;
  readonly placeholder?: string;
  /** Marks the field optional in its label, for a form where most fields are not. */
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
   * Overrides the derived `{testIdPrefix}-{field name}` testid (and the
   * `-error` id derived from it), so a migrated form keeps its E2E ids
   * byte-identical.
   */
  readonly testId?: string;
}

export function TextField({
  label,
  type = "text",
  placeholder,
  optional = false,
  autoComplete,
  inputMode,
  testId,
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
