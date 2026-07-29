/**
 * The package's single `createFormHook` call.
 *
 * TanStack Form's `createFormHook` binds a set of field components to the shared
 * contexts once; the hook it returns produces form instances whose `AppField`
 * render prop exposes exactly those components (`f.TextField`, `f.SelectField`,
 * ...). Calling it a second time anywhere would produce a parallel binding whose
 * fields read a different context, so it is called HERE and nowhere else — every
 * catalog field registers itself by being added to `fieldComponents` below.
 *
 * `fieldComponents` is the catalog: every field listed below becomes a member of
 * the `AppField` render prop's argument (`f.TextField`, `f.SelectField`, ...),
 * and a new field is registered by being added here.
 *
 * This is the ONE file in `src/core/` that imports from `src/fields/` — the
 * registration point has to live beside the `createFormHook` call it feeds. The
 * dependency runs one way (fields import `core/contexts` and
 * `form/app-form-context`, never `core/form-hook`), so no cycle forms.
 */

import { createFormHook } from "@tanstack/react-form";

import { CheckboxField } from "../fields/checkbox-field";
import { PasswordField } from "../fields/password-field";
import { SelectField } from "../fields/select-field";
import { TextField } from "../fields/text-field";
import { TextareaField } from "../fields/textarea-field";
import { fieldContext, formContext } from "./contexts";

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
