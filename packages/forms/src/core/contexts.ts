import { createFormHookContexts } from "@tanstack/react-form";

/**
 * The shared TanStack Form contexts every catalog field and the form shell hang
 * off. They are created ONCE here, at module scope: `createFormHook`
 * (core/form-hook.ts) and every field component must import these instances
 * rather than call `createFormHookContexts()` again, so a field rendered by
 * `useAppForm` always reads the field/form API the shell provided.
 *
 * Layer 0 of the package: `src/core/` imports nothing from `src/fields/` or
 * `src/form/`.
 */

export const { fieldContext, formContext, useFieldContext, useFormContext } =
  createFormHookContexts();
