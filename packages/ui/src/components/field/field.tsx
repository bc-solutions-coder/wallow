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

/** Field and Field.Root share the same container. Its parts associate labels, descriptions, and errors with the control through Base UI context. */

export interface FieldRootProps
  extends Omit<ComponentProps<typeof BaseField.Root>, "className">, FieldRootRecipeProps {
  readonly className?: string;
}

/**
 * Props shared by Field and Field.Root; both render the same field container.
 */
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
 * A field container with associated Label, Control, Description, Error, Item, and Validity
 * parts. Field itself renders Field.Root. A name connects the field to Form values and server
 * errors.
 */
export const Field = Object.assign(FieldRoot, {
  /**
   * Owns the component state and provides context to its parts.
   */
  Root: FieldRoot,
  /**
   * Provides the control's accessible label.
   */
  Label: FieldLabel,
  /**
   * The interactive control connected to Root state.
   */
  Control: FieldControl,
  /**
   * Provides supporting text associated with the control or popup.
   */
  Description: FieldDescription,
  /**
   * Displays the field's matching validation or server error.
   */
  Error: FieldError,
  /**
   * Groups one item and its associated content.
   */
  Item: FieldItem,
  /**
   * Exposes field validation state to a render-prop child.
   */
  Validity: FieldValidity,
});
