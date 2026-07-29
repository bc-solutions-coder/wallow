/**
 * The catalog's multi-line text field — `TextField`'s anatomy over the ui
 * `Textarea` control.
 *
 * The `Textarea` is substituted INTO `Field.Control` through Base UI's `render`
 * prop rather than rendered beside it. Base UI ships no textarea part, so the ui
 * `Textarea` is a bare native element: standing it next to the label would leave
 * the row with no control to associate, forcing this field to hand-maintain an
 * `htmlFor`/`id` pair — the exact chore the `Field` row exists to remove. Going
 * through `Field.Control` also gives the textarea the row's `data-invalid` and
 * `aria-describedby` wiring for free.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import { Textarea } from "@bc-solutions-coder/ui/textarea";
import type { ReactElement } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

export interface TextareaFieldProps {
  /** The visible label, associated with the control by the ui `Field` row. */
  readonly label: string;
  readonly placeholder?: string;
  /** The control's visible height in lines, forwarded to the native attribute. */
  readonly rows?: number;
  /** Marks the field optional in its label, for a form where most fields are not. */
  readonly optional?: boolean;
  /** Overrides the derived `{testIdPrefix}-{field name}` testid and its `-error` id. */
  readonly testId?: string;
}

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
