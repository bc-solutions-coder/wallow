import type { ReactElement, TextareaHTMLAttributes } from "react";

import { cn } from "../../core/cn";
import { textareaRecipe, type TextareaRecipeProps } from "./textarea.styles";

/**
 * Every native `<textarea>` attribute plus the recipe's variants.
 *
 * `className` is declared explicitly as `string` so the catalog-wide contract
 * holds here too: a caller's `className` always means "utilities merged over the
 * recipe, last one wins".
 */
export interface TextareaProps
  extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "className">, TextareaRecipeProps {
  readonly className?: string;
}

/**
 * The catalog's multi-line text control. Base UI ships no textarea part (its
 * `Input` renders an `<input>`), so this wraps the native element directly.
 *
 * With no Base UI part behind it nothing else would write the `data-disabled`
 * attribute this catalog styles state off, leaving the recipe's
 * `data-[disabled]:opacity-50` dead — so the component stamps it itself. The
 * stamp sits before the passthrough spread, leaving a caller free to override it
 * like any other native attribute.
 */
export function Textarea({ className, disabled, ...rest }: TextareaProps): ReactElement {
  return (
    <textarea
      className={cn(textareaRecipe(), className)}
      disabled={disabled}
      data-disabled={disabled ? "" : undefined}
      {...rest}
    />
  );
}
