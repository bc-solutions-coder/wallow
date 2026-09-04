/*
 * Public-API pin for Wallow-ov6w.2.5 (public barrel + package quality gate).
 *
 * `@bc-solutions-coder/forms` publishes exactly ONE entry — `src/index.ts` — so
 * that barrel *is* the package's contract. Everything the five migrating forms
 * are allowed to reach lives on it, and everything else (the raw TanStack
 * contexts, the shell's own React context, the field-part helpers) deliberately
 * does not: a form that imports `useFieldContext` or calls `createFormHook` a
 * second time gets a parallel binding whose fields read a different context, and
 * nothing catches that at review time.
 *
 * Three surfaces are pinned here, mirroring `packages/ui/src/index.test.ts`:
 *
 *   - runtime values, asserted as an EXACT set in both directions (a dropped
 *     export fails, and so does an accidentally widened one);
 *   - types, checked at COMPILE time by `PublicTypeExports` at the bottom —
 *     `tsconfig.json` includes all of `src`, so `pnpm --filter
 *     @bc-solutions-coder/forms typecheck` fails if a `type` re-export is lost,
 *     which no runtime assertion can see;
 *   - the BUILT artifact, because `package.json`'s exports map points consumers
 *     at `dist/index.js` and `dist/index.d.ts`, not at `src/`. A barrel that
 *     compiles but emits a different surface would break every consumer while
 *     leaving the source assertions green.
 *
 * This is a pure-logic `*.test.ts`, so it runs in the vitest NODE project. It
 * only imports the module graph (no rendering), which is safe: neither the
 * catalog fields nor the ui components they wrap touch the DOM at module scope.
 */

import { describe, expect, it } from "vitest";

import * as forms from "./index";

import type {
  AppFormApi,
  AppFormContextValue,
  AppFormInstance,
  AppFormProps,
  CheckboxFieldProps,
  FormErrorProps,
  PasswordFieldProps,
  SelectFieldOption,
  SelectFieldProps,
  SplitServerError,
  SubmitButtonProps,
  TextareaFieldProps,
  TextFieldProps,
  TextFieldType,
  UseAppFormOptions,
  WallowFormExtras,
} from "./index";

/**
 * The complete runtime surface of `@bc-solutions-coder/forms`, sorted. Asserted
 * as an EXACT set, so this list grows only when a new capability is genuinely
 * meant to be public — nothing reaches the barrel by accident.
 *
 * Four groups, and no fifth:
 *   - the hook (`useAppForm`) and its higher-order companion (`withForm`);
 *   - the shell (`AppForm`) and the two children that read its context
 *     (`SubmitButton`, `FormError`);
 *   - the catalog fields, so a form can also render one outside `AppField`;
 *   - the two testid helpers plus the two DEPRECATED error readers
 *     (`splitServerError`, `errorText`), exported only until their remaining
 *     call sites move.
 */
const PUBLIC_RUNTIME_EXPORTS = [
  "AppForm",
  "CheckboxField",
  "FormError",
  "PasswordField",
  "SelectField",
  "SubmitButton",
  "TextField",
  "TextareaField",
  "errorText",
  "fieldErrorTestId",
  "fieldTestId",
  "splitServerError",
  "useAppForm",
  "withForm",
];

/**
 * Internals that must stay OFF the barrel even though they are `export`ed from
 * their own module for the package's other files to import.
 *
 * The first five are TanStack's shared bindings: `createFormHook` is called once
 * in `core/form-hook.tsx` and a consumer that reached these could call it again,
 * producing fields bound to a context no `AppForm` publishes. `AppFormContext` /
 * `useAppFormContext` are the shell's private channel — a component outside the
 * package has no business reading `testIdPrefix` directly. The rest are helpers
 * the catalog fields share; they are implementation, not contract.
 *
 * Checked explicitly rather than left to the exact-set assertion above so the
 * failure names the leak instead of dumping two long arrays.
 */
const INTERNAL_EXPORTS = [
  "AppFormContext",
  "CatalogFieldError",
  "CatalogFieldLabel",
  "fieldContext",
  "firstErrorMessage",
  "formContext",
  "useAppFormContext",
  "useCatalogField",
  "useFieldContext",
  "useFormContext",
  "useTanstackAppForm",
];

describe("@bc-solutions-coder/forms public API", () => {
  it("exports exactly the documented runtime surface", () => {
    expect(Object.keys(forms).toSorted()).toEqual(PUBLIC_RUNTIME_EXPORTS);
  });

  it("exports every public name as something callable", () => {
    // Every entry is either a component, a hook or a plain helper — all of them
    // functions. A re-export that resolved to `undefined` (a renamed source
    // symbol the barrel still names) would slip past a presence check but not
    // past this one.
    for (const name of PUBLIC_RUNTIME_EXPORTS) {
      expect(typeof (forms as Record<string, unknown>)[name], name).toBe("function");
    }
  });

  it("keeps the shared contexts and field-part helpers internal", () => {
    for (const name of INTERNAL_EXPORTS) {
      expect(forms, name).not.toHaveProperty(name);
    }
  });

  it("derives testids exactly as the catalog fields do", () => {
    // The two helpers are on the barrel so a bespoke form can keep its
    // Playwright ids byte-identical to a migrated one; pinning their VALUES here
    // makes the barrel's copy provably the same function the catalog uses.
    expect(forms.fieldTestId("inquiry", "projectType")).toBe("inquiry-project-type");
    expect(forms.fieldErrorTestId("inquiry", "projectType")).toBe("inquiry-project-type-error");
  });
});

/**
 * Compile-time-only pin of the barrel's `type` re-exports. Referencing each one
 * in a tuple makes `tsc --noEmit` fail if a `export type` line is dropped from
 * `src/index.ts`; the alias is exported so it does not read as an unused local.
 *
 * The two generic members are instantiated with a throwaway value shape — what
 * is pinned is that the re-export survives, not what the generic holds.
 */
export type PublicTypeExports = [
  AppFormApi<{ name: string }>,
  AppFormContextValue,
  AppFormInstance,
  AppFormProps,
  CheckboxFieldProps,
  FormErrorProps,
  PasswordFieldProps,
  SelectFieldOption,
  SelectFieldProps,
  SplitServerError,
  SubmitButtonProps,
  TextareaFieldProps,
  TextFieldProps,
  TextFieldType,
  UseAppFormOptions<{ name: string }, unknown, unknown>,
  WallowFormExtras,
];
