/**
 * Create-inquiry form (Wallow-8w1h.7.3) — copies the CANONICAL create-form
 * template (`CreateOrganizationForm`, Wallow-8w1h.4.3): `useForm` (TanStack
 * Form) + `useMutation(createInquiryMutation(queryClient))`. It submits the
 * full `SubmitInquiryBody`, relies on the mutation factory's `onSuccess` to
 * invalidate `['inquiries']`, swaps the form for a success state on success,
 * and surfaces the server's RFC 7807 ProblemDetails `detail` when the submit
 * fails.
 *
 * Testids mirror the C# E2E `InquiryPage` page object verbatim:
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
import { Button, Card, ErrorBanner, Field, Input } from "@bc-solutions-coder/ui";
import { useForm } from "@tanstack/react-form";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { ProblemDetails } from "@bc-solutions-coder/sdk";

import { SelectControl, type SelectControlOption } from "../../../components/SelectControl";
import { createInquiryMutation } from "../api";

/**
 * The bare `textarea` has no browser default that matches the token-styled `ui`
 * `Input`, so it carries its measured recipe verbatim plus the focus ring. The
 * three selects no longer need it: since Wallow-m5aq.5.3 they are catalog
 * `Select`s, whose trigger recipe supplies the same look.
 */
const CONTROL =
  "w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring";

/** A cosmetic select option (value is the wire value; label is display text). */
type SelectOption = SelectControlOption;

/** Cosmetic option lists — display-only, not a server-side enum. */
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

/** Presentational select with the placeholder + option list. */
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
      <SelectControl
        testId={testId}
        value={value}
        options={options}
        placeholder="Select..."
        onChange={onChange}
      />
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
        className={CONTROL}
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
    <Card spacing="p-8 space-y-6" className="shadow-sm">
      <CreateInquiryFormFields />
    </Card>
  );
}

/** The eight submitted fields — also the `useForm` value shape. */
interface InquiryFormValues {
  name: string;
  email: string;
  phone: string;
  company: string;
  projectType: string;
  budgetRange: string;
  timeline: string;
  message: string;
}

/**
 * The card body: heading + form, or the success state that replaces both. Split
 * out from `CreateInquiryForm` so the `Card` surface stays a shallow wrapper and
 * the heading and form remain siblings directly under it.
 */
function CreateInquiryFormFields() {
  const queryClient = useQueryClient();
  const mutation = useMutation(createInquiryMutation(queryClient));

  if (mutation.isSuccess) {
    return (
      <div data-testid="inquiry-success" className="text-center py-6">
        <div className="text-[80px] leading-none mb-4">🐷</div>
        <h2 className="text-xl font-semibold text-foreground mb-2">
          Thank you — your inquiry has been submitted.
        </h2>
      </div>
    );
  }

  return (
    <>
      <h2 data-testid="inquiry-create-heading" className="text-xl font-semibold text-foreground">
        Submit an Inquiry
      </h2>
      <InquiryFormBody
        error={mutation.isError ? (mutation.error as ProblemDetails) : null}
        onSubmit={(value) => {
          // Fire-and-observe: drive the mutation with `mutate` (not awaited
          // `mutateAsync`) so a rejected submit is captured in mutation state and
          // surfaced below rather than escaping as an unhandled rejection. The
          // factory's own `onSuccess` invalidates `['inquiries']`; success swaps
          // heading and form for the success state above, so no field reset is
          // needed.
          mutation.mutate(value);
        }}
      />
    </>
  );
}

/**
 * The form itself. It is its own component so `<form>` stays a render root — the
 * `form > form.Field > TextField` chain only fits the repo's JSX nesting budget
 * when nothing wraps it.
 */
function InquiryFormBody(props: {
  onSubmit: (value: InquiryFormValues) => void;
  error: ProblemDetails | null;
}) {
  const { onSubmit, error } = props;

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
      onSubmit(value);
    },
  });

  return (
    <form
      data-testid="inquiry-create-form"
      className="space-y-5"
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

      {error === null ? null : (
        <ErrorBanner data-testid="inquiry-error">{error.detail}</ErrorBanner>
      )}

      <Button type="submit" data-testid="inquiry-submit">
        Submit Inquiry
      </Button>
    </form>
  );
}
