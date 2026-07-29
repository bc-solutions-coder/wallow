# Forms

`@bc-solutions-coder/forms` (`packages/forms`) is the shared form-authoring layer both frontends
write their forms with. It binds [TanStack Form](https://tanstack.com/form) state onto the
`@bc-solutions-coder/ui` catalog once, so a form is a schema, a mutation, and a list of fields —
not a re-implementation of submit plumbing.

It sits one layer above the component library and depends on it one way:

```
@bc-solutions-coder/styles  →  @bc-solutions-coder/ui  →  @bc-solutions-coder/forms  →  apps
```

`ui` knows nothing about forms and must never import this package. Like `ui`, `forms` is private
(never published) and consumed as a `workspace:*` dependency.

## Why it exists

Before it, each of the five forms in the two apps hand-rolled the same things and disagreed about
half of them: the `preventDefault` + `stopPropagation` + `void form.handleSubmit()` trio, a
per-field validator function, the `disabled={pending}` / `{pending ? "Sending..." : "Send"}` pair on
every submit button, hand-written `data-testid` strings on every control **and** its message, and
two different ways of showing a failure — some forms put per-field messages in an `ErrorBanner` that
named no field, others put them under the input. The package replaces all of it with one shell, one
hook and one catalog, and the testids it derives are byte-identical to the ones the Playwright
suites already select.

## The surface

`src/index.ts` is the only entry — there are no subpaths, and nothing else in `packages/forms/src`
is importable. Everything below comes from `@bc-solutions-coder/forms`.

| Export                                                                   | What it is                                                                                       |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| `useAppForm`                                                             | The one hook a form calls: schema + mutation + submit pipeline + RFC 7807 error split.           |
| `AppForm`                                                                | The shell: owns the `<form>` element, the submit wiring, the vertical rhythm, and the testid prefix. |
| `SubmitButton`, `FormError`                                              | The two children that read the shell's `pending` / `serverError` instead of taking them as props. |
| `TextField`, `PasswordField`, `TextareaField`, `SelectField`, `CheckboxField` | The catalog fields, reached through `form.AppField`'s render prop.                            |
| `fieldTestId`, `fieldErrorTestId`, `splitServerError`                    | The derivation and error-split helpers, so a bespoke control can match the catalog exactly.      |
| `withForm`                                                               | TanStack's higher-order form composition, bound to this package's contexts.                       |

## Authoring a form

The canonical template is
`apps/wallow-web/src/features/organizations/components/CreateOrganizationForm.tsx`. Stripped to its
shape:

```tsx
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useQueryClient } from "@tanstack/react-query";
import { useRouteContext } from "@tanstack/react-router";
import { z } from "zod";

import { organizationsCreateMutation, queriesWithTag } from "../api";

const createOrganizationSchema = z.object({
  name: z.string().trim().min(1, "Name is required"),
});

function CreateOrganizationFormFields() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const form = useAppForm({
    schema: createOrganizationSchema,
    defaultValues: { name: "" },
    // The generated factory goes over WHOLE — no destructuring, no cast.
    mutation: organizationsCreateMutation({ client: sdk.client }),
    // Only needed when the request body is not the form's values verbatim.
    toVariables: (values) => ({ body: { name: values.name, domain: null } }),
    onSuccess: () => {
      void queryClient.invalidateQueries(queriesWithTag("Organizations"));
      form.reset();
    },
    fallbackError: "Could not create the organization.",
  });

  return (
    <AppForm form={form} testIdPrefix="organization-create">
      <form.AppField name="name">
        {(field) => <field.TextField label="Name" testId="organization-name" />}
      </form.AppField>

      <FormError />

      <SubmitButton>Create organization</SubmitButton>
    </AppForm>
  );
}
```

`mutation` is the **generated** `{operation}Mutation({ client })` factory from
`@bc-solutions-coder/sdk/query` (usually re-exported through the feature's `api.ts` — see
[Frontend State](frontend-state.md)), passed whole. `useAppForm` infers its error type, so nothing
is destructured or cast.

### `useAppForm` options

| Option          | Required | What it does                                                                                                         |
| --------------- | -------- | -------------------------------------------------------------------------------------------------------------------- |
| `schema`        | yes      | A zod schema, wired as TanStack's `validators.onSubmit`. Its input type is the form's value shape.                    |
| `defaultValues` | yes      | The initial values. Its **keys are also the field names** the server-error split matches against.                     |
| `mutation`      | no       | The generated `{operation}Mutation({ client })` options object, passed whole. Omit it for the `onSubmit` escape hatch. |
| `toVariables`   | no       | Values → mutation variables. Defaults to `(values) => ({ body: values })` when `mutation` is given.                    |
| `onSubmit`      | no       | The no-mutation escape hatch (see below). Still runs through an internal mutation, so `pending` keeps working.         |
| `onSuccess`     | no       | Runs with the mutation's data: sweep the query cache, reset the form, hand a one-time secret to the parent.           |
| `fallbackError` | no       | Banner text for a failure carrying nothing usable. Defaults to `"Something went wrong. Please try again."`.           |

It returns the TanStack form instance (so `form.AppField`, `form.Field`, `form.reset` and
`form.handleSubmit` are all where a TanStack user expects them) plus a `form.wallow` member:

| `form.wallow`       | What it holds                                                                             |
| ------------------- | ------------------------------------------------------------------------------------------- |
| `pending`           | Whether the submit mutation is in flight. `AppForm` publishes it to `SubmitButton` and the fields. |
| `serverError`       | The form-level failure text `FormError` renders, or `null`.                                 |
| `reset()`           | Drops the mutation's result/error state — e.g. when a dialog reopens.                       |
| `clearServerErrors()` | Drops the last submit's banner and server field messages. `AppForm` calls it on every submit. |

### The catalog fields

Every field is reached as a member of `form.AppField`'s render-prop argument (`field.TextField`,
`field.SelectField`, …) and shares `label`, `testId`, and disabling itself while the form is
pending.

