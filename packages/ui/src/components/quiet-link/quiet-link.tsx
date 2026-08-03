import type { AnchorHTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { quietLinkRecipe } from "./quiet-link.styles";

export type QuietLinkProps = AnchorHTMLAttributes<HTMLAnchorElement>;

/**
 * The muted secondary link: card footers, "Forgot password?", back-links.
 *
 * A plain `<a>` rather than a `render`-composed part, because all 13 sourced
 * call sites are plain anchors — wallow-auth navigates across origins with real
 * hrefs, not router links. A caller `className` merges over the recipe, last
 * value winning, which is how the two layout outliers (`block text-center`,
 * `inline-block ... mb-4`) keep their positioning.
 *
 * Distinct from `Button variant="link"`, which is the primary-coloured,
 * underlined stand-in for an ACTION. This one recedes.
 */
export function QuietLink({ className, ...rest }: QuietLinkProps): ReactElement {
  return <a className={cn(quietLinkRecipe(), className)} {...rest} />;
}
