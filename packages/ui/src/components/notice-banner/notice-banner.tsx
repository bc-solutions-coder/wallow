import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { noticeBannerRecipe, type NoticeBannerRecipeProps } from "./notice-banner.styles";

/**
 * Div attributes with a success or warning tone. Children supply their own text structure and
 * typography.
 */
export type NoticeBannerProps = HTMLAttributes<HTMLDivElement> & NoticeBannerRecipeProps;

/**
 * Renders a success or warning notice. Compose Text, headings, or actions inside it; the
 * banner adds no paragraph wrapper.
 */
export function NoticeBanner({ tone, className, ...rest }: NoticeBannerProps): ReactElement {
  // `tone` is destructured rather than spread — it is a recipe axis, not an
  // attribute, and would otherwise land on the DOM node.
  return <div className={cn(noticeBannerRecipe({ tone }), className)} {...rest} />;
}
