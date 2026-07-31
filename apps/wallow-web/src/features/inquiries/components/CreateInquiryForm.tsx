/**
 * Create-inquiry form (Wallow-8w1h.7.3, migrated to `@bc-solutions-coder/forms`
 * in Wallow-ov6w.4.3) — one `useAppForm` call holding the zod schema, the
 * GENERATED `inquiriesSubmitMutation({ client })` and the success work, rendered
 * through the shared `AppForm` shell (see `CreateOrganizationForm`, the canonical
 * template). It submits the full `SubmitInquiryRequest`, sweeps the Inquiries tag
 * on success, swaps the form for a success state, and surfaces the server's
 * RFC 7807 failure — per-property `errors` next to the control they name,
 * anything else in the banner.
 *
 * TESTIDS. This form is the reason `AppForm` carries an explicit `testId` beside
 * `testIdPrefix`: the `<form>` is stamped `inquiry-create-form` while every
 * field, the submit and the banner use the bare `inquiry` prefix
 * (`inquiry-name`, `inquiry-submit`, `inquiry-error`), and one derivation cannot
 * produce both. The prefix therefore stays `inquiry` — which derives every field
 * id, `inquiry-submit` and `inquiry-error` correctly — and only the element's own
 * id is overridden. The ids mirror the C# E2E `InquiryPage` page object verbatim:
 * `inquiry-name`, `inquiry-email`, `inquiry-phone`, `inquiry-company`,
 * `inquiry-project-type`, `inquiry-budget-range`, `inquiry-timeline` (selects),
 * `inquiry-message` (textarea), `inquiry-submit` (button), `inquiry-success` /
 * `inquiry-error` (result states); field messages keep the `{field}-error`
 * convention. The per-field `testId` props below are redundant with the
 * derivation, and deliberately kept explicit: they are what a future field rename
 * would have to argue with.
 *
 * ERROR SURFACE. Per-field messages now render through the catalog field's
 * `Field.Error` — small destructive text under the control, `aria-describedby`'d
 * and `aria-invalid`'d onto it — rather than through a sibling `ErrorBanner` that
 * named nothing. The testids are unchanged. Only the FORM-level failure is still
 * a banner (`FormError` -> `inquiry-error`).
 *
 * Required-field validation mirrors `SubmitInquiryValidator.cs`, whose
 * `.NotEmpty()` rules cover name, email, phone, projectType, budgetRange,
 * timeline, and message. `company` is the only server-nullable field
 * (`SubmitInquiryCommand.Company` is `string?`), so it stays optional — and now
 * says so in its label instead of leaving a user to discover it by submitting.
 */
