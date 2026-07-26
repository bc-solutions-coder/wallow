import type { HTMLAttributes, ReactElement } from "react";

/**
 * The shared danger banner. Sourced from 12x
 * `rounded-md border border-destructive bg-destructive/10 p-3` wrappers, each
 * around a `text-sm text-destructive` paragraph, in wallow-auth. The
 * data-testid stays app-owned (call sites apply e.g. `login-error` to the
 * wrapper), so it passes through onto the outer element; a caller `className` is
 * appended to the wrapper recipe.
 */
export type ErrorBannerProps = HTMLAttributes<HTMLDivElement>;

export function ErrorBanner({ className, children, ...rest }: ErrorBannerProps): ReactElement {
  const recipe = "rounded-md border border-destructive bg-destructive/10 p-3";
  const merged = className === undefined ? recipe : `${recipe} ${className}`;

  return (
    <div className={merged} {...rest}>
      <p className="text-sm text-destructive">{children}</p>
    </div>
  );
}
