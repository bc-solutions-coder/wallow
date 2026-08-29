import {
  AppForm,
  type AppFormApi,
  FormError,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import { Button, Text } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { type ReactElement, type ReactNode, useState } from "react";
import { z } from "zod";

import { setupCreateAdminMutation } from "../api";
import { AuthScreen } from "@shared/components/auth-screen";
import { StrengthMeter } from "@shared/components/strength-meter";
import { toAppHref } from "@shared/lib/base-path";
import { passwordStrength, type PasswordStrength } from "@shared/lib/password-strength";

/**
 * The first-run setup screen: one form that creates the bootstrap administrator
 * — user, organization, and owner membership in a single
 * `POST /v1/identity/setup/admin`.
 *
 * Unlike the register screen this form rides `useAppForm`'s `mutation:` path:
 * the setup endpoint speaks RFC 7807, so `splitServerError` lands field errors
 * next to their fields and everything else (a 409 from a setup raced to
 * completion elsewhere included) in the banner — no hand-rolled rejection
 * mapping. The zod rules run for the same reason: there is no ordered
 * single-banner contract here, so per-field messages are the right shape.
 *
 * "Is setup still open?" is NOT this component's question — the `/setup`
 * route's `beforeLoad` guard redirects to `/login` when setup is already
 * complete, so by the time this renders the form is live. Success is local
 * state rather than a re-check: the answer flips to "complete" the moment the
 * admin exists, and a status read here would show the already-complete card
 * instead of the success card.
 */

const PASSWORD_MISMATCH_MESSAGE = "Passwords do not match.";
const MIN_PASSWORD_LENGTH = 8;

const setupSchema = z
  .object({
    email: z.email("Enter a valid email address"),
    password: z
      .string()
      .min(MIN_PASSWORD_LENGTH, `Password must be at least ${MIN_PASSWORD_LENGTH} characters`),
    confirmPassword: z.string(),
    firstName: z.string().trim().min(1, "First name is required"),
    lastName: z.string().trim().min(1, "Last name is required"),
    organizationName: z.string().trim().min(1, "Organization name is required"),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: PASSWORD_MISMATCH_MESSAGE,
    path: ["confirmPassword"],
  });

type SetupValues = z.infer<typeof setupSchema>;

const EMPTY_VALUES: SetupValues = {
  email: "",
  password: "",
  confirmPassword: "",
  firstName: "",
  lastName: "",
  organizationName: "",
};

/** The end state after the bootstrap admin is created. No auto-login. */
function SetupSucceeded(): ReactElement {
  return (
    <AuthScreen
      title="Setup complete"
      description="Your administrator account and organization are ready."
      headingTestId="setup-success-heading"
    >
      <Text as="p" variant="bodySm" color="muted">
        Sign in with the email and password you just created to finish configuring your platform.
      </Text>
      {/* A plain document navigation on purpose: a full load re-asks the API for
          setup status, so the login page's own gate reads the post-setup answer
          rather than this request's cached "still required". */}
      <Button
        render={<a href={toAppHref("/login")} data-testid="setup-signin-link" />}
        nativeButton={false}
        className="w-full"
      >
        Sign in
      </Button>
    </AuthScreen>
  );
}

/** The live strength rating, rendered only once the password is non-empty. */
function StrengthRow({ password }: { readonly password: string }): ReactNode {
  const strength: PasswordStrength | null = passwordStrength(password);

  return strength === null ? null : (
    <StrengthMeter strength={strength} testId="setup-password-strength" />
  );
}

/** The two-column name row, split out to stay inside the JSX depth budget. */
function NameFields({ form }: { readonly form: AppFormApi<SetupValues> }): ReactElement {
  return (
    <div className="grid grid-cols-2 gap-3">
      <form.AppField name="firstName">
        {(field) => <field.TextField label="First name" />}
      </form.AppField>
      <form.AppField name="lastName">
        {(field) => <field.TextField label="Last name" />}
      </form.AppField>
    </div>
  );
}

interface SetupFieldsProps {
  readonly onDone: () => void;
  /** See {@link SetupFormProps.seededOrganizationName}. */
  readonly seededOrganizationName: string | undefined;
}

function SetupFields({ onDone, seededOrganizationName }: SetupFieldsProps): ReactElement {
  const { sdk } = useRouteContext({ from: "__root__" });
  const organizationSeeded: boolean = seededOrganizationName !== undefined;

  const form = useAppForm({
    schema: setupSchema,
    defaultValues:
      seededOrganizationName === undefined
        ? EMPTY_VALUES
        : { ...EMPTY_VALUES, organizationName: seededOrganizationName },
    mutation: setupCreateAdminMutation({ client: sdk.client }),
    // `confirmPassword` is the form's, not the API's: the request body carries
    // only what `CreateAdminRequest` declares.
    toVariables: (values: SetupValues) => ({
      body: {
        email: values.email,
        password: values.password,
        firstName: values.firstName,
        lastName: values.lastName,
        organizationName: values.organizationName,
      },
    }),
    onSuccess: onDone,
    fallbackError: "Could not complete setup.",
  });

  return (
    <AppForm form={form} testIdPrefix="setup" className="space-y-4">
      <FormError />

      <form.AppField name="email">
        {(field) => (
          <field.TextField label="Admin email" type="email" placeholder="admin@example.com" />
        )}
      </form.AppField>

      <form.AppField name="password">
        {(field) => <field.PasswordField label="Password" placeholder="Create a password" />}
      </form.AppField>

      {/* Under `form.Subscribe` rather than the password field's render prop: a
          field only re-renders for its own value, and the meter must track it live. */}
      <form.Subscribe<string> selector={(state) => state.values.password}>
        {(password: string) => <StrengthRow password={password} />}
      </form.Subscribe>

      <form.AppField name="confirmPassword">
        {(field) => (
          <field.PasswordField label="Confirm password" placeholder="Confirm your password" />
        )}
      </form.AppField>

      <NameFields form={form} />

      {/* Stated, not asked, when the seed already created one: the dashboard
          client is bound to THAT organization, and typing a different name
          would found a sibling the new administrator is not a member of. */}
      <form.AppField name="organizationName">
        {(field) => (
          <field.TextField
            label="Organization name"
            placeholder="Acme Inc."
            readOnly={organizationSeeded}
          />
        )}
      </form.AppField>
      {organizationSeeded ? (
        <Text as="p" variant="bodySm" color="muted" data-testid="setup-organization-seeded">
          Your deployment already created this organization. Your account will become its owner.
        </Text>
      ) : null}

      <SubmitButton pendingLabel="Setting up...">Create administrator</SubmitButton>
    </AppForm>
  );
}

export interface SetupFormProps {
  /**
   * The organization the deployment seeded, when there is exactly one — the
   * `/setup` route reads it off setup status. Pre-filled and read-only, so the
   * bootstrap administrator joins it as owner instead of founding another.
   */
  readonly seededOrganizationName?: string;
}

export function SetupForm({ seededOrganizationName }: SetupFormProps = {}): ReactNode {
  const [done, setDone] = useState(false);

  if (done) {
    return <SetupSucceeded />;
  }

  return (
    <AuthScreen
      title="Set up your platform"
      description="Create the first administrator account and organization"
      headingTestId="setup-heading"
    >
      <SetupFields onDone={() => setDone(true)} seededOrganizationName={seededOrganizationName} />
    </AuthScreen>
  );
}
