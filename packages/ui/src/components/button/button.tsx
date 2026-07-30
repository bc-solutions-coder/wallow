import { Button as BaseButton } from "@base-ui/react/button";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { buttonRecipe, type ButtonRecipeProps } from "./button.styles";

/** The visual variants the shared button offers. `primary` is the default. */
export type ButtonVariant = NonNullable<ButtonRecipeProps["variant"]>;

/** The size scale the shared button offers. `md` is the default. */
export type ButtonSize = NonNullable<ButtonRecipeProps["size"]>;

/**
 * Every Base UI `Button` prop (`render`, `nativeButton`, `disabled` and the
 * native button attributes) plus the recipe's `variant`, `size`, `width` and
 * `shape`.
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes
 * the same narrowing, so a caller's `className` always means "utilities merged
 * over the recipe, last one wins".
 */
export interface ButtonProps
  extends Omit<ComponentProps<typeof BaseButton>, "className">, ButtonRecipeProps {
  readonly className?: string;
}

/**
 * The catalog's button, built on Base UI so state arrives as `data-*`
 * attributes and `render` can compose the recipe onto another element.
 */
export function Button({
  variant,
  size,
  width,
  shape,
  className,
  ...rest
}: ButtonProps): ReactElement {
  // Every recipe group is destructured, never spread: `size` and `width` are
  // real HTML attribute names, so a forgotten one lands in the markup instead
  // of in the class list.
  return (
    <BaseButton
      className={cn(buttonRecipe({ variant, size, width, shape }), className)}
      {...rest}
    />
  );
}
