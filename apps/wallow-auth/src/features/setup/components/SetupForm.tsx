/**
 * PROTOTYPE — wayfinder ticket #106. Throwaway code answering "what does the
 * first-run setup page look like when assembled from the existing auth
 * components?" It deliberately reuses the shared surface every other auth
 * screen uses — `AuthScreen` for the card, `useAppForm` + the GENERATED
 * `setupCreateAdminMutation` for the form — so the reaction it invites is to
 * the page, not to novel plumbing.
 *
 * What it demonstrates (per the map's resolved decisions):
 *  - single form, not a wizard: email / password / first + last name /
 *    organization name in one card (#105);
 *  - the generated mutation goes through `useAppForm`'s `mutation:` path, so
 *    the API's RFC 7807 `errors` land next to their fields for free — unlike
 *    register, this endpoint speaks problem-details (#104/#107);
 *  - a self-guard on `setupGetStatus`: setup already complete → explain and
 *    point at sign-in instead of rendering a dead form (#105);
 *  - success ends at a "go sign in" card, no auto-login (#105).
 *
 * Deliberately skipped (prototype rules — the real build adds them):
 *  - the register screen's password-strength meter (it lives inside
 *    `features/register`; sharing it means promoting it to `shared/`);
 *  - confirm-password;
 *  - node specs for the guard narrowing, browser specs for the form.
 */
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useQuery } from "@bc-solutions-coder/query";
import { Button, Card, MutedText, Text } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";
import { z } from "zod";
import { setupCreateAdminMutation, setupGetStatusOptions } from "../api";
import { AuthScreen } from "@shared/components/auth-screen";
import { toAppHref } from "@shared/lib/base-path";

const setupSchema = z.object({
  email: z.string().trim().min(1, "Email is required").email("Enter a valid email address"),
  password: z.string().min(8, "Password must be at least 8 characters"),
  firstName: z.string().trim().min(1, "First name is required"),
  lastName: z.string().trim().min(1, "Last name is required"),
  organizationName: z.string().trim().min(1, "Organization name is required"),
});

function LoadingCard(): ReactNode {
  return (
    <Card spacing="p-6">
      <div className="py-6 text-center" data-testid="setup-loading">
        <MutedText>Checking setup status...</MutedText>
      </div>
    </Card>
  );
}

/** Rendered when someone lands on /setup after setup already happened. */
function AlreadyComplete(): ReactNode {
  return (
    <AuthScreen
      title="Setup is already complete"
      description="This deployment already has an administrator."
      headingTestId="setup-complete-heading"
    >
      <Button render={<a href={toAppHref("/login")} />} nativeButton={false} className="w-full">
        Go to sign in
      </Button>
    </AuthScreen>
  );
}

/** The end state after the bootstrap admin is created. No auto-login (#105). */
function SetupSucceeded(): ReactNode {
  return (
    <AuthScreen
      title="Setup complete"
      description="Your administrator account and organization are ready."
      headingTestId="setup-success-heading"
    >
      <Text as="p" variant="bodySm" color="muted">
        Sign in with the email and password you just created to finish configuring your platform.
      </Text>
      <Button render={<a href={toAppHref("/login")} />} nativeButton={false} className="w-full">
        Sign in
      </Button>
    </AuthScreen>
  );
}

function SetupFields({ onDone }: { readonly onDone: () => void }): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });

  const form = useAppForm({
    schema: setupSchema,
    defaultValues: {
      email: "",
      password: "",
      firstName: "",
      lastName: "",
      organizationName: "",
    },
    mutation: setupCreateAdminMutation({ client: sdk.client }),
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

      <div className="grid grid-cols-2 gap-3">
        <form.AppField name="firstName">
          {(field) => <field.TextField label="First name" />}
        </form.AppField>
        <form.AppField name="lastName">
          {(field) => <field.TextField label="Last name" />}
        </form.AppField>
      </div>

      <form.AppField name="organizationName">
        {(field) => <field.TextField label="Organization name" placeholder="Acme Inc." />}
      </form.AppField>

      <SubmitButton pendingLabel="Setting up...">Create administrator</SubmitButton>
    </AppForm>
  );
}

export function SetupForm(): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });
  const [done, setDone] = useState(false);

  // Self-guard: the page asks the API whether first-run setup is still open.
  // After a successful submit the answer flips to false, so `done` takes
  // precedence — the success card, not the already-complete card.
  const statusQuery = useQuery(setupGetStatusOptions({ client: sdk.client }));

  if (done) {
    return <SetupSucceeded />;
  }

  if (statusQuery.isLoading) {
    return <LoadingCard />;
  }

  if (statusQuery.data?.setupRequired === false) {
    return <AlreadyComplete />;
  }

  return (
    <AuthScreen
      title="Set up your platform"
      description="Create the first administrator account and organization"
      headingTestId="setup-heading"
    >
      <SetupFields onDone={() => setDone(true)} />
    </AuthScreen>
  );
}
