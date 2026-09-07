/**
 * Public form hooks, shell components, field catalog, and test ID helpers. Internal contexts and
 * registration hooks are not exported.
 */

export {
  /** Build a reusable form section bound to the parent form and this package's registered fields. */
  withForm,
} from "./core/form-hook";
export { fieldErrorTestId, fieldTestId } from "./core/test-id";
export { AppForm, type AppFormInstance, type AppFormProps } from "./form/app-form";
export type { AppFormContextValue } from "./form/app-form-context";
export { FormError, type FormErrorProps } from "./form/form-error";
export { SubmitButton, type SubmitButtonProps } from "./form/submit-button";
export {
  type AppFormApi,
  useAppForm,
  type UseAppFormOptions,
  type WallowFormExtras,
} from "./form/use-app-form";
export { CheckboxField, type CheckboxFieldProps } from "./fields/checkbox-field";
export { PasswordField, type PasswordFieldProps } from "./fields/password-field";
export { SelectField, type SelectFieldOption, type SelectFieldProps } from "./fields/select-field";
export { TextField, type TextFieldProps, type TextFieldType } from "./fields/text-field";
export { TextareaField, type TextareaFieldProps } from "./fields/textarea-field";
