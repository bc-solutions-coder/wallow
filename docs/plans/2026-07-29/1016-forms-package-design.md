**status: active**

# Design: `@bc-solutions-coder/forms`

A new workspace package, `packages/forms`, that owns everything about building forms:
field components, validation (zod), error display, the submit pipeline, and server-error
mapping. Apps stop hand-rolling `useForm` + render-prop + presentational-field boilerplate
(the pattern currently copy-pasted across 5 forms with two different error styles) and
compose a pre-bound catalog instead.

Decisions made during brainstorming (with the user):

- **Scope:** full pipeline — fields, validation, submit shell, mutation wiring, server
  RFC 7807 error mapping, pending state.
- **Validation:** zod schemas (Standard Schema, consumed natively by TanStack Form).
- **Migration:** all five existing forms migrate as part of this work.
- **Architecture:** TanStack Form's `createFormHook` field-catalog pattern (not a
  config-driven generator).
- **Layering constraint (explicit user requirement):** forms consumes `@bc-solutions-coder/ui`;
  ui must have **zero** knowledge of forms — no imports, no types, no peer deps pointing back.

## 1. Package architecture & layering

```
packages/styles   (tokens)
      ↑
packages/ui       (Field, Input, Select, Button, ErrorBanner… — knows NOTHING about forms)
      ↑
packages/forms    (NEW — imports ui components, binds them to TanStack Form state)
      ↑
apps/wallow-auth, apps/wallow-web
```

- `@bc-solutions-coder/forms`, private, browser-only — same posture as ui.
- **Dependency direction is one-way**: forms depends on `@bc-solutions-coder/ui`
  (`workspace:*`) and consumes only ui's existing public surface (`Field`, `Field.Label`,
  `Field.Error`, `Input`, `Select`, `Checkbox`, `OtpField`, `Button`, `ErrorBanner`)
  exactly as an app would. If forms needs a look ui can't produce, the fix lands in
  ui/styles first as a normal, forms-unaware component change.
