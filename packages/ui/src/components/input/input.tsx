import { Input as BaseInput } from "@base-ui/react/input";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { inputRecipe, type InputRecipeProps } from "./input.styles";

/**
 * Every Base UI `Input` prop (`render`, `disabled`, `onValueChange` and the
 * native input attributes) plus the recipe's variants.
 *
 * The name and shape stay compatible with the pre-rebuild
 * `InputHTMLAttributes<HTMLInputElement>` alias: the 23 `<Input>` call sites in
 * wallow-auth/wallow-web pass `id`, `type`, `placeholder`, `data-testid`,
 * `required`, `autoComplete`, `name`, and a controlled `value`/`onChange` pair,
 * all of which Base UI's Input accepts unchanged.
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes
 * the same narrowing, so a caller's `className` always means "utilities merged
 * over the recipe, last one wins".
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
