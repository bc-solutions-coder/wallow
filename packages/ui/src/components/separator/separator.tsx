import { Separator as BaseSeparator } from "@base-ui/react/separator";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import { separatorRecipe, type SeparatorRecipeProps } from "./separator.styles";

/**
 * Every Base UI `Separator` prop — `orientation` (`'horizontal'` by default),
 * `render`, and the native div attributes.
 *
 * `className` is deliberately narrowed back to `string`, as everywhere in this
 * catalog: Base UI widens it to `string | ((state) => string | undefined)` and
 * the callback form cannot be merged with a recipe through `cn()`.
 */
export interface SeparatorProps
  extends Omit<ComponentProps<typeof BaseSeparator>, "className">, SeparatorRecipeProps {
  readonly className?: string;
}

/**
 * A rule between two groups of content, announced to screen readers. This is a
 * SINGLE-part component in Base UI — there is no `Separator.Root` — so it is
 * exported as a plain component rather than a namespace object, mirroring
 * `@base-ui/react/separator` 1:1.
 *
 * Distinct from the separator parts several multi-part components ship of their
 * own (`Select.Separator`, `Toolbar.Separator`): those are different runtimes
 * with their own recipes and are not interchangeable with this one.
 */
export function Separator({ className, ...rest }: SeparatorProps): ReactElement {
  return <BaseSeparator className={cn(separatorRecipe(), className)} {...rest} />;
}
