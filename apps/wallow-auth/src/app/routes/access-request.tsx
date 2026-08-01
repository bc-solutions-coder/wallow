import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { AccessRequestPage } from "@features/access-request";

/**
 * The `/access-request` route.
 *
 * The authorize endpoint redirects here when enrollment recorded a pending membership under an
 * organization's `RequestApproval` policy. It takes no search parameters — everything the
 * screen says is true of every pending request, and a redirect target that reads query text
 * would be one more attacker-constructible surface for no gain.
 */
function AccessRequestRoute() {
  return (
    <AuthLayout>
      <AccessRequestPage />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/access-request")({
  component: AccessRequestRoute,
});
