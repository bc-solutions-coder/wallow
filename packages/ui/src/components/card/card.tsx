import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { cardRecipe, cardTitleRecipe } from "./card.styles";

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /**
   * The padding/vertical-rhythm block, overridable to cover the two measured
   * outliers (LoginScreen uses `p-6 space-y-4`, RegisterForm's first card uses a
   * bare `p-6`). Defaults to the dominant `p-6 space-y-6` recipe. It stays a
   * free-form string rather than a cva variant because call sites pass arbitrary
   * combinations, and it merges BETWEEN the recipe and `className` so a caller
   * can still override it.
   */
  readonly spacing?: string;
}

/**
 * The shared card surface. Sourced from 14x
 * `rounded-lg border border-border bg-card p-6 space-y-6` in wallow-auth. The
 * `spacing` slot swaps the padding/rhythm block; children and data-testid pass
 * through; a caller `className` is merged over both, last value winning.
 */
export function Card({ spacing = "p-6 space-y-6", className, ...rest }: CardProps): ReactElement {
  return <div className={cn(cardRecipe(), spacing, className)} {...rest} />;
}

/**
 * The card heading. Sourced from 15x `text-lg font-semibold text-card-foreground`
 * in wallow-auth; renders an `<h2>`. children and data-testid pass through; a
 * caller `className` is merged over the recipe.
 */
export type CardTitleProps = HTMLAttributes<HTMLHeadingElement>;

export function CardTitle({ className, ...rest }: CardTitleProps): ReactElement {
  return <h2 className={cn(cardTitleRecipe(), className)} {...rest} />;
}
