/**
 * Create-inquiry form (Wallow-8w1h.7.3) — copies the CANONICAL create-form
 * template (`CreateOrganizationForm`, Wallow-8w1h.4.3): `useForm` (TanStack
 * Form) + `useMutation(createInquiryMutation(queryClient))`. It submits the
 * full `SubmitInquiryBody`, relies on the mutation factory's `onSuccess` to
 * invalidate `['inquiries']`, swaps the form for a success state on success,
 * and surfaces the server's RFC 7807 ProblemDetails `detail` when the submit
 * fails.
 *
 * Testids mirror the C# E2E `InquiryPage` page object + Blazor oracle
 * (`api/src/Wallow.Web/Components/Pages/Dashboard/Inquiries.razor`) verbatim:
 * `inquiry-name`, `inquiry-email`, `inquiry-phone`, `inquiry-company`,
 * `inquiry-project-type`, `inquiry-budget-range`, `inquiry-timeline` (selects),
 * `inquiry-message` (textarea), `inquiry-submit` (button), `inquiry-success` /
 * `inquiry-error` (result states). Field-validation messages use the
 * `{field}-error` convention (`inquiry-name-error`, etc.).
 *
 * Required-field validation mirrors `SubmitInquiryValidator.cs`, whose
 * `.NotEmpty()` rules cover name, email, phone, projectType, budgetRange,
 * timeline, and message. `company` is the only server-nullable field
 * (`SubmitInquiryCommand.Company` is `string?`), so it stays optional; every
 * other field blocks submit with a `{field}-error` message client-side rather
 * than letting the server reject an "apparently valid" form.
 */
import { Button, Card, ErrorBanner, Field, Input, MutedText } from "@bc-solutions-coder/ui";
import { useForm } from "@tanstack/react-form";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { ProblemDetails } from "@bc-solutions-coder/sdk";

import { createInquiryMutation } from "../api";

/** A cosmetic select option (value is the wire value; label is display text). */
interface SelectOption {
  value: string;
  label: string;
}

/** Blazor oracle option lists — cosmetic parity only, not a server-side enum. */
const PROJECT_TYPE_OPTIONS: readonly SelectOption[] = [
  { value: "web-app", label: "Web Application" },
  { value: "mobile-app", label: "Mobile Application" },
  { value: "api", label: "API / Backend" },
  { value: "saas", label: "SaaS Platform" },
  { value: "consulting", label: "Consulting" },
  { value: "other", label: "Other" },
];

const BUDGET_RANGE_OPTIONS: readonly SelectOption[] = [
  { value: "under-5k", label: "Under $5,000" },
  { value: "5k-15k", label: "$5,000 - $15,000" },
  { value: "15k-50k", label: "$15,000 - $50,000" },
  { value: "50k-100k", label: "$50,000 - $100,000" },
  { value: "over-100k", label: "$100,000+" },
];

const TIMELINE_OPTIONS: readonly SelectOption[] = [
  { value: "asap", label: "ASAP" },
  { value: "1-3-months", label: "1 - 3 months" },
  { value: "3-6-months", label: "3 - 6 months" },
  { value: "6-plus-months", label: "6+ months" },
  { value: "flexible", label: "Flexible" },
];

/**
 * Presentational text input (+ optional validation message), extracted so the
 * form's render-prop tree stays within the repo's JSX nesting budget — the same
 * pattern `CreateOrganizationForm`'s `NameField` established.
 */
function TextField(props: {
  testId: string;
  value: string;
  onChange: (value: string) => void;
  error?: string | undefined;
  errorTestId?: string | undefined;
}) {
  const { testId, value, onChange, error, errorTestId } = props;
  return (
    <>
      <Field>
        <Input
          data-testid={testId}
          value={value}
          onChange={(e) => {
            onChange(e.target.value);
          }}
        />
      </Field>
      {error === undefined || errorTestId === undefined ? null : (
        <ErrorBanner data-testid={errorTestId}>{error}</ErrorBanner>
      )}
    </>
  );
}

/** Presentational select with the Blazor oracle's placeholder + option list. */
function SelectField(props: {
  testId: string;
  value: string;
  options: readonly SelectOption[];
  onChange: (value: string) => void;
  error?: string | undefined;
  errorTestId?: string | undefined;
}) {
  const { testId, value, options, onChange, error, errorTestId } = props;
  return (
    <>
      <select
        data-testid={testId}
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      >
        <option value="">Select...</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error === undefined || errorTestId === undefined ? null : (
        <ErrorBanner data-testid={errorTestId}>{error}</ErrorBanner>
      )}
    </>
  );
}

