/**
 * The catalog's masked text field. It is `TextField`'s anatomy with the type
 * pinned to `"password"` rather than a `type` prop a caller could get wrong: a
 * password control that silently renders unmasked is the kind of defect no test
 * in a migrated screen would notice. `TextFieldType` therefore does not offer
 * `"password"` at all — this field is the only way to render a masked control.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import type { ReactElement } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

export interface PasswordFieldProps {
  /** The visible label, associated with the control by the ui `Field` row. */
  readonly label: string;
  readonly placeholder?: string;
  /** e.g. `"new-password"` on a reset screen, `"current-password"` on sign-in. */
  readonly autoComplete?: string;
  /** Overrides the derived `{testIdPrefix}-{field name}` testid and its `-error` id. */
  readonly testId?: string;
}

export function PasswordField({
  label,
  placeholder,
  autoComplete,
  testId,
}: PasswordFieldProps): ReactElement {
  const { field, pending, error, controlTestId, errorTestId } = useCatalogField<string>(testId);

  return (
    <Field invalid={error !== undefined}>
      <CatalogFieldLabel label={label} />
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
