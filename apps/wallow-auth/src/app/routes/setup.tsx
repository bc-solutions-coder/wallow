import { createFileRoute, redirect } from "@tanstack/react-router";
import type { ReactElement } from "react";

import { AuthLayout } from "@shared/components/auth-layout";
import { ensureSetupStatus, SetupForm, type SetupStatus } from "@features/setup";

/**
 * The `/setup` route — the first-run page where a fresh deployment's bootstrap
 * administrator is created. Production seeds no admin, so until this form is
 * submitted the API answers almost everything with a 503 and the auth app's
 * login gate points here.
 *
 * The already-complete guard lives in `beforeLoad` rather than in the
 * component so the redirect is the response itself: SSR emits a real 3xx to
 * `/login` before any markup, and a client-side navigation never paints a dead
 * form first. Only a definite "setup is complete" redirects — an unreachable
 * status endpoint renders the form and lets the submit surface the failure,
 * which the visitor can at least see and retry.
 *
 * No `validateSearch`: this page is deployment bootstrap, not a step in an
 * OIDC flow — it carries no `returnUrl` or `client_id`, and renders the fork's
 * own branding.
 */
function SetupRoute(): ReactElement {
  const { seededOrganizationName } = Route.useRouteContext();

  return (
    <AuthLayout>
      <SetupForm seededOrganizationName={seededOrganizationName} />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/setup")({
  beforeLoad: async ({ context }) => {
    const status: SetupStatus | null = await ensureSetupStatus({
      queryClient: context.queryClient,
      client: context.sdk.client,
    });

    if (status?.setupRequired === false) {
      throw redirect({ to: "/login" });
    }

    // The seeded organization rides the route context so the form states it
    // rather than asking for it: production seeds the organization the
    // dashboard client is bound to, and an administrator who founds a sibling
    // here is not a member where it counts.
    return { seededOrganizationName: status?.seededOrganizationName };
  },
  component: SetupRoute,
});
