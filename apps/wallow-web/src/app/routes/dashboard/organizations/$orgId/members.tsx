import { PageContainer } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import {
  MemberRoles,
  organizationsGetAllOptions,
  organizationsGetMembersOptions,
  rolesGetRolesOptions,
} from "@features/organizations";
import { notFoundOn404 } from "@shared/lib/not-found-on-404";

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
  // The members read answers 404 for a missing organization, so the whole
  // prefetch is the router's not-found (a 404 response), not a loader error.
  loader: ({ context, params }) => {
    const client = context.sdk.client;
    const members = context.queryClient.ensureQueryData(
      organizationsGetMembersOptions({ client, path: { id: params.orgId } }),
    );
    const organizations = context.queryClient.ensureQueryData(
      organizationsGetAllOptions({ client }),
    );
    const roles = context.queryClient.ensureQueryData(rolesGetRolesOptions({ client }));
    return notFoundOn404(Promise.all([members, organizations, roles]));
  },
  component: MemberRolesPage,
});
