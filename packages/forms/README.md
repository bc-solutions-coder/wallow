# @bc-solutions-coder/forms

TanStack Form hooks and context-bound fields using Wallow UI components. This
private workspace package provides schema validation, submission state, server
field errors, and form-level failure messages through one entry.

## Setup

Render forms under `QueryClientProvider` from `@bc-solutions-coder/query`, even
when submission uses a callback rather than an SDK mutation. Load the app's UI
utilities and theme. Tailwind must scan this package's source or built JavaScript;
it does not expose a `source.css` entry. An optional UI `FailureMessagesProvider`
customizes banner messages; without one, shipped messages are used.

```tsx
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { z } from "zod";

const schema = z.object({ name: z.string().min(1, "Name is required") });

export function NameForm({ save }: { save: (name: string) => Promise<void> }) {
  const form = useAppForm({
    schema,
    defaultValues: { name: "" },
    onSubmit: (values) => save(values.name),
  });
  return (
    <AppForm form={form} testIdPrefix="name">
      <form.AppField name="name">{(field) => <field.TextField label="Name" />}</form.AppField>
      <FormError />
      <SubmitButton pendingLabel="Saving...">Save</SubmitButton>
    </AppForm>
  );
}
```

## Exports and submission

| Exports                                                                       | Purpose                                                        |
| ----------------------------------------------------------------------------- | -------------------------------------------------------------- |
| `useAppForm`, `UseAppFormOptions`, `AppFormApi`, `WallowFormExtras`           | Schema, values, mutation or callback, and submit state         |
| `AppForm`, `AppFormProps`, `AppFormInstance`, `AppFormContextValue`           | Native form shell and its shared state contract                |
| `FormError`, `FormErrorProps`, `SubmitButton`, `SubmitButtonProps`            | Banner and pending-aware submit control                        |
| `TextField`, `PasswordField`, `TextareaField`, `SelectField`, `CheckboxField` | Registered fields, with a corresponding `*Props` type for each |
| `TextFieldType`, `SelectFieldOption`                                          | Input types and select value/label pairs                       |
| `withForm`                                                                    | Reusable form sections bound to this package's field catalog   |
| `fieldTestId`, `fieldErrorTestId`                                             | Shared control and error ID derivation                         |

Fields need both `AppForm` and `form.AppField` context; importing a field directly
does not remove those requirements. String fields hold strings, the checkbox
holds a boolean, and an empty select string means no selection. An `optional`
label marker does not alter schema validation.

A Standard Schema validator, such as Zod, runs on the first submit and on edits
afterward. Schema output transformations do not replace submitted form values.
For an API write, pass the generated `{operation}Mutation({ client })` result as
`mutation`. Variables default to `{ body: values }`; use `toVariables` to add
path parameters or reshape the body. `mutation` takes precedence over `onSubmit`.

Server errors match keys in `defaultValues`; unmatched messages remain banner
context. The hook marks its mutation handled to avoid duplicate failure reports.
`form.wallow.reset()` clears mutation and server-error state without changing
values. `form.reset()` resets form values. `AppForm` clears server errors before
retry validation; call `form.wallow.clearServerErrors()` before a direct
`form.handleSubmit()` retry.

IDs derive from `testIdPrefix` and kebab-cased field names, with `-error` appended
for messages. Each component's `testId` prop overrides its derived ID. See the
[forms guide](../../docs/development/forms.md) for the full authoring reference.
