import { Field as BaseField } from "@base-ui/react/field";
import type { ComponentProps, ReactElement } from "react";

import { cn } from "../../core/cn";
import {
  fieldControlRecipe,
  type FieldControlRecipeProps,
  fieldDescriptionRecipe,
  type FieldDescriptionRecipeProps,
  fieldErrorRecipe,
  type FieldErrorRecipeProps,
  fieldItemRecipe,
  type FieldItemRecipeProps,
  fieldLabelRecipe,
  type FieldLabelRecipeProps,
  fieldRootRecipe,
  type FieldRootRecipeProps,
} from "./field.styles";

/**
 * The Field anatomy, on Base UI's `Field` parts.
 *
 * `Field` is BOTH the field row component and the namespace holding the parts:
 * calling it renders `Field.Root`, so the 22 pre-rebuild `<Field>` call sites
 * keep working untouched, while `Field.Label` / `Field.Control` /
 * `Field.Description` / `Field.Error` / `Field.Item` / `Field.Validity` expose
 * the rest of Base UI's anatomy under the names Base UI itself uses.
 *
 * What the rebuild buys, and the pre-rebuild `<div className="space-y-2">`
 * could not do: the root publishes the field's state to every part beneath it
 * as `data-*` attributes, and it associates the label with the control (and the
 * description and error message with both) so callers no longer have to keep an
 * `htmlFor`/`id` pair in sync by hand.
 *
 * Every part narrows `className` back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. The whole catalog makes this narrowing,
 * so a caller's `className` always means "utilities merged over the recipe,
 * last one wins".
 */

/**
 * The field row's props. Base UI's `Field.Root` renders a `div`, so this stays
 * assignable from the pre-rebuild `HTMLAttributes<HTMLDivElement>` alias every
 * call site was written against, and adds the field-level controls Base UI
 * brings (`name`, `disabled`, `invalid`, `validate`, `validationMode`).
 */
export interface FieldRootProps
  extends Omit<ComponentProps<typeof BaseField.Root>, "className">, FieldRootRecipeProps {
  readonly className?: string;
}

/** The pre-rebuild export name for the field row's props, kept for compat. */
export type FieldProps = FieldRootProps;

/** The label's props. Base UI's `Field.Label` renders a `label`. */
export interface FieldLabelProps
  extends Omit<ComponentProps<typeof BaseField.Label>, "className">, FieldLabelRecipeProps {
  readonly className?: string;
}

/** The control's props. Base UI's `Field.Control` renders an `input`. */
export interface FieldControlProps
  extends Omit<ComponentProps<typeof BaseField.Control>, "className">, FieldControlRecipeProps {
  readonly className?: string;
}

/** The description's props. Base UI's `Field.Description` renders a `p`. */
export interface FieldDescriptionProps
  extends
    Omit<ComponentProps<typeof BaseField.Description>, "className">,
    FieldDescriptionRecipeProps {
  readonly className?: string;
}

/**
 * The error message's props. Base UI's `Field.Error` renders a `div`, and
 * renders NOTHING unless its `match` prop is satisfied — `match` alone always
 * shows it, `match="valueMissing"` (or any other `ValidityState` key) shows it
 * for that failure, and the default shows it once the field fails validation.
 */
export interface FieldErrorProps
  extends Omit<ComponentProps<typeof BaseField.Error>, "className">, FieldErrorRecipeProps {
  readonly className?: string;
}

/** The item's props. Base UI's `Field.Item` renders a `div`. */
export interface FieldItemProps
  extends Omit<ComponentProps<typeof BaseField.Item>, "className">, FieldItemRecipeProps {
  readonly className?: string;
}

/** The validity render-prop part's props — unstyled, so it takes no recipe. */
export type FieldValidityProps = ComponentProps<typeof BaseField.Validity>;

function FieldRoot({ className, ...rest }: FieldRootProps): ReactElement {
  return <BaseField.Root className={cn(fieldRootRecipe(), className)} {...rest} />;
}

function FieldLabel({ className, ...rest }: FieldLabelProps): ReactElement {
  return <BaseField.Label className={cn(fieldLabelRecipe(), className)} {...rest} />;
}

function FieldControl({ className, ...rest }: FieldControlProps): ReactElement {
  return <BaseField.Control className={cn(fieldControlRecipe(), className)} {...rest} />;
}

function FieldDescription({ className, ...rest }: FieldDescriptionProps): ReactElement {
  return <BaseField.Description className={cn(fieldDescriptionRecipe(), className)} {...rest} />;
}

function FieldError({ className, ...rest }: FieldErrorProps): ReactElement {
  return <BaseField.Error className={cn(fieldErrorRecipe(), className)} {...rest} />;
}

function FieldItem({ className, ...rest }: FieldItemProps): ReactElement {
  return <BaseField.Item className={cn(fieldItemRecipe(), className)} {...rest} />;
}

/** Unstyled: it renders whatever its children callback returns, and no element. */
function FieldValidity(props: FieldValidityProps): ReactElement {
  return <BaseField.Validity {...props} />;
}

/**
 * The field row, and the namespace its parts hang off. `Field` and `Field.Root`
 * are the same component on purpose — the former is the compat name, the latter
 * the Base UI part name — so a fork can write either.
 */
export const Field = Object.assign(FieldRoot, {
  Root: FieldRoot,
  Label: FieldLabel,
  Control: FieldControl,
  Description: FieldDescription,
  Error: FieldError,
  Item: FieldItem,
  Validity: FieldValidity,
});
