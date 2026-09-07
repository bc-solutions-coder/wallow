/**
 * Multiline string field composed through Field.Control so the label, invalid state, and error
 * description stay associated with the textarea.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import { Textarea } from "@bc-solutions-coder/ui/textarea";
import type { ReactElement } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

/**
 * Labels and presentation for TextareaField. The field value and validation come from the
 * surrounding AppField.
 */
export interface TextareaFieldProps {
  /** The visible label, associated with the control by the ui `Field` row. */
  readonly label: string;
  readonly placeholder?: string;
  /** The control's visible height in lines, forwarded to the native attribute. */
  readonly rows?: number;
  /** Add an optional marker to the label. Does not change schema validation. */
  readonly optional?: boolean;
  /** Overrides the derived `{testIdPrefix}-{field name}` testid and its `-error` id. */
  readonly testId?: string;
}

/**
 * Render a multiline string input inside AppForm and a matching AppField. The control disables
 * while pending and displays the first field error.
 */
export function TextareaField({
  label,
  placeholder,
  rows,
  optional = false,
  testId,
}: TextareaFieldProps): ReactElement {
  const { field, pending, error, controlTestId, errorTestId } = useCatalogField<string>(testId);

  return (
    <Field invalid={error !== undefined}>
      <CatalogFieldLabel label={label} optional={optional} />
      <Field.Control
        render={<Textarea rows={rows} placeholder={placeholder} />}
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
