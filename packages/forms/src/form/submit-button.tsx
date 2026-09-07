/**
 * Submit control that reads pending state from AppForm.
 */

import { Button } from "@bc-solutions-coder/ui/button";
import type { ReactElement, ReactNode } from "react";

import { useAppFormContext } from "./app-form-context";

/**
 * Submit label, pending label, and presentation overrides.
 */
export interface SubmitButtonProps {
  /** The button's label while the form is idle. */
  readonly children: ReactNode;
  /** The label shown instead of `children` while the form is pending, e.g. `"Sending..."`. */
  readonly pendingLabel?: ReactNode;
  /** Overrides the derived `{testIdPrefix}-submit`, e.g. `"organization-create-submit"`. */
  readonly testId?: string;
  /**
   * Disable submission in addition to the form pending state. false cannot enable the button
   * while a submission is pending.
   */
  readonly disabled?: boolean;
  readonly className?: string;
}

/**
 * Render a submit button inside AppForm. It disables while pending and uses pendingLabel when
 * supplied; otherwise children remain visible.
 */
export function SubmitButton({
  children,
  pendingLabel,
  testId,
  disabled = false,
  className,
}: SubmitButtonProps): ReactElement {
  const { testIdPrefix, pending } = useAppFormContext();

  return (
    <Button
      type="submit"
      disabled={pending || disabled}
      data-testid={testId ?? `${testIdPrefix}-submit`}
      className={className}
    >
      {/* The swap is optional; several forms disable without relabelling. */}
      {pending && pendingLabel !== undefined ? pendingLabel : children}
    </Button>
  );
}
