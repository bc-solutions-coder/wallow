/**
 * Create-organization form (Wallow-8w1h.4.3) — the CANONICAL create-form
 * template every later vertical (Apps/Settings/MFA/Inquiries, Phases 4-6)
 * copies: `useForm` (TanStack Form) + `useMutation(createOrganizationMutation(
 * queryClient))`. It submits `{ name, domain: null }`, relies on the mutation
 * factory's `onSuccess` to invalidate `['orgs']`, resets the field on success,
 * enforces a required-name validator, and surfaces the server's RFC 7807
 * ProblemDetails `detail` when the create fails.
 *
 * Testids follow `{page}-{element}` kebab-case: `organization-name` (input),
 * `organization-create-submit` (submit button), `organization-name-error`
 * (required-field validation message), `organization-create-error` (server
 * ProblemDetails surface).
 */
import { useForm } from "@tanstack/react-form";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { ProblemDetails } from "@bc-solutions-coder/sdk";

import { createOrganizationMutation } from "../api";

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
      <input
        data-testid="organization-name"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
      {error === undefined ? null : <span data-testid="organization-name-error">{error}</span>}
    </>
  );
}

export function CreateOrganizationForm() {
  const queryClient = useQueryClient();
  const mutation = useMutation(createOrganizationMutation(queryClient));

  const form = useForm({
    defaultValues: { name: "" },
    onSubmit: ({ value }) => {
      // Fire-and-observe: the mutation captures success/error in its own state
      // (surfaced below), so we drive it with `mutate` + a per-call `onSuccess`
      // rather than awaiting `mutateAsync` — a rejected create must NOT escape
      // the submit handler as an unhandled rejection. The factory's own
      // `onSuccess` still invalidates `['orgs']`; this per-call one resets.
      mutation.mutate(
        { name: value.name, domain: null },
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
        <span data-testid="organization-create-error">
          {(mutation.error as ProblemDetails).detail}
        </span>
      ) : null}

      <button type="submit" data-testid="organization-create-submit">
        Create organization
      </button>
    </form>
  );
}
