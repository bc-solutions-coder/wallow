import { PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute } from "@tanstack/react-router";

import {
  CreateOrganizationForm,
  organizationsGetAllOptions,
  OrganizationList,
} from "@features/organizations";
import { PAGE_CONTAINER } from "@shared/lib/page-container";

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
 * The title block is the catalog `PageHeader` (Wallow-lrlm.5.1). Unlike the apps
 * page it is given no `actions`: the create form mounts inline below the list,
 * so there is no create-page to link to, and `PageHeader` then omits the actions
 * slot rather than leaving an empty flex child in the row.
 */
function OrganizationsIndexPage() {
  return (
    <div data-testid="dashboard-organizations" className={PAGE_CONTAINER}>
      <PageHeader data-testid="organizations-header" title="Organizations" />
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
