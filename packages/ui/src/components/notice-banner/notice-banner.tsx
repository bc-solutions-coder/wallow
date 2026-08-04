import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { noticeBannerRecipe, type NoticeBannerRecipeProps } from "./notice-banner.styles";

/**
 * The shared non-destructive banner — a confirmation or a nudge. Sourced from
 * six hand-rolled wrappers across wallow-auth that each rebuilt `ErrorBanner`'s
 * shape in a different tone (five `border-success bg-success/10`, one
 * `border-warning bg-warning/10`). The data-testid stays app-owned, so it passes
 * through onto the wrapper, and a caller `className` is merged over the recipe.
 *
 * Unlike `ErrorBanner` this does NOT wrap its children in a styled `<p>`: a
 * notice body ranges from one sentence to a heading plus an action link, so the
 * caller composes `Text` inside it.
 */
export type NoticeBannerProps = HTMLAttributes<HTMLDivElement> & NoticeBannerRecipeProps;

export function NoticeBanner({ tone, className, ...rest }: NoticeBannerProps): ReactElement {
  // `tone` is destructured rather than spread — it is a recipe axis, not an
  // attribute, and would otherwise land on the DOM node.
  return <div className={cn(noticeBannerRecipe({ tone }), className)} {...rest} />;
}
