import { createFileRoute, redirect } from "@tanstack/react-router";
import type { ReactElement } from "react";

import { AuthLayout } from "@shared/components/auth-layout";
import { ensureSetupRequired, SetupForm } from "@features/setup";

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
  return (
    <AuthLayout>
      <SetupForm />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/setup")({
  beforeLoad: async ({ context }) => {
    const required: boolean | null = await ensureSetupRequired({
      queryClient: context.queryClient,
      client: context.sdk.client,
    });

    if (required === false) {
      throw redirect({ to: "/login" });
    }
  },
  component: SetupRoute,
});
