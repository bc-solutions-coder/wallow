import { createFormHookContexts } from "@tanstack/react-form";

/**
 * Shared TanStack contexts used by the registered fields. All field bindings must import these
 * instances.
 */

export const { fieldContext, formContext, useFieldContext, useFormContext } =
  createFormHookContexts();