/** Presentational message textarea (+ optional validation message). */
function MessageField(props: {
  value: string;
  error: string | undefined;
  onChange: (value: string) => void;
}) {
  const { value, error, onChange } = props;
  return (
    <>
      <textarea
        data-testid="inquiry-message"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
      {error === undefined ? null : (
        <ErrorBanner data-testid="inquiry-message-error">{error}</ErrorBanner>
      )}
    </>
  );
}

const required = ({ value }: { value: string }): string | undefined =>
  value.trim() ? undefined : "This field is required";

export function CreateInquiryForm() {
  return (
    <Card>
      <CreateInquiryFormFields />
    </Card>
  );
}

/**
 * The form body, split out so the `Card` surface stays a shallow wrapper and the
 * `form > form.Field > TextField` chain keeps within the repo's JSX nesting budget.
 */
function CreateInquiryFormFields() {
  const queryClient = useQueryClient();
  const mutation = useMutation(createInquiryMutation(queryClient));

  const form = useForm({
    defaultValues: {
      name: "",
      email: "",
      phone: "",
      company: "",
      projectType: "",
      budgetRange: "",
      timeline: "",
      message: "",
    },
    onSubmit: ({ value }) => {
      // Fire-and-observe: drive the mutation with `mutate` (not awaited
      // `mutateAsync`) so a rejected submit is captured in mutation state and
      // surfaced below rather than escaping as an unhandled rejection. The
      // factory's own `onSuccess` invalidates `['inquiries']`; success swaps the
      // form for the success card (below), so no in-place field reset is needed.
      mutation.mutate({
        name: value.name,
        email: value.email,
        phone: value.phone,
        company: value.company,
        projectType: value.projectType,
        budgetRange: value.budgetRange,
        timeline: value.timeline,
        message: value.message,
      });
    },
  });

  if (mutation.isSuccess) {
    return (
      <MutedText data-testid="inquiry-success">
        Thank you — your inquiry has been submitted.
      </MutedText>
    );
  }

  return (
    <form
      data-testid="inquiry-create-form"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        void form.handleSubmit();
      }}
    >
      <form.Field name="name" validators={{ onSubmit: required }}>
        {(field) => (
          <TextField
            testId="inquiry-name"
            value={field.state.value}
            error={field.state.meta.errors[0]}
            errorTestId="inquiry-name-error"
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="email" validators={{ onSubmit: required }}>
        {(field) => (
          <TextField
            testId="inquiry-email"
            value={field.state.value}
            error={field.state.meta.errors[0]}
            errorTestId="inquiry-email-error"
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="phone" validators={{ onSubmit: required }}>
        {(field) => (
          <TextField
            testId="inquiry-phone"
            value={field.state.value}
            error={field.state.meta.errors[0]}
            errorTestId="inquiry-phone-error"
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="company">
        {(field) => (
          <TextField
            testId="inquiry-company"
            value={field.state.value}
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="projectType" validators={{ onSubmit: required }}>
        {(field) => (
          <SelectField
            testId="inquiry-project-type"
            value={field.state.value}
            options={PROJECT_TYPE_OPTIONS}
            error={field.state.meta.errors[0]}
            errorTestId="inquiry-project-type-error"
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="budgetRange" validators={{ onSubmit: required }}>
        {(field) => (
          <SelectField
            testId="inquiry-budget-range"
            value={field.state.value}
            options={BUDGET_RANGE_OPTIONS}
            error={field.state.meta.errors[0]}
            errorTestId="inquiry-budget-range-error"
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="timeline" validators={{ onSubmit: required }}>
        {(field) => (
          <SelectField
            testId="inquiry-timeline"
            value={field.state.value}
            options={TIMELINE_OPTIONS}
            error={field.state.meta.errors[0]}
            errorTestId="inquiry-timeline-error"
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="message" validators={{ onSubmit: required }}>
        {(field) => (
          <MessageField
            value={field.state.value}
            error={field.state.meta.errors[0]}
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      {mutation.isError ? (
        <ErrorBanner data-testid="inquiry-error">
          {(mutation.error as ProblemDetails).detail}
        </ErrorBanner>
      ) : null}

      <Button type="submit" data-testid="inquiry-submit">
        Submit Inquiry
      </Button>
    </form>
  );
}
