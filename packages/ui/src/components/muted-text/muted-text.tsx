import type { HTMLAttributes, ReactElement } from "react";

import { cn } from "../../core/cn";
import { mutedTextRecipe } from "./muted-text.styles";

/**
 * The shared muted paragraph. Sourced from 41x `text-sm text-muted-foreground`
 * in wallow-auth — the strongest single recipe in the inventory. Renders a `<p>`;
 * children and data-testid pass through, and a caller `className` is merged over
 * the recipe.
 */
export type MutedTextProps = HTMLAttributes<HTMLParagraphElement>;

export function MutedText({ className, ...rest }: MutedTextProps): ReactElement {
  return <p className={cn(mutedTextRecipe(), className)} {...rest} />;
}
