// The one entry `@bc-solutions-coder/forms` publishes, so this barrel IS the
// package's contract: a form may reach only what appears below. Ordered by
// source folder (core -> form -> fields) so it reads against `ls src`.
//
// Deliberately absent: the shared TanStack bindings (`fieldContext`,
// `formContext`, `useFieldContext`, `useFormContext`, `useTanstackAppForm`), the
// shell's own React context (`AppFormContext`, `useAppFormContext`) and the
// field-part helpers (`CatalogFieldLabel`, `CatalogFieldError`,
// `useCatalogField`, `firstErrorMessage`). Those are internal — a caller that
// re-ran `createFormHook` or read `testIdPrefix` directly would build fields
// bound to a context no `AppForm` publishes. `src/index.test.ts` pins both
// directions.

export { withForm } from "./core/form-hook";
export { splitServerError, type SplitServerError } from "./core/server-error";
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
