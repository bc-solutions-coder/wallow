import type { HTMLAttributes, ReactElement } from "react";

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /**
   * The padding/vertical-rhythm block, overridable to cover the two measured
   * outliers (LoginScreen uses `p-6 space-y-4`, RegisterForm's first card uses a
   * bare `p-6`). Defaults to the dominant `p-6 space-y-6` recipe. Keeping this a
   * discrete slot — rather than appending a caller `className` — lets 6.5 reach
   * byte-identical rendered classes at every call site.
   */
  readonly spacing?: string;
}

/**
 * The shared card surface. Sourced from 14x
 * `rounded-lg border border-border bg-card p-6 space-y-6` in wallow-auth. The
 * `spacing` slot swaps the padding/rhythm block; children and data-testid pass
 * through; a caller `className` is appended to the recipe.
 */
export function Card({ spacing = "p-6 space-y-6", className, ...rest }: CardProps): ReactElement {
  const recipe = `rounded-lg border border-border bg-card ${spacing}`;
  const merged = className === undefined ? recipe : `${recipe} ${className}`;

  return <div className={merged} {...rest} />;
}

/**
 * The card heading. Sourced from 15x `text-lg font-semibold text-card-foreground`
 * in wallow-auth; renders an `<h2>`. children and data-testid pass through; a
 * caller `className` is appended to the recipe.
 */
export type CardTitleProps = HTMLAttributes<HTMLHeadingElement>;

export function CardTitle({ className, ...rest }: CardTitleProps): ReactElement {
  const recipe = "text-lg font-semibold text-card-foreground";
  const merged = className === undefined ? recipe : `${recipe} ${className}`;

  return <h2 className={merged} {...rest} />;
}