- **Deps:** `@tanstack/react-form`, `zod` (real deps); `react`, `react-dom`,
  `@tanstack/react-query` (peers — must share the app's instances).
- **Build/test:** mirrors ui — Vite lib mode, `preserveModules`, root barrel + subpath
  exports; vitest via `@bc-solutions-coder/testing` (node + browser projects, real
  Chromium, no mocking). No Storybook initially — coverage is browser-mode tests that
  render the real ui components.
- **Internal structure:**

```
packages/forms/src/
  core/        createFormHookContexts output, testid derivation, RFC 7807 mapping
  fields/      one folder per catalog field (text-field/, select-field/, …)
  form/        useAppForm, AppForm shell, SubmitButton, FormError
  index.ts     barrel
```

## 2. The authoring API

Built on TanStack Form's official extension point (`createFormHook`). A complete form:

```tsx
import { useAppForm } from "@bc-solutions-coder/forms";
import { z } from "zod";

const inquirySchema = z.object({
  name: z.string().trim().min(1, "This field is required"),
  email: z.string().trim().min(1, "This field is required"),
  company: z.string(),            // optional stays optional
  projectType: z.string().min(1, "This field is required"),
  message: z.string().trim().min(1, "This field is required"),
});

function InquiryForm() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const form = useAppForm({
    schema: inquirySchema,                          // validation + inferred value types
    defaultValues: { name: "", email: "", company: "", projectType: "", message: "" },
    mutation: inquiriesSubmitMutation({ client: sdk.client }),
    toVariables: (values) => ({ body: values }),    // this is the default; shown for clarity
    onSuccess: () => { /* sweep tags, swap to success state */ },
  });

  return (
    <form.AppForm testIdPrefix="inquiry">
      <form.AppField name="name">{(f) => <f.TextField label="Name" />}</form.AppField>
      <form.AppField name="email">{(f) => <f.TextField label="Email" />}</form.AppField>
      <form.AppField name="company">{(f) => <f.TextField label="Company" optional />}</form.AppField>
      <form.AppField name="projectType">
        {(f) => <f.SelectField label="Project type" options={PROJECT_TYPE_OPTIONS} />}
      </form.AppField>
      <form.AppField name="message">{(f) => <f.TextareaField label="Message" />}</form.AppField>
      <form.FormError />                             {/* → testid "inquiry-error" */}
      <form.SubmitButton pendingLabel="Sending...">Submit Inquiry</form.SubmitButton>
    </form.AppForm>
  );
}
```

| Piece | Owns |
| --- | --- |
| `useAppForm` | TanStack form instance + optional `useMutation` wiring; zod schema as validator; typed values |
| `form.AppForm` | The `<form>` element, `preventDefault`/`handleSubmit` boilerplate, vertical spacing, testid context |
| `form.AppField` + catalog | One field row: `Field` → `Field.Label` → control → `Field.Error`, bound to field state |
| `form.FormError` | The single form-level server-error banner (ui's `ErrorBanner`) |
| `form.SubmitButton` | ui `Button type="submit"`, auto-disabled while pending, `pendingLabel` swap |

- **Initial field catalog** (exactly what the 5 existing forms need — YAGNI beyond that):
  `TextField`, `PasswordField`, `TextareaField`, `SelectField`, `CheckboxField`,
  `OtpField`. Adding a catalog field later is one folder + one registration.
- **Testids are derived**: `testIdPrefix="inquiry"` + `name="projectType"` → control
  `inquiry-project-type`, error `inquiry-project-type-error` — the repo's existing
  `{page}-{element}` convention, enforced by construction. A per-field `testId` prop
  overrides, so migrated forms keep their E2E testids byte-identical.
- **Escape hatches** (deliberate, documented):
  - `form.AppField`'s render prop still exposes the raw TanStack field object — custom
    or one-off controls stay possible without leaving the form.
  - `useAppForm({ schema, onSubmit })` with **no** `mutation` for forms that own their
    submit semantics — this is how ForgotPassword keeps its anti-enumeration behavior
    (swallow the rejection, constant confirmation) without fighting the framework.

## 3. Errors — one look, three layers

Today's inconsistency (raw `<span className="text-destructive">` in wallow-auth vs
`ErrorBanner` in wallow-web for the same kind of error) is resolved by assigning each
error type exactly one surface:

| Layer | Source | Surface | Testid |
| --- | --- | --- | --- |
| Field validation | zod schema (client) | ui `Field.Error` — small destructive text under the control | `{prefix}-{field}-error` |
| Server field errors | RFC 7807 `errors` dict keyed by field name | Mapped onto the matching field's `Field.Error` via TanStack's `setErrorMap` | same as above |
| Form-level failure | RFC 7807 `detail` (or fallback text) | `form.FormError` → ui `ErrorBanner` above the submit | `{prefix}-error` |

The RFC 7807 mapper lives in `forms/core` (generalizing the existing per-app `errorText`
helpers). Unknown/unmatched field names in the server's `errors` dict fall back to the
form-level banner rather than vanishing.

**Visual note:** wallow-web's per-field errors visually change from banner style to
inline `Field.Error` text. Testids are preserved so E2E holds; this is the intended
unification.

## 4. Design & styling conventions ("every form feels the same")

Encoded in the components, not in a style guide people must remember:

- **Spacing:** `AppForm` owns vertical rhythm (`space-y-5`-equivalent via recipe); field
  rows own label→control→error gaps through ui's `Field` recipe. Forms never hand-place
  spacing utilities between fields.
- **Labels:** always present via `Field.Label` (auto-associated with the control — the
  `htmlFor`/`id` bookkeeping disappears). `optional` prop renders the muted "(optional)"
  affix; required is the unmarked default.
- **Error text:** one recipe (ui `Field.Error`), destructive token, appears only after
  the relevant validation event.
- **Validation timing:** standard is validate-on-submit, then revalidate-on-change for
  fields that have errored (TanStack's default cycle) — matching the current forms'
  submit-time behavior.
- **Pending:** submit disables + swaps label. No spinners in v1.
- Documented in a new `docs/development/forms.md` (added to `toc.yml`) — the
  "how to build a form" page.

## 5. Testing & migration

**Package tests** (browser-mode Chromium, real ui components):

- Each catalog field: renders label/control/error, testid derivation, error appears on
  failed submit.
- `useAppForm`: zod wiring, server-error mapping (7807 field errors + detail fallback),
  pending/disable behavior, `toVariables` default.

**Migration — all five forms**, simplest → hardest, each preserving existing testids and
passing its existing component + E2E suites unchanged:

1. `ForgotPasswordForm` (wallow-auth) — exercises the no-mutation escape hatch
2. `ResetPasswordForm` (wallow-auth)
3. `CreateOrganizationForm` (wallow-web) — the current "canonical template"; its
   replacement becomes the new canonical
4. `RegisterAppForm` (wallow-web)
5. `CreateInquiryForm` (wallow-web) — largest; selects + textarea

Existing per-form presentational components (the `TextField`/`SelectField`/`MessageField`
copies in each form file) are deleted as each form migrates. After migration, zero
old-style forms remain to be copied from.

**Out of scope (v1):** multi-step/wizard forms, arrays/dynamic field lists, file-upload
fields, Storybook for the forms package, publishing to npm.

## Addendum: Select styling fix (user-reported, folded into this work)

Two confirmed defects in the catalog `Select`, fixed in ui before the forms `SelectField`
consumes it (implementation plan Task 3b):

1. `selectPopupRecipe` has no width rule, so the open popup is narrower than its trigger;
   fix is `min-w-[var(--anchor-width)]` (Base UI publishes the trigger width on the
   Positioner).
2. Call sites pass a text glyph `▾` as `Select.Icon` children, which renders off-baseline
   and platform-dependently; fix is a default inline SVG chevron in ui's `SelectIcon`
   (children still override). Combobox/Autocomplete get the same treatment; no icon-library
   dependency is added to ui.
