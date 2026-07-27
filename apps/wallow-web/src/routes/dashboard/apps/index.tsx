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
/**
 * Title row: page heading on the left, gold pill CTA on the right. Extracted so
 * the page body stays within the repo's JSX nesting budget.
 */
function AppsHeader() {
  return (
    <div className="flex items-center justify-between mb-8">
      <h1 data-testid="apps-heading" className="text-3xl font-bold text-foreground">
        My Apps
      </h1>
      <a
        data-testid="apps-register-link"
        href="/dashboard/apps/register"
        className="bg-primary text-primary-foreground font-medium px-6 py-2.5 rounded-full hover:opacity-90 no-underline text-sm transition-colors"
      >
        Register New App
      </a>
    </div>
  );
}

function AppsIndexPage() {
  return (
    <div data-testid="dashboard-apps" className="max-w-5xl mx-auto">
      <AppsHeader />
      <AppList />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/apps/")({
  loader: ({ context }) => context.queryClient.ensureQueryData(appsQueries.list()),
  component: AppsIndexPage,
});
