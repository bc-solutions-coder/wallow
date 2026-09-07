import { createFormHookContexts } from "@tanstack/react-form";
import { describe, expect, it } from "vitest";

import { fieldContext, formContext, useFieldContext, useFormContext } from "./contexts";

// Verify that catalog bindings share the TanStack contexts.

describe("core/contexts", () => {
  it("exports the field and form contexts with their hooks", () => {
    expect(fieldContext).toBeDefined();
    expect(formContext).toBeDefined();
    expect(typeof useFieldContext).toBe("function");
    expect(typeof useFormContext).toBe("function");
  });

  it("exports React contexts, not bare objects", () => {
    expect(fieldContext).toHaveProperty("Provider");
    expect(formContext).toHaveProperty("Provider");
    expect(fieldContext).not.toBe(formContext);
  });

  it("uses TanStack Form's own context instances", () => {
    // `createFormHookContexts()` hands back contexts created at react-form's
    // module scope, so the framework's copy is the identity to match: anything
    // hand-rolled here would be a different context object and would silently
    // detach every field from the form the shell provides.
    const tanstack = createFormHookContexts();

    expect(fieldContext).toBe(tanstack.fieldContext);
    expect(formContext).toBe(tanstack.formContext);
  });
});
