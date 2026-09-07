import type { AnchorHTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { quietLinkRecipe } from "./quiet-link.styles";

/**
 * Native anchor attributes for a muted supporting link, including href, target, and rel.
 */
export type QuietLinkProps = AnchorHTMLAttributes<HTMLAnchorElement>;

/**
 * Renders a muted native anchor for secondary navigation, including cross-origin destinations.
 * className overrides the link recipe.
 */
export function QuietLink({ className, ...rest }: QuietLinkProps): ReactElement {
  return <a className={cn(quietLinkRecipe(), className)} {...rest} />;
}
