import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { cardRecipe, cardTitleRecipe } from "./card.styles";

/**
 * Div attributes for a bordered card, with an optional replacement spacing class list.
 */
export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /**
   * Replacement padding and vertical-spacing utilities. Defaults to p-6 space-y-6 and merges
   * before className.
   */
  readonly spacing?: string;
}

/**
 * Renders a bordered card with default padding and vertical spacing. spacing replaces that
 * spacing block; className overrides the result.
 */
export function Card({ spacing = "p-6 space-y-6", className, ...rest }: CardProps): ReactElement {
  return <div className={cn(cardRecipe(), spacing, className)} {...rest} />;
}

/**
 * Heading attributes for the card's h2 title.
 */
export type CardTitleProps = HTMLAttributes<HTMLHeadingElement>;

/**
 * Renders an h2 using the card title scale and foreground color.
 */
export function CardTitle({ className, ...rest }: CardTitleProps): ReactElement {
  return <h2 className={cn(cardTitleRecipe(), className)} {...rest} />;
}
