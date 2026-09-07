import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { badgeRecipe, type BadgeRecipeProps } from "./badge.styles";

/**
 * Span attributes and a status variant for Badge. className overrides the recipe classes.
 */
export type BadgeProps = Omit<HTMLAttributes<HTMLSpanElement>, "className"> &
  BadgeRecipeProps & {
    className?: string;
  };

/**
 * Displays a compact status label. The variant selects a matching background and foreground;
 * defaults to neutral.
 */
export function Badge({ variant, className, ...rest }: BadgeProps): ReactElement {
  return <span className={cn(badgeRecipe({ variant }), className)} {...rest} />;
}
