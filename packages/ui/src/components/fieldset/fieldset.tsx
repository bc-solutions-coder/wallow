import { Fieldset as BaseFieldset } from "@base-ui/react/fieldset";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  fieldsetLegendRecipe,
  type FieldsetLegendRecipeProps,
  fieldsetRootRecipe,
  type FieldsetRootRecipeProps,
} from "./fieldset.styles";

/**
 * A group of related fields under a shared heading, on Base UI's `Fieldset`
 * parts. New in the Base UI rebuild — the pre-rebuild catalog had no equivalent.
 *
 * Like `Field`, `Fieldset` is both a component and its own namespace: calling it
 * renders `Fieldset.Root`, and `Fieldset.Legend` is the heading.
 *
 * Two things about Base UI's Fieldset are worth knowing before styling it, both
 * measured rather than assumed:
 *   - `Fieldset.Legend` renders a `div`, NOT a `<legend>`, and is tied to the
 *     fieldset through `aria-labelledby`. `<legend>` cannot be laid out
 *     reliably, so Base UI trades the element for the same accessible name.
 *   - `Fieldset.Root disabled` sets the native `disabled` attribute on the
 *     `<fieldset>`, which disables the controls inside it through HTML rather
 *     than through the Field context. Those controls therefore get NO
 *     `data-disabled` of their own; style them from the root's instead.
 */

/** The group box's props. Base UI's `Fieldset.Root` renders a `fieldset`. */
export interface FieldsetRootProps
  extends Omit<ComponentProps<typeof BaseFieldset.Root>, "className">, FieldsetRootRecipeProps {
  readonly className?: string;
}

/** The public name for the group box's props. */
export type FieldsetProps = FieldsetRootProps;

/** The legend's props. Base UI's `Fieldset.Legend` renders a `div`. */
export interface FieldsetLegendProps
  extends Omit<ComponentProps<typeof BaseFieldset.Legend>, "className">, FieldsetLegendRecipeProps {
  readonly className?: string;
}

function FieldsetRoot({ className, ...rest }: FieldsetRootProps): ReactElement {
  return <BaseFieldset.Root className={cn(fieldsetRootRecipe(), className)} {...rest} />;
}

function FieldsetLegend({ className, ...rest }: FieldsetLegendProps): ReactElement {
  return <BaseFieldset.Legend className={cn(fieldsetLegendRecipe(), className)} {...rest} />;
}

/** The group box, and the namespace its parts hang off. */
export const Fieldset = Object.assign(FieldsetRoot, {
  Root: FieldsetRoot,
  Legend: FieldsetLegend,
});
