import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { badgeRecipe, type BadgeRecipeProps } from "./badge.styles";

/**
 * The shared status/label pill. One inline `<span>` carrying the recipe — the
 * chip six wallow-web surfaces hand-roll as a literal class string today, plus
 * the state variants that string could not express.
 *
 * `className` is narrowed back to a plain string and merged over the recipe, so
 * a caller class always wins.
 */
export type BadgeProps = Omit<HTMLAttributes<HTMLSpanElement>, "className"> &
  BadgeRecipeProps & {
    className?: string;
  };

export function Badge({ variant, className, ...rest }: BadgeProps): ReactElement {
  return <span className={cn(badgeRecipe({ variant }), className)} {...rest} />;
}
