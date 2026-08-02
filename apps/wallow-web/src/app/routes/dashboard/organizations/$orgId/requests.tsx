import { PageContainer } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { organizationsGetPendingMembersOptions, PendingRequestList } from "@features/organizations";

/**
 * The dashboard pending-requests route, nested under the `$orgId` directory
 * alongside the org-detail page. The auth gate is inherited from
 * `routes/dashboard/route.tsx`; this route adds none.
 *
 * The `loader` prefetches the pending-requests query via
 * `context.queryClient.ensureQueryData(...)`; the page reads the `orgId`
 * route param and renders `PendingRequestList` (which owns all render
 * coverage).
 */
function PendingRequestsPage() {
  const { orgId } = Route.useParams();
  return (
    <PageContainer data-testid="dashboard-organization-requests">
      <PendingRequestList orgId={orgId} />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/organizations/$orgId/requests")({
  loader: ({ context, params }) =>
    context.queryClient.ensureQueryData(
      organizationsGetPendingMembersOptions({
        client: context.sdk.client,
        path: { id: params.orgId },
      }),
    ),
  component: PendingRequestsPage,
});
