/**
 * The form's submit control. It reads the shell's `pending` state rather than
 * taking it as a prop, so a migrated form drops the `disabled={pending}` +
 * `{pending ? "Sending..." : "Send"}` pair every hand-written submit repeats.
 */

import { Button } from "@bc-solutions-coder/ui/button";
import type { ReactElement, ReactNode } from "react";

import { useAppFormContext } from "./app-form-context";

export interface SubmitButtonProps {
  /** The button's label while the form is idle. */
  readonly children: ReactNode;
  /** The label shown instead of `children` while the form is pending, e.g. `"Sending..."`. */
  readonly pendingLabel?: ReactNode;
  /** Overrides the derived `{testIdPrefix}-submit`, e.g. `"organization-create-submit"`. */
  readonly testId?: string;
  readonly className?: string;
}

export function SubmitButton({
  children,
  pendingLabel,
  testId,
  className,
}: SubmitButtonProps): ReactElement {
  const { testIdPrefix, pending } = useAppFormContext();

  return (
    <Button
      type="submit"
      disabled={pending}
      data-testid={testId ?? `${testIdPrefix}-submit`}
      className={className}
    >
      {/* The swap is optional; several forms disable without relabelling. */}
      {pending && pendingLabel !== undefined ? pendingLabel : children}
    </Button>
  );
}
