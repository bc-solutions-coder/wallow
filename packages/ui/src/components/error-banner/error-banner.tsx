import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  errorBannerRecipe,
  errorBannerTextRecipe,
  type ErrorBannerRecipeProps,
} from "./error-banner.styles";

/**
 * Div attributes and a page or sidebar palette for an error message. Children render inside a
 * styled paragraph.
 */
export type ErrorBannerProps = HTMLAttributes<HTMLDivElement> & ErrorBannerRecipeProps;

/**
 * Renders an error message in a bordered destructive-color banner. Children belong inside a
 * paragraph; choose surface="sidebar" on a sidebar palette.
 */
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
