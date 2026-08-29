import { PageContainer } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import {
  ClientRegistrationPrototype,
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
 *
 * PROTOTYPE (map #112 / ticket #122): `?variant=A|B|C` swaps the clients
 * section for the client-registration prototype. Throwaway.
 */
function OrganizationDetailPage() {
  const { orgId } = Route.useParams();
  const { variant } = Route.useSearch();
  return (
    <PageContainer data-testid="dashboard-organization-detail">
      <OrganizationDetail
        orgId={orgId}
        clientsSection={
          variant
            ? (orgName) => (
                <ClientRegistrationPrototype orgId={orgId} orgName={orgName} variant={variant} />
              )
            : undefined
        }
      />
    </PageContainer>
  );
}

export const Route = createFileRoute("/dashboard/organizations/$orgId/")({
  validateSearch: (search: Record<string, unknown>): { variant?: string } => ({
    variant: typeof search.variant === "string" ? search.variant : undefined,
  }),
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
