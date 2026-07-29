import { createFormHookContexts } from "@tanstack/react-form";
import { describe, expect, it } from "vitest";

import { fieldContext, formContext, useFieldContext, useFormContext } from "./contexts";

// Acceptance-criteria guard for Wallow-ov6w.2.1 (core/contexts.ts, the shared
// TanStack Form contexts). Module-graph assertions only — nothing is rendered —
// so this spec runs in the node vitest project (.test.ts, not .test.tsx).
//
// These are the singletons `createFormHook` and every catalog field bind to. A
// field that read a DIFFERENT context would render fine and then find no field
// API at runtime, so the point of this spec is that the module re-exports
// TanStack's own contexts rather than standing up its own `createContext`.

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
