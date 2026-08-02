import { PageContainer, PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import { meGetOrganizationsOptions, MyOrganizations } from "@features/organizations";

/**
 * The member-facing "my organizations" route (Wallow-yp3e.7). Deliberately
 * TOP-LEVEL rather than nested under `organizations/`: `MeController.GetOrganizations`
 * asks for no permission and reads the caller's own memberships, unlike
 * `organizations/index.tsx`'s admin-only roster — nesting it under the
 * admin-gated vertical would put it behind a nav gate no plain member clears.
 *
 * The `loader` prefetches the memberships list via
 * `context.queryClient.ensureQueryData(...)`, built from the same query
 * options `MyOrganizations` reads with, so the loader's prefetch and the
 * component's `useQuery` hit the same cache entry.
 */
function MyOrganizationsPage() {
  return (
    <PageContainer data-testid="dashboard-my-organizations">
      <PageHeader
        data-testid="my-organizations-heading"
        title="My organizations"
        description="Organizations you belong to. Leaving removes your access immediately."
      />
      <MyOrganizations />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/my-organizations")({
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(meGetOrganizationsOptions({ client: context.sdk.client })),
  component: MyOrganizationsPage,
});
