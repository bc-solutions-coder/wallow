import { createFileRoute } from "@tanstack/react-router";

import { appsQueries } from "../../../features/apps/api";
import { AppList } from "../../../features/apps/components/AppList";

/**
 * The dashboard apps index route (Wallow-8w1h.5.2) — copies the CANONICAL
 * authenticated list route (`dashboard/organizations/index`, Wallow-8w1h.4.2).
 *
 * The page root carries `data-testid="dashboard-apps"` and renders the `AppList`
 * component; the route `loader` prefetches the list via
 * `context.queryClient.ensureQueryData(appsQueries.list())`.
 *
 * Authored file-route style (`createFileRoute('/dashboard/apps/')`), so its
 * `id`/`path`/parent are left unset — `src/router.tsx` binds it under the root
 * via `.update({ id, path, getParentRoute })` (there is no dashboard layout
 * route yet; that lands in Phase 7).
 */
function AppsIndexPage() {
  return (
    <div data-testid="dashboard-apps">
      <a data-testid="apps-register-link" href="/dashboard/apps/register">
        Register New App
      </a>
      <AppList />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/apps/")({
  loader: ({ context }) => context.queryClient.ensureQueryData(appsQueries.list()),
  component: AppsIndexPage,
});
