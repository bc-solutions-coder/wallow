import { createFileRoute } from "@tanstack/react-router";

import { organizationsQueries } from "../../../features/organizations/api";
import { OrganizationDetail } from "../../../features/organizations/components/OrganizationDetail";

/**
 * The dashboard organization-detail route (Wallow-8w1h.4.4). Mirrors the list
 * route's authored file-route style (`createFileRoute('/dashboard/organizations/
 * $orgId')`); `src/router.tsx` binds it under the root via
 * `.update({ id, path, getParentRoute })` (no dashboard layout route yet).
 *
 * The `loader` prefetches both the org detail and its members via
 * `context.queryClient.ensureQueryData(...)`; the page reads the `orgId` route
 * param and renders `OrganizationDetail` (which owns all render coverage).
 */
function OrganizationDetailPage() {
  const { orgId } = Route.useParams();
  return (
    <div data-testid="dashboard-organization-detail">
      <OrganizationDetail orgId={orgId} />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/organizations/$orgId")({
  loader: ({ context, params }) =>
    Promise.all([
      context.queryClient.ensureQueryData(organizationsQueries.detail(params.orgId)),
      context.queryClient.ensureQueryData(organizationsQueries.members(params.orgId)),
    ]),
  component: OrganizationDetailPage,
});
