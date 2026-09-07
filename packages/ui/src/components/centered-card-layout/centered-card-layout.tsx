import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  centeredCardLayoutColumnRecipe,
  centeredCardLayoutViewportRecipe,
} from "./centered-card-layout.styles";

/**
 * Attributes applied to the inner centered column, including children and className. The outer
 * viewport wrapper has no prop overrides.
 */
export type CenteredCardLayoutProps = HTMLAttributes<HTMLDivElement>;

/**
 * Centers a constrained column in a full-height page. Pass card content as children; className
 * and other div attributes apply to the inner column.
 */
export function CenteredCardLayout({ className, ...rest }: CenteredCardLayoutProps): ReactElement {
  return (
    <div className={centeredCardLayoutViewportRecipe()}>
      <div {...rest} className={cn(centeredCardLayoutColumnRecipe(), className)} />
    </div>
  );
}
