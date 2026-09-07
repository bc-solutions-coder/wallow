/**
 * Render the form-level error supplied by AppForm.
 */

import { ErrorBanner } from "@bc-solutions-coder/ui/error-banner";
import type { ReactElement } from "react";

import { useAppFormContext } from "./app-form-context";

/**
 * Presentation overrides for the form-level error banner.
 */
export interface FormErrorProps {
  /** Overrides the derived `{testIdPrefix}-error`, e.g. `"organization-create-error"`. */
  readonly testId?: string;
  readonly className?: string;
}

/**
 * Render the AppForm serverError as an ErrorBanner, or nothing when it is null. Must be rendered
 * inside AppForm.
 */
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
