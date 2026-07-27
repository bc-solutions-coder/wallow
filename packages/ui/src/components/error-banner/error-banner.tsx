import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { errorBannerRecipe, errorBannerTextRecipe } from "./error-banner.styles";

/**
 * The shared danger banner. Sourced from 12x
 * `rounded-md border border-destructive bg-destructive/10 p-3` wrappers, each
 * around a `text-sm text-destructive` paragraph, in wallow-auth. The
 * data-testid stays app-owned (call sites apply e.g. `login-error` to the
 * wrapper), so it passes through onto the outer element; a caller `className` is
 * merged over the wrapper recipe and never reaches the inner paragraph.
 */
export type ErrorBannerProps = HTMLAttributes<HTMLDivElement>;

export function ErrorBanner({ className, children, ...rest }: ErrorBannerProps): ReactElement {
  return (
    <div className={cn(errorBannerRecipe(), className)} {...rest}>
      <p className={errorBannerTextRecipe()}>{children}</p>
    </div>
  );
}
