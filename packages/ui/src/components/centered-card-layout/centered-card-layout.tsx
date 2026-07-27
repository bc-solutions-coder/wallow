import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  centeredCardLayoutColumnRecipe,
  centeredCardLayoutViewportRecipe,
} from "./centered-card-layout.styles";

/**
 * The centred fixed-width column shell generalized from wallow-auth's
 * `auth-layout.tsx` (`AuthCard`/`AuthLayout`): an outer viewport wrapper around
 * an inner fixed-width column. children, data-testid and `className` pass
 * through onto the inner column; the viewport wrapper stays sealed.
 */
export type CenteredCardLayoutProps = HTMLAttributes<HTMLDivElement>;

export function CenteredCardLayout({ className, ...rest }: CenteredCardLayoutProps): ReactElement {
  return (
    <div className={centeredCardLayoutViewportRecipe()}>
      <div {...rest} className={cn(centeredCardLayoutColumnRecipe(), className)} />
    </div>
  );
}
