import { createFileRoute } from "@tanstack/react-router";

import { organizationsGetAllOptions } from "../../../features/organizations/api";
import { CreateOrganizationForm } from "../../../features/organizations/components/CreateOrganizationForm";
import { OrganizationList } from "../../../features/organizations/components/OrganizationList";

/**
 * The dashboard organizations index route (Wallow-8w1h.4.2) — the CANONICAL
 * authenticated list route every later vertical (Phases 4-6) copies.
 *
 * The page root carries `data-testid="dashboard-organizations"` and renders the
 * `OrganizationList` component; the route `loader` prefetches the list via
 * `context.queryClient.ensureQueryData(organizationsGetAllOptions({ client }))`,
 * binding the request-scoped client off the router context.
 *
 * Authored file-route style (`createFileRoute('/dashboard/organizations/')`),
 * so its `id`/`path`/parent are left unset — `src/router.tsx` binds it under the
 * root via `.update({ id, path, getParentRoute })` (there is no dashboard layout
 * route yet; that lands in Phase 7).
 */
/**
 * Title row. Extracted so the page body stays within the repo's JSX nesting
 * budget. Unlike the apps page there is no CTA beside the heading: the create
 * form mounts inline below the list, so there is no create-page to link to.
 */
function OrganizationsHeader() {
  return (
    <div className="flex items-center justify-between mb-8">
      <h1 data-testid="organizations-heading" className="text-3xl font-bold text-foreground">
        Organizations
      </h1>
    </div>
  );
}

function OrganizationsIndexPage() {
  return (
    <div data-testid="dashboard-organizations" className="max-w-5xl mx-auto">
      <OrganizationsHeader />
      <OrganizationList />
      <CreateOrganizationForm />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/organizations/")({
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(organizationsGetAllOptions({ client: context.sdk.client })),
  component: OrganizationsIndexPage,
});