| Field           | Value type | Props beyond `label` / `testId`                                 |
| --------------- | ---------- | ---------------------------------------------------------------- |
| `TextField`     | `string`   | `type` (`"text" \| "email" \| "tel" \| "url"`), `placeholder`, `autoComplete`, `optional` |
| `PasswordField` | `string`   | `placeholder`, `autoComplete` — the type is pinned to `"password"` and cannot be widened |
| `TextareaField` | `string`   | `placeholder`, `rows`, `optional`                                |
| `SelectField`   | `string`   | `options` (`{ value, label }[]`), `placeholder`, `optional`      |
| `CheckboxField` | `boolean`  | `description`                                                    |

`optional` renders a muted `(optional)` marker after the label, which is how a form says a field is
not required instead of leaving a user to discover it by submitting.

## Testids

Testids are **derived**, never hand-written, from the shell's `testIdPrefix` plus the TanStack field
name (camelCase folded to kebab-case), so they satisfy the repo's `{page}-{element}` rule
(`.claude/rules/E2E.md`) by construction:

| Element              | Derived id                              | With `testIdPrefix="inquiry"`, field `projectType` |
| -------------------- | --------------------------------------- | --------------------------------------------------- |
| The `<form>` element | `{testIdPrefix}-form`                   | `inquiry-form`                                      |
| A field's control    | `{testIdPrefix}-{kebab field name}`     | `inquiry-project-type`                              |
| A field's message    | the control's id plus `-error`          | `inquiry-project-type-error`                        |
| `SubmitButton`       | `{testIdPrefix}-submit`                 | `inquiry-submit`                                    |
| `FormError`          | `{testIdPrefix}-error`                  | `inquiry-error`                                     |

`testIdPrefix` and `testId` are different tools, and mixing them up is the easiest way to move an id
a Playwright suite depends on:

- **`testIdPrefix` (on `AppForm`) is the derivation root.** Change it and every id in the form moves
  at once. Pick the prefix the existing field ids already share.
- **`testId` (on `AppForm`, a field, `SubmitButton`, or `FormError`) overrides one id.** On a field
  it overrides **both** ids — control and message — so `testId="organization-name"` also produces
  `organization-name-error`.

