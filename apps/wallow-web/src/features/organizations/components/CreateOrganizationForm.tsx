/**
 * Create-organization form (Wallow-8w1h.4.3, migrated to
 * `@bc-solutions-coder/forms` in Wallow-ov6w.4.1) — the CANONICAL create-form
 * template every other wallow-web form copies: one `useAppForm` call holding the
 * zod schema, the GENERATED `organizationsCreateMutation({ client })`, and the
 * success work, rendered through the shared `AppForm` shell.
 *
 * It submits `{ name, domain: null }` (hence `toVariables` — the form has one
 * field but the request body has two), sweeps the Organizations tag on success
 * (generated keys are flat, so there is no `['orgs']` prefix to invalidate by),
 * resets the field, enforces a required-name rule, and surfaces the server's
 * RFC 7807 failure — per-property `errors` next to the input, anything else in
 * the banner.
 *
 * Testids follow `{page}-{element}` kebab-case and are DERIVED from the shell's
 * `testIdPrefix` — `organization-create-form`, `organization-create-error`,
 * `organization-create-submit` — with ONE exception: the name field predates the
 * convention and is `organization-name`, not `organization-create-name`, so it
 * carries an explicit `testId` (which the catalog also suffixes for its message,
 * `organization-name-error`). Every id here is selected by
 * `e2e-cross-app/login-journey.spec.ts`; none may drift.
 */
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { Card, CardTitle } from "@bc-solutions-coder/ui";
import { useQueryClient } from "@tanstack/react-query";
import { useRouteContext } from "@tanstack/react-router";
import { z } from "zod";

import { organizationsCreateMutation, queriesWithTag } from "../api";

/**
 * The required-name rule, carried over verbatim from the hand-written
 * `value.trim() ? undefined : "Name is required"` validator this form used
 * before the migration — including its message, which the suites assert.
 *
 * `.trim()` is what makes `"   "` fail the `min(1)`. It does NOT trim the value
 * the submit receives: TanStack's standard-schema adapter reads only the issue
 * list off a validation result and discards the parsed output, so
 * `form.state.values` stays raw — which is also what the pre-migration form
 * posted, so the payload is unchanged.
 */
const createOrganizationSchema = z.object({
  name: z.string().trim().min(1, "Name is required"),
});

export function CreateOrganizationForm() {
  // A form keeps the ui `Card` (only tables need the raw div). The card owns the
  // top margin because this form has exactly one mount site: below the list on
  // the organizations index page.
  return (
    <Card className="mt-8">
      <CardTitle data-testid="organization-create-heading">Create Organization</CardTitle>
      <CreateOrganizationFormFields />
    </Card>
  );
}

/**
 * The form body, split out so the `Card` surface stays a shallow wrapper.
 */
function CreateOrganizationFormFields() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const form = useAppForm({
    schema: createOrganizationSchema,
    defaultValues: { name: "" },
    // The generated factory goes over WHOLE — `useAppForm` infers its `TError`
    // (Wallow-ov6w.2.6), so nothing here has to be destructured or cast.
    mutation: organizationsCreateMutation({ client: sdk.client }),
    // The generated mutation takes the whole request options object, and the
    // create body carries a `domain` the form has no field for. Without this the
    // default `{ body: values }` would post `{ name }` alone.
    toVariables: (values) => ({ body: { name: values.name, domain: null } }),
    onSuccess: () => {
      void queryClient.invalidateQueries(queriesWithTag("Organizations"));
      // TanStack's own `reset` (the form's values), NOT `form.wallow.reset`
      // (the mutation's result state). Clearing the field is the signal the
      // cross-app login journey reads to prove the create actually landed.
      // Closing over `form` is safe: `onSuccess` only ever runs after a render.
      form.reset();
    },
    fallbackError: "Could not create the organization.",
  });

  return (
    // `space-y-6`, not the shell's default `space-y-5` — this card's rhythm is
    // pinned by `CreateOrganizationForm.restyle.test.tsx`.
    <AppForm form={form} testIdPrefix="organization-create" className="space-y-6">
      <form.AppField name="name">
        {(field) => <field.TextField label="Name" testId="organization-name" />}
      </form.AppField>

      <FormError />

      <SubmitButton>Create organization</SubmitButton>
    </AppForm>
  );
}
