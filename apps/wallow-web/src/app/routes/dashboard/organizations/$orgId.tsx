import { createFileRoute } from "@tanstack/react-router";

import {
  OrganizationDetail,
  organizationsGetByIdOptions,
  organizationsGetMembersOptions,
} from "@features/organizations";

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
    <div data-testid="dashboard-organization-detail" className="max-w-5xl mx-auto">
      <OrganizationDetail orgId={orgId} />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/organizations/$orgId")({
  loader: ({ context, params }) =>
    Promise.all([
      context.queryClient.ensureQueryData(
        organizationsGetByIdOptions({ client: context.sdk.client, path: { id: params.orgId } }),
      ),
      context.queryClient.ensureQueryData(
        organizationsGetMembersOptions({ client: context.sdk.client, path: { id: params.orgId } }),
      ),
    ]),
  component: OrganizationDetailPage,
});
