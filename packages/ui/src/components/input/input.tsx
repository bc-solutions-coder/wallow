import { Input as BaseInput } from "@base-ui/react/input";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { inputRecipe, type InputRecipeProps } from "./input.styles";

/**
 * Base UI input attributes, including controlled value, onValueChange, and render composition.
 * className accepts a string merged over the input recipe.
 */
export interface InputProps
  extends Omit<ComponentProps<typeof BaseInput>, "className">, InputRecipeProps {
  readonly className?: string;
}

/**
 * The catalog's text input, built on Base UI so state arrives as `data-*`
 * attributes and `render` can compose the recipe onto another element.
 */
export function Input({ className, ...rest }: InputProps): ReactElement {
  return <BaseInput className={cn(inputRecipe(), className)} {...rest} />;
}
