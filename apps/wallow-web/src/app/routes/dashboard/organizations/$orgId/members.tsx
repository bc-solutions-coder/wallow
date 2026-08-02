import { PageContainer } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import {
  MemberRoles,
  organizationsGetAllOptions,
  organizationsGetMembersOptions,
  rolesGetRolesOptions,
} from "@features/organizations";

/**
 * The dashboard member-roles route, nested under the `$orgId` directory
 * alongside the org-detail and pending-requests pages. The auth gate is
 * inherited from `routes/dashboard/route.tsx`; this route adds none.
 *
 * The `loader` prefetches the three queries `MemberRoles` reads —
 * members, the caller's own organizations (the own-org gate), and the roles
 * catalog — via `context.queryClient.ensureQueryData(...)`; the page reads
 * the `orgId` route param and renders `MemberRoles` (which owns all render
 * coverage).
 */
function MemberRolesPage() {
  const { orgId } = Route.useParams();
  return (
    <PageContainer data-testid="dashboard-organization-members">
      <MemberRoles orgId={orgId} />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/organizations/$orgId/members")({
  loader: ({ context, params }) =>
    Promise.all([
      context.queryClient.ensureQueryData(
        organizationsGetMembersOptions({ client: context.sdk.client, path: { id: params.orgId } }),
      ),
      context.queryClient.ensureQueryData(
        organizationsGetAllOptions({ client: context.sdk.client }),
      ),
      context.queryClient.ensureQueryData(rolesGetRolesOptions({ client: context.sdk.client })),
    ]),
  component: MemberRolesPage,
});