import {
  AppForm,
  FormError,
  type SelectFieldOption,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import { useQueryClient } from "@bc-solutions-coder/query";
import { Card, Text } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";
import { z } from "zod";

import { inquiriesSubmitMutation, queriesWithTag } from "../api";

/** Cosmetic option lists — display-only, not a server-side enum. */
const PROJECT_TYPE_OPTIONS: readonly SelectFieldOption[] = [
  { value: "web-app", label: "Web Application" },
  { value: "mobile-app", label: "Mobile Application" },
  { value: "api", label: "API / Backend" },
  { value: "saas", label: "SaaS Platform" },
  { value: "consulting", label: "Consulting" },
  { value: "other", label: "Other" },
];

const BUDGET_RANGE_OPTIONS: readonly SelectFieldOption[] = [
  { value: "under-5k", label: "Under $5,000" },
  { value: "5k-15k", label: "$5,000 - $15,000" },
  { value: "15k-50k", label: "$15,000 - $50,000" },
  { value: "50k-100k", label: "$50,000 - $100,000" },
  { value: "over-100k", label: "$100,000+" },
];

const TIMELINE_OPTIONS: readonly SelectFieldOption[] = [
  { value: "asap", label: "ASAP" },
  { value: "1-3-months", label: "1 - 3 months" },
  { value: "3-6-months", label: "3 - 6 months" },
  { value: "6-plus-months", label: "6+ months" },
  { value: "flexible", label: "Flexible" },
];

/**
 * The one sentence all seven required fields say. Unlike the wallow-auth forms
 * (per-field wording), this form has always used a single shared `required`
 * validator, so one shared refinement replaces it verbatim.
 */
const REQUIRED_MESSAGE = "This field is required";

/**
 * The required-field rules, carried over from the hand-written
 * `value.trim() ? undefined : "This field is required"` validator this form used
 * before the migration — including the message, which the suites assert.
 *
 * `.trim()` is what makes `"   "` fail the `min(1)`. It does NOT trim the value
 * the submit receives: TanStack's standard-schema adapter reads only the issue
 * list off a validation result and discards the parsed output, so
 * `form.state.values` stays raw — which is also what the pre-migration form
 * posted, so the payload is unchanged. `company` carries no rule at all, matching
 * the one nullable field on the command.
 */
const createInquirySchema = z.object({
  name: z.string().trim().min(1, REQUIRED_MESSAGE),
  email: z.string().trim().min(1, REQUIRED_MESSAGE),
  phone: z.string().trim().min(1, REQUIRED_MESSAGE),
  company: z.string(),
  projectType: z.string().min(1, REQUIRED_MESSAGE),
  budgetRange: z.string().min(1, REQUIRED_MESSAGE),
  timeline: z.string().min(1, REQUIRED_MESSAGE),
  message: z.string().trim().min(1, REQUIRED_MESSAGE),
});

export function CreateInquiryForm() {
  return (
    <Card spacing="p-8 space-y-6" className="shadow-sm">
      <CreateInquiryFormFields />
    </Card>
  );
}

/** The thank-you state that replaces the heading and the form once a submit lands. */
function InquirySubmittedView() {
  return (
    <div data-testid="inquiry-success" className="text-center py-6">
      <div className="text-[80px] leading-none mb-4">🐷</div>
      <Text as="h2" variant="subheading" className="mb-2">
        Thank you — your inquiry has been submitted.
      </Text>
    </div>
  );
}

/**
 * The card body: heading + form, or the success state that replaces both. Split
 * out from `CreateInquiryForm` so the `Card` surface stays a shallow wrapper and
 * the heading and form remain siblings directly under it.
 *
 * The submitted flag lives HERE, in the parent of the `useAppForm` caller, rather
 * than on the mutation: `useAppForm` owns the mutation and does not hand the
 * instance back, so the swap is gated on state captured in its `onSuccess` (the
 * same seam `RegisterAppForm` uses). Holding it in the parent is what lets the
 * thank-you REPLACE the form instead of appearing beside it — a live form under a
 * "thank you" invites a duplicate inquiry.
 */
function CreateInquiryFormFields() {
  const [submitted, setSubmitted] = useState(false);

  if (submitted) {
    return <InquirySubmittedView />;
  }

  return (
    <>
      <Text as="h2" variant="subheading" data-testid="inquiry-create-heading">
        Submit an Inquiry
      </Text>
      <InquiryFormBody
        onSubmitted={() => {
          setSubmitted(true);
        }}
      />
    </>
  );
}

/**
 * The form itself. It is its own component so the `AppForm > form.AppField >
 * field.*Field` chain stays a render root — the only shape that fits the repo's
 * JSX nesting budget.
 */
function InquiryFormBody(props: { onSubmitted: () => void }) {
  const { onSubmitted } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const form = useAppForm({
    schema: createInquirySchema,
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
    // The generated factory goes over WHOLE — `useAppForm` infers its `TError`
    // (Wallow-ov6w.2.6), so nothing here has to be destructured or cast. All
    // eight fields map 1:1 onto `SubmitInquiryRequest`, so the default
    // `toVariables` (`{ body: values }`) is the whole request contract.
    mutation: inquiriesSubmitMutation({ client: sdk.client }),
    onSuccess: () => {
      // Generated keys are flat, so there is no `['inquiries']` prefix to
      // invalidate by — the Inquiries tag predicate is the sweep.
      void queryClient.invalidateQueries(queriesWithTag("Inquiries"));
      // No `form.reset()` here: this hands the card over to the thank-you state,
      // which unmounts the form and its values with it.
      onSubmitted();
    },
    fallbackError: "Could not submit the inquiry.",
  });

  return (
    <AppForm form={form} testIdPrefix="inquiry" testId="inquiry-create-form">
      <form.AppField name="name">
        {(field) => <field.TextField label="Name" testId="inquiry-name" />}
      </form.AppField>

      <form.AppField name="email">
        {(field) => <field.TextField label="Email" testId="inquiry-email" />}
      </form.AppField>

      <form.AppField name="phone">
        {(field) => <field.TextField label="Phone" testId="inquiry-phone" />}
      </form.AppField>

      <form.AppField name="company">
        {(field) => <field.TextField label="Company" optional testId="inquiry-company" />}
      </form.AppField>

      <form.AppField name="projectType">
        {(field) => (
          <field.SelectField
            label="Project type"
            options={PROJECT_TYPE_OPTIONS}
            placeholder="Select..."
            testId="inquiry-project-type"
          />
        )}
      </form.AppField>

      <form.AppField name="budgetRange">
        {(field) => (
          <field.SelectField
            label="Budget range"
            options={BUDGET_RANGE_OPTIONS}
            placeholder="Select..."
            testId="inquiry-budget-range"
          />
        )}
      </form.AppField>

      <form.AppField name="timeline">
        {(field) => (
          <field.SelectField
            label="Timeline"
            options={TIMELINE_OPTIONS}
            placeholder="Select..."
            testId="inquiry-timeline"
          />
        )}
      </form.AppField>

      <form.AppField name="message">
        {(field) => <field.TextareaField label="Message" testId="inquiry-message" />}
      </form.AppField>

      <FormError />

      <SubmitButton>Submit Inquiry</SubmitButton>
    </AppForm>
  );
}
