import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { SetupForm } from "@features/setup";

/**
 * PROTOTYPE — wayfinder ticket #106. The `/setup` first-run route: renders the
 * throwaway SetupForm inside the standard auth chrome. No search params — the
 * page is a deployment bootstrap, not part of an OIDC flow, so there is no
 * client_id or returnUrl to carry.
 */
function SetupRoute() {
  return (
    <AuthLayout>
      <SetupForm />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/setup")({
  component: SetupRoute,
});
