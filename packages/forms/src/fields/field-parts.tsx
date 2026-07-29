/**
 * The three things every catalog field would otherwise repeat: the state a field
 * reads off the two contexts, the label (with its optional marker), and the
 * error message.
 *
 * They live here rather than in each field because the testid rule is the part
 * that must not drift — a form's Playwright ids are derived, and the `-error`
 * suffix has to follow an explicit `testId` override just as faithfully as it
 * follows the derivation. One implementation, five call sites.
 *
 * INTERNAL: nothing here is re-exported from `src/index.ts`. A form author
 * composes the fields, not their parts.
 */

import { Field } from "@bc-solutions-coder/ui/field";
import type { ReactElement } from "react";

import { useFieldContext } from "../core/contexts";
import { firstErrorMessage } from "../core/errors";
import { fieldErrorTestId, fieldTestId } from "../core/test-id";
import { useAppFormContext } from "../form/app-form-context";

/** Everything a catalog field needs from the field and the form shell. */
export interface CatalogFieldState<TValue> {
  /** The TanStack field this component renders under, via `AppField`. */
  readonly field: ReturnType<typeof useFieldContext<TValue>>;
  /** Whether the form's submit is in flight, so the control can disable itself. */
  readonly pending: boolean;
  /** The message to display under the control, or `undefined` when valid. */
  readonly error: string | undefined;
  /** The control's `data-testid`. */
  readonly controlTestId: string;
  /** The message's `data-testid` — always the control's plus `-error`. */
  readonly errorTestId: string;
}

/**
 * The field/shell state behind every catalog field.
 *
 * `testId` overrides BOTH ids: a migrated form that pins its control as
 * `forgot-password-email` must keep `forgot-password-email-error` too, since
 * that is the pair its E2E suite already selects.
 */
export function useCatalogField<TValue>(testId: string | undefined): CatalogFieldState<TValue> {
  const field = useFieldContext<TValue>();
  const { testIdPrefix, pending } = useAppFormContext();

  return {
    field,
    pending,
    error: firstErrorMessage(field.state.meta.errors),
    controlTestId: testId ?? fieldTestId(testIdPrefix, field.name),
    errorTestId:
      testId === undefined ? fieldErrorTestId(testIdPrefix, field.name) : `${testId}-error`,
  };
}

export interface CatalogFieldLabelProps {
  readonly label: string;
  /** Adds the `(optional)` marker, for a form where most fields are required. */
  readonly optional?: boolean;
  /**
   * Associates the label by hand. Only a field whose control cannot register
   * with the ui `Field` row needs this; every Base UI-backed control associates
   * itself, and passing an id there would override the one Base UI generated.
   */
  readonly htmlFor?: string;
}

/**
 * A catalog field's label.
 *
 * `htmlFor` is spread conditionally rather than passed as `undefined`: Base UI
 * merges an explicitly passed prop over the association it computed, so an
 * `htmlFor={undefined}` would blank out the generated one.
 */
export function CatalogFieldLabel({
  label,
  optional = false,
  htmlFor,
}: CatalogFieldLabelProps): ReactElement {
  return (
    <Field.Label {...(htmlFor === undefined ? {} : { htmlFor })}>
      {label}
      {optional ? <span className="ml-1 font-normal text-muted-foreground">(optional)</span> : null}
    </Field.Label>
  );
}

export interface CatalogFieldErrorProps {
  /** The message, or `undefined` when the field is valid. */
  readonly message: string | undefined;
  readonly testId: string;
}

/**
 * A catalog field's message.
 *
 * `match` shows Base UI's error part unconditionally, and the conditional render
 * around it is what actually decides whether there is a message — display
 * authority stays with TanStack Form's state rather than moving to Base UI's own
 * (unused) validation. The element is therefore ABSENT when the field is valid,
 * which is what the suites assert.
 */
export function CatalogFieldError({
  message,
  testId,
}: CatalogFieldErrorProps): ReactElement | null {
  if (message === undefined) {
    return null;
  }

  return (
    <Field.Error match data-testid={testId}>
      {message}
    </Field.Error>
  );
}
