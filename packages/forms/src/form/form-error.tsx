/**
 * The form-level (non-field) server error banner. It reads the shell's
 * `serverError` rather than taking it as a prop, so a migrated form drops the
 * `{error === null ? null : <ErrorBanner data-testid="...">{error}</ErrorBanner>}`
 * ternary every hand-written form repeats.
 */

import { ErrorBanner } from "@bc-solutions-coder/ui/error-banner";
import type { ReactElement } from "react";

import { useAppFormContext } from "./app-form-context";

export interface FormErrorProps {
  /** Overrides the derived `{testIdPrefix}-error`, e.g. `"organization-create-error"`. */
  readonly testId?: string;
  readonly className?: string;
}

export function FormError({ testId, className }: FormErrorProps): ReactElement | null {
  const { testIdPrefix, serverError } = useAppFormContext();

  // Nothing is rendered without an error, so no empty banner reserves space and
  // no stale testid is left behind once the error clears.
  if (serverError === null) {
    return null;
  }

  return (
    <ErrorBanner data-testid={testId ?? `${testIdPrefix}-error`} className={className}>
      {serverError}
    </ErrorBanner>
  );
}
