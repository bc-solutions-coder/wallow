import { PageContainer } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { notFoundOn404 } from "@shared/lib/not-found-on-404";

import {
  OrganizationDetail,
  organizationsGetByIdOptions,
  organizationsGetMembersOptions,
} from "@features/organizations";

/**
 * The dashboard organization-detail route. Directory form (no `route.tsx`),
 * so it contributes no layout and nests `$orgId`-scoped siblings under it.
 *
 * The `loader` prefetches both the org detail and its members via
 * `context.queryClient.ensureQueryData(...)`; the page reads the `orgId` route
 * param and renders `OrganizationDetail` (which owns all render coverage).
 * Both reads go through `notFoundOn404`, so a missing record is the
 * router's not-found (and a 404 response), not a 500 with the right screen.
 */
function OrganizationDetailPage() {
  const { orgId } = Route.useParams();
  return (
    <PageContainer data-testid="dashboard-organization-detail">
      <OrganizationDetail orgId={orgId} />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/organizations/$orgId/")({
  loader: ({ context, params }) => {
    const path = { id: params.orgId };
    const organization = context.queryClient.ensureQueryData(
      organizationsGetByIdOptions({ client: context.sdk.client, path }),
    );
    const members = context.queryClient.ensureQueryData(
      organizationsGetMembersOptions({ client: context.sdk.client, path }),
    );
    // Both reads answer 404 for a missing organization, and whichever settles first
    // decides the loader's outcome — so the pair, not the by-id read alone,
    // is what becomes the router's not-found.
    return notFoundOn404(Promise.all([organization, members]));
  },
  component: OrganizationDetailPage,
});
