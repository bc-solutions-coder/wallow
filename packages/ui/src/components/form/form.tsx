import { Form as BaseForm } from "@base-ui/react/form";
import type {
  FormActions as BaseFormActions,
  FormProps as BaseFormProps,
  FormSubmitEventDetails as BaseFormSubmitEventDetails,
  FormValidationMode as BaseFormValidationMode,
} from "@base-ui/react/form";
import type { ReactElement, Ref } from "react";

import { cn } from "../../core/cn";
import { formRecipe, type FormRecipeProps } from "./form.styles";

/**
 * The catalog's form, on Base UI's `Form`.
 *
 * `Form` is a SINGLE part — there is no namespace and no `Form.Root`, so this
 * folder exports one component. What it adds over a native `<form>` is a
 * context the `Field`s beneath it join by `name`:
 *
 *   - server-side errors handed in through `errors` are routed to the matching
 *     field and rendered by that field's `Field.Error` with no `match` prop —
 *     the one case where an error message shows without one — and cleared again
 *     the moment the user edits that field;
 *   - a submit runs every field's validation first, and is blocked (with focus
 *     moved to the first offender) if any field fails;
 *   - `onFormSubmit` receives the field values keyed by each field's `name`,
 *     with the native default already prevented;
 *   - `validationMode` sets the default for every field, which each `Field` may
 *     still override for itself.
 *
 * `className` is deliberately narrowed back to `string`: Base UI widens it to
 * `string | ((state) => string | undefined)`, and the callback form cannot be
 * merged with a recipe through `cn()`. Every component in this catalog makes
 * the same narrowing, so a caller's `className` always means "utilities merged
 * over the recipe, last one wins".
 */

/** The shape of the `errors` prop: a message, or list of messages, per field name. */
export type FormErrors = NonNullable<BaseFormProps["errors"]>;

/** When fields validate: `onSubmit` (default), `onBlur`, or `onChange`. */
export type FormValidationMode = BaseFormValidationMode;

/** The imperative handle `actionsRef` receives — `validate()` on demand. */
export type FormActions = BaseFormActions;

/** The second argument `onFormSubmit` receives, carrying the native event. */
export type FormSubmitEventDetails = BaseFormSubmitEventDetails;

/**
 * Every Base UI `Form` prop (`errors`, `onFormSubmit`, `validationMode`,
 * `actionsRef`, `render` and the native form attributes) plus the recipe's
 * variants.
 *
 * The `FormValues` type parameter is Base UI's and is kept rather than erased:
 * it is what types the object `onFormSubmit` receives, so a caller that names
 * its fields gets the submitted values typed instead of a bare record.
 */
export interface FormProps<FormValues extends Record<string, unknown> = Record<string, unknown>>
  extends Omit<BaseFormProps<FormValues>, "className">, FormRecipeProps {
  readonly className?: string;
  /** Base UI declares the form's ref on its call signature; restated here so callers can pass one. */
  readonly ref?: Ref<HTMLFormElement>;
}

/**
 * The `FormValues` type argument is forwarded explicitly rather than left to
 * inference, which would erase it back to the default and untype the values
 * `onFormSubmit` receives.
 */
export function Form<FormValues extends Record<string, unknown> = Record<string, unknown>>({
  className,
  ...rest
}: FormProps<FormValues>): ReactElement {
  return <BaseForm<FormValues> className={cn(formRecipe(), className)} {...rest} />;
}