`CreateInquiryForm` is the case that needs both: its `<form>` is stamped `inquiry-create-form` while
its fields, submit and banner use the bare `inquiry` prefix, so it sets `testIdPrefix="inquiry"` and
overrides the element alone with `testId="inquiry-create-form"`.

## The error model

Three failure surfaces, two testid shapes, one path each:

| Failure                         | Route                                                                                       | Rendered as                                             |
| ------------------------------- | ------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| Client-side validation          | zod schema → TanStack's `onSubmit` validator → `field.state.meta.errors`                     | `Field.Error` under the control — `{prefix}-{field}-error` |
| Server field errors             | RFC 7807 `errors` dict → `WallowError.fieldErrors` → `splitServerError` → `form.setErrorMap({ onServer })` | the same `Field.Error`, the same testid    |
| Form-level failure              | RFC 7807 `detail`, the `fallbackError`, or a thrown `Error`'s own message → `form.wallow.serverError` | `FormError` → a ui `ErrorBanner` — `{prefix}-error` |

`splitServerError` decides which is which, and the rules are worth knowing because they are what
keeps a message from disappearing:

- The API emits property names in PascalCase (`"Name"`); the split folds only the **first**
  character, so `ProjectType` lands on `projectType` and `emailAddress` survives.
- A message keyed by a name the form does not hold **joins the banner** rather than vanishing.
- If every message landed on a field, `serverError` stays `null` and no banner renders — a banner
  there would only repeat what is already under the inputs.
- A failure that is not a `WallowError` (a network fault, say) contributes its own message if it has
  one, and `fallbackError` otherwise.

`FormError` renders nothing at all when there is no form-level error, so no empty banner reserves
space and no stale testid is left behind.

## Behaviour and styling conventions

- **The shell owns the rhythm.** `AppForm` applies `space-y-5`; a `className` **replaces** it
  wholesale rather than merging (`CreateOrganizationForm` passes `space-y-6` for its card).
- **Every field has a visible label.** There is no label-less catalog field; use `optional` to mark
  a field that is not required.
- **Validation runs on submit.** The schema is wired as `validators.onSubmit` — nothing validates on
  keystroke, so a user is not corrected mid-word.
- **Server errors are cleared on the way into a submit**, not after it. `AppForm` calls
  `clearServerErrors()` before `handleSubmit()`, because `handleSubmit` aborts on a field still
  carrying the previous submit's server error and nothing in TanStack clears an `onServer` error by
  itself.
- **Pending disables, it does not spin.** While the mutation is in flight every catalog control and
  the submit button are disabled; `SubmitButton`'s optional `pendingLabel` swaps the text
  (`<SubmitButton pendingLabel="Sending...">Send reset link</SubmitButton>`). There are no spinners.
- **The `<form>` is `noValidate`.** The schema owns validation, so the browser never double-validates
  a `type="email"` control or pops a native bubble.

## The two escape hatches

**A control the catalog does not have** stays on `form.AppField`'s render prop, which hands over the
raw TanStack field object — it is still a real form field, just not a catalog one.
`RegisterAppForm`'s scope multi-select does this:

```tsx
<form.AppField name="scopes">
  {(field) => <ScopeToggles value={field.state.value} onChange={field.handleChange} />}
</form.AppField>
```

