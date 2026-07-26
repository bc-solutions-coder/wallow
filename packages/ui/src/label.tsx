import type { LabelHTMLAttributes, ReactElement } from "react";

/**
 * The shared field label. Sourced from the 22x `text-sm font-medium text-foreground`
 * recipe in wallow-auth. `htmlFor`, children, and data-testid pass through; a
 * caller `className` is appended to the recipe.
 */
export type LabelProps = LabelHTMLAttributes<HTMLLabelElement>;

export function Label({ className, ...rest }: LabelProps): ReactElement {
  const recipe = "text-sm font-medium text-foreground";
  const merged = className === undefined ? recipe : `${recipe} ${className}`;

  return <label className={merged} {...rest} />;
}
