import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  errorBannerRecipe,
  errorBannerTextRecipe,
  type ErrorBannerRecipeProps,
} from "./error-banner.styles";

/**
 * The shared danger banner. Sourced from 12x
 * `rounded-md border border-destructive bg-destructive/10 p-3` wrappers, each
 * around a `text-sm text-destructive` paragraph, in wallow-auth. The
 * data-testid stays app-owned (call sites apply e.g. `login-error` to the
 * wrapper), so it passes through onto the outer element; a caller `className` is
 * merged over the wrapper recipe and never reaches the inner paragraph.
 */
export type ErrorBannerProps = HTMLAttributes<HTMLDivElement> & ErrorBannerRecipeProps;

export function ErrorBanner({
  surface,
  className,
  children,
  ...rest
}: ErrorBannerProps): ReactElement {
  // `surface` reaches BOTH recipes: the tint and the message colour are two
  // halves of one decision, and a banner whose fill moved to the rail while its
  // text stayed on the page palette is the illegible state, not a partial fix.
  // Destructured rather than spread — it is a recipe axis, not an attribute.
  return (
    <div className={cn(errorBannerRecipe({ surface }), className)} {...rest}>
      <p className={errorBannerTextRecipe({ surface })}>{children}</p>
    </div>
  );
}
