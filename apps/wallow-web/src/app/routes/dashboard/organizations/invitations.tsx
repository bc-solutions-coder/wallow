import { PageContainer, PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import {
  INVITATIONS_QUERY,
  invitationsGetByTenantOptions,
  InvitationList,
} from "@features/organizations";

/**
 * The outstanding-invitations route (Wallow-yp3e.3.3). Deliberately NOT nested
 * under `organizations/$orgId/`: `InvitationsController.GetByTenant` reads the
 * AMBIENT tenant with no organization-id parameter, so there is no orgId to
 * scope this route by — passing one would compile and silently do nothing.
 *
 * The `loader` prefetches the invitations list via
 * `context.queryClient.ensureQueryData(...)`, built from the same
 * `INVITATIONS_QUERY` object `InvitationList` reads with, so the loader's
 * prefetch and the component's `useQuery` hit the same cache entry.
 */
function InvitationsPage() {
  return (
    <PageContainer data-testid="dashboard-invitations">
      <PageHeader
        data-testid="invitations-heading"
        title="Outstanding invitations"
        description="Invitations issued to this organization that have not yet been accepted."
      />
      <InvitationList />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/organizations/invitations")({
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(
      invitationsGetByTenantOptions({ client: context.sdk.client, query: INVITATIONS_QUERY }),
    ),
  component: InvitationsPage,
});
