import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { textRecipe, type TextRecipeProps } from "./text.styles";

/**
 * The elements Text can render. `as` chooses the ELEMENT and, on its own, the
 * default type scale; an explicit `variant` overrides that default, so
 * `<Text as="h2" variant="body">` decouples heading level from visual weight.
 */
export type TextAs =
  | "h1"
  | "h2"
  | "h3"
  | "h4"
  | "h5"
  | "h6"
  | "p"
  | "span"
  | "div"
  | "label"
  | "legend"
  | "code";

/**
 * The type scale each element derives when the caller supplies no `variant`, so
 * the common case is `<Text as="h2">` and nothing else. A plain lookup table:
 * the semantic level of the element and the visual scale it reads at are the
 * same decision until a caller says otherwise.
 */
const AS_DEFAULT_VARIANT: Record<TextAs, NonNullable<TextRecipeProps["variant"]>> = {
  h1: "display",
  h2: "title",
  h3: "heading",
  h4: "subheading",
  h5: "body",
  h6: "caption",
  p: "body",
  span: "body",
  div: "body",
  label: "body",
  legend: "caption",
  code: "code",
};

/**
 * The single text primitive: the element (`as`), the type scale (`variant`), the
 * semantic colour (`color`), plus optional `weight` and `align` overrides.
 * `className` is narrowed back to a plain string and merged over the recipe, so
 * a caller class always wins.
 */
export type TextProps = Omit<HTMLAttributes<HTMLElement>, "className"> &
  TextRecipeProps & {
    as?: TextAs;
    className?: string;
  };

export function Text({
  as = "p",
  variant,
  color,
  weight,
  align,
  className,
  ...rest
}: TextProps): ReactElement {
  const Element = as;

  return (
    <Element
      className={cn(
        textRecipe({ variant: variant ?? AS_DEFAULT_VARIANT[as], color, weight, align }),
        className,
      )}
      {...rest}
    />
  );
}
