/**
 * Register the field catalog against the shared contexts. Keep this registration in one place;
 * fields import contexts rather than this module to avoid a dependency cycle.
 */

import { createFormHook } from "@tanstack/react-form";

import { CheckboxField } from "../fields/checkbox-field";
import { PasswordField } from "../fields/password-field";
import { SelectField } from "../fields/select-field";
import { TextField } from "../fields/text-field";
import { TextareaField } from "../fields/textarea-field";
import { fieldContext, formContext } from "./contexts";

/**
 * Create forms and reusable form sections using the same registered field catalog. The public
 * withForm helper binds a render component to a compatible parent form; it does not create a
 * separate form instance.
 */
export const { useAppForm: useTanstackAppForm, withForm } = createFormHook({
  fieldContext,
  formContext,
  fieldComponents: {
    CheckboxField,
    PasswordField,
    SelectField,
    TextField,
    TextareaField,
  },
  formComponents: {},
});
