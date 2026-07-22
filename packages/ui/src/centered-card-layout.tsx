import type { HTMLAttributes, ReactElement } from "react";

/**
 * The centred fixed-width column shell generalized from wallow-auth's
 * `auth-layout.tsx` (`AuthCard`/`AuthLayout`): an outer
 * `min-h-screen bg-background flex flex-col items-center justify-center px-4`
 * viewport wrapper around an inner `w-full max-w-[420px]` column. children and
 * data-testid pass through onto the inner column.
 */
export type CenteredCardLayoutProps = HTMLAttributes<HTMLDivElement>;

export function CenteredCardLayout(props: CenteredCardLayoutProps): ReactElement {
  return (
    <div className="min-h-screen bg-background flex flex-col items-center justify-center px-4">
      <div {...props} className="w-full max-w-[420px]" />
    </div>
  );
}