A child that is wired to no field at all (`RegisterAppForm`'s branding subsection) is simply a plain
child of the shell.

**A form that owns its own submit semantics** omits `mutation` and passes `onSubmit`. It still gets
`pending`, the shell, and the fields; it just does not get the mutation-driven error split.
`ForgotPasswordForm` uses this to swallow every failure for anti-enumeration:

```tsx
const form = useAppForm({
  schema: forgotPasswordSchema,
  defaultValues: { email: "" },
  onSubmit: async (values): Promise<void> => {
    try {
      await accountForgotPassword({ client: sdk.client, body: { email: values.email.trim() } });
    } catch {
      // Deliberately swallowed: a failure that appears for only some addresses
      // tells the caller which addresses are real.
    }
  },
  onSuccess: onSubmitted,
});
```

`ResetPasswordForm` is the same hatch one step further: its endpoint answers failures with a bare
JSON object rather than problem details, so it keeps the banner text in its own `useState` and hands
it to the shell as an explicit `serverError` prop, which `AppForm` prefers over
`form.wallow.serverError`. `<FormError />` still derives the testid from the prefix.

Note the hatch's one trap: an early `return` out of the `onSubmit` callback still **resolves** the
internal mutation and therefore fires `onSuccess`. A form whose guards can bail early should do its
navigation at the end of the callback rather than in `onSuccess`.

## Gotchas

- **`z.string().trim()` does not trim the submitted value.** It makes `"   "` fail a `.min(1)`,
  which is the whitespace-only guard — but TanStack's standard-schema adapter reads only the issue
  list off a validation result and discards the parsed output, so `form.state.values` stays raw.
  A form that must post a trimmed value trims it itself (`values.email.trim()`).
- **Import from the package root only.** `@bc-solutions-coder/forms` publishes one entry; the shared
  TanStack contexts, the shell's React context and the field-part helpers are deliberately not on it.
  A form that reached them — or called `createFormHook` a second time — would build fields bound to a
  context no `AppForm` publishes, and nothing would catch it at review time.
- **`FormError` and `SubmitButton` must render inside `<AppForm>`.** They read the shell's context and
  throw without it rather than stamping an `undefined-submit` testid nobody notices until Playwright
  goes red.
- **`react/jsx-max-depth` is 2** (oxlint `pedantic`, and `pnpm lint` runs `--deny-warnings`). An
  `AppForm > form.AppField > field.TextField` chain is already at the budget, so a form body belongs
  in its own component rather than nested inside a `Card` in the same function — which is why every
  migrated form splits `<Feature>Form` from `<Feature>FormFields`.
- **`unicorn/catch-error-name` requires the catch parameter to be named `error`** outside test files.

## Adding a catalog field

1. Add `packages/forms/src/fields/<name>-field.tsx`, following `text-field.tsx` — a ui `Field` row
   built from `useCatalogField`, `CatalogFieldLabel` and `CatalogFieldError` so the testid derivation
   and the `testId` override behave identically to every other field.
2. Register it in `fieldComponents` in `packages/forms/src/core/form-hook.tsx`. That is the package's
   single `createFormHook` call and the only place a field becomes a member of `AppField`'s argument.
3. Export the component and its props type from `packages/forms/src/index.ts`.
4. Pin it in `packages/forms/src/index.test.ts`: add the runtime name to `PUBLIC_RUNTIME_EXPORTS`
   **and** the props type to the `PublicTypeExports` tuple at the bottom of the same file.
5. If it wraps a ui component backed by a Base UI part not already listed, append that subpath to
   `baseUiSubpaths` in `packages/forms/vitest.config.ts` — without it the browser project pre-bundles
   a second copy of React and the specs die.

`packages/forms/CLAUDE.md` holds the contributor detail: internal layering, the test model, and the
package's scripts.

## How it is tested

`pnpm --filter @bc-solutions-coder/forms test` runs the shared two-project Vitest split from
`@bc-solutions-coder/testing`: a `node` project for the pure-logic specs (the barrel pin, the testid
and error-split helpers, the on-disk scaffold guard) and a `browser` project running every field and
shell spec in real headless Chromium — with the Tailwind pipeline and the fork theme attached, since
a ui control gets its box from a recipe utility and would otherwise measure 0×0. Nothing is mocked;
`@bc-solutions-coder/ui` in particular must never be (`.claude/rules/TESTING.md`).

## See also

- [Component Library](component-library.md) — the `@bc-solutions-coder/ui` catalog these fields wrap.
- [Frontend State: TanStack Query vs. Zustand](frontend-state.md) — where the generated
  `{operation}Mutation()` a form submits through comes from, and how to sweep the cache after a write.
- [Frontend Setup](frontend-setup.md) — app bootstrap and the shared-package wiring.
