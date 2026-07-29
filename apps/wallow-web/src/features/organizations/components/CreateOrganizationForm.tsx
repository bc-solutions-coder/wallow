/**
 * Create-organization form (Wallow-8w1h.4.3) — the CANONICAL create-form
 * template every later vertical (Apps/Settings/MFA/Inquiries, Phases 4-6)
 * copies: `useForm` (TanStack Form) + `useMutation(organizationsCreateMutation({
 * client }))`. It submits `{ name, domain: null }`, sweeps the Organizations tag
 * on success (generated keys are flat, so there is no `['orgs']` prefix to
 * invalidate by), resets the field, enforces a required-name validator, and
 * surfaces the server's RFC 7807 `detail` when the create fails.
 *
 * Testids follow `{page}-{element}` kebab-case: `organization-name` (input),
 * `organization-create-submit` (submit button), `organization-name-error`
 * (required-field validation message), `organization-create-error` (server
 * ProblemDetails surface).
 */
import { Button, Card, CardTitle, ErrorBanner, Field, Input } from "@bc-solutions-coder/ui";
import { useForm } from "@tanstack/react-form";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouteContext } from "@tanstack/react-router";

import { errorText } from "../../../lib/error-text";
import { organizationsCreateMutation, queriesWithTag } from "../api";

/**
 * Presentational name field, extracted so the form's render-prop tree stays
 * within the repo's JSX nesting budget. `error` is the first validator message
 * (undefined when the field is valid).
 */
function NameField(props: {
  value: string;
  error: string | undefined;
  onChange: (value: string) => void;
}) {
  const { value, error, onChange } = props;
  return (
    <>
      <Field>
        <Input
          data-testid="organization-name"
          value={value}
          onChange={(e) => {
            onChange(e.target.value);
          }}
        />
      </Field>
      {error === undefined ? null : (
        <ErrorBanner data-testid="organization-name-error">{error}</ErrorBanner>
      )}
    </>
  );
}

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
 * The form body, split out so the `Card` surface stays a shallow wrapper and the
 * `form > form.Field > NameField` chain keeps within the repo's JSX nesting budget.
 */
function CreateOrganizationFormFields() {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  const mutation = useMutation({
    ...organizationsCreateMutation({ client: sdk.client }),
    onSuccess: (): void => {
      void queryClient.invalidateQueries(queriesWithTag("Organizations"));
    },
  });

  const form = useForm({
    defaultValues: { name: "" },
    onSubmit: ({ value }) => {
      // Fire-and-observe: the mutation captures success/error in its own state
      // (surfaced below), so we drive it with `mutate` + a per-call `onSuccess`
      // rather than awaiting `mutateAsync` — a rejected create must NOT escape
      // the submit handler as an unhandled rejection. The mutation's own
      // `onSuccess` still sweeps the tag; this per-call one resets.
      mutation.mutate(
        { body: { name: value.name, domain: null } },
        {
          onSuccess: () => {
            form.reset();
          },
        },
      );
    },
  });

  return (
    <form
      data-testid="organization-create-form"
      className="space-y-6"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        void form.handleSubmit();
      }}
    >
      <form.Field
        name="name"
        validators={{
          onSubmit: ({ value }) => (value.trim() ? undefined : "Name is required"),
        }}
      >
        {(field) => (
          <NameField
            value={field.state.value}
            error={field.state.meta.errors[0]}
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      {mutation.isError ? (
        <ErrorBanner data-testid="organization-create-error">
          {errorText(mutation.error, "Could not create the organization.")}
        </ErrorBanner>
      ) : null}

      <Button type="submit" data-testid="organization-create-submit">
        Create organization
      </Button>
    </form>
  );
}
