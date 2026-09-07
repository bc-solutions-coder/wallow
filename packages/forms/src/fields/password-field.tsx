/**
 * Masked string field bound to the surrounding AppField and AppForm contexts.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import type { ReactElement, ReactNode } from "react";

import { CatalogFieldError, CatalogFieldLabel, useCatalogField } from "./field-parts";

/**
 * Labels and presentation for PasswordField. The field value and validation come from the
 * surrounding AppField.
 */
export interface PasswordFieldProps {
  /** The visible label, associated with the control by the ui `Field` row. */
  readonly label: string;
  readonly placeholder?: string;
  /** e.g. `"new-password"` on a reset screen, `"current-password"` on sign-in. */
  readonly autoComplete?: string;
  /**
   * Render an action, such as a password-reset link, beside the label. The action is outside the
   * label and does not name the input.
   */
  readonly labelAction?: ReactNode;
  /** Overrides the derived `{testIdPrefix}-{field name}` testid and its `-error` id. */
  readonly testId?: string;
}

/**
 * Keep the label action beside the label while limiting JSX nesting.
 */
function LabelRow({ label, action }: { readonly label: string; readonly action: ReactNode }) {
  return (
    <div className="flex items-center justify-between">
      <CatalogFieldLabel label={label} />
      {action}
    </div>
  );
}

/**
 * Render a masked string input inside AppForm and a matching AppField. The control disables while
 * pending; labelAction appears beside the label without becoming part of its accessible name.
 */
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
