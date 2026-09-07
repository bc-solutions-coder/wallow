import { Fieldset as BaseFieldset } from "@base-ui/react/fieldset";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  fieldsetLegendRecipe,
  type FieldsetLegendRecipeProps,
  fieldsetRootRecipe,
  type FieldsetRootRecipeProps,
} from "./fieldset.styles";

/** Fieldset is both a callable root component and a namespace containing Root and Legend. */

/** The group box's props. Base UI's `Fieldset.Root` renders a `fieldset`. */
export interface FieldsetRootProps
  extends Omit<ComponentProps<typeof BaseFieldset.Root>, "className">, FieldsetRootRecipeProps {
  readonly className?: string;
}

/**
 * Props shared by Fieldset and Fieldset.Root; both render the same fieldset container.
 */
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

/**
 * A named group of related fields. Fieldset itself renders Fieldset.Root; Legend supplies the
 * group's accessible name.
 */
export const Fieldset = Object.assign(FieldsetRoot, {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: FieldsetRoot,
  /**
   * Provides the fieldset's accessible group name.
   */
  Legend: FieldsetLegend,
});
