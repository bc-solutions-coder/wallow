import { createFileRoute } from "@tanstack/react-router";

import { organizationsQueries } from "../../../features/organizations/api";
import { OrganizationList } from "../../../features/organizations/components/OrganizationList";

/**
 * The dashboard organizations index route (Wallow-8w1h.4.2) — the CANONICAL
 * authenticated list route every later vertical (Phases 4-6) copies.
 *
 * The page root carries `data-testid="dashboard-organizations"` and renders the
 * `OrganizationList` component; the route `loader` prefetches the list via
 * `context.queryClient.ensureQueryData(organizationsQueries.list())`.
 *
 * Authored file-route style (`createFileRoute('/dashboard/organizations/')`),
 * so its `id`/`path`/parent are left unset — `src/router.tsx` binds it under the
 * root via `.update({ id, path, getParentRoute })` (there is no dashboard layout
 * route yet; that lands in Phase 7).
 */
function OrganizationsIndexPage() {
  return (
    <div data-testid="dashboard-organizations">
      <OrganizationList />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/organizations/")({
  loader: ({ context }) => context.queryClient.ensureQueryData(organizationsQueries.list()),
  component: OrganizationsIndexPage,
});
