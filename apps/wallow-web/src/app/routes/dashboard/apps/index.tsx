import { Button } from "@bc-solutions-coder/ui";
import { createFileRoute, Link } from "@tanstack/react-router";

import { AppList, appsGetUserAppsOptions } from "@features/apps";

/**
 * The dashboard apps index route (Wallow-8w1h.5.2) — copies the CANONICAL
 * authenticated list route (`dashboard/organizations/index`, Wallow-8w1h.4.2).
 *
 * The page root carries `data-testid="dashboard-apps"` and renders the `AppList`
 * component; the route `loader` prefetches the list via
 * `context.queryClient.ensureQueryData(appsGetUserAppsOptions({ client }))`,
 * binding the request-scoped client off the router context.
 *
 * Authored file-route style (`createFileRoute('/dashboard/apps/')`), so its
 * `id`/`path`/parent are left unset — `src/router.tsx` binds it under the root
 * via `.update({ id, path, getParentRoute })` (there is no dashboard layout
 * route yet; that lands in Phase 7).
 */
/**
 * Title row: page heading on the left, gold pill CTA on the right. Extracted so
 * the page body stays within the repo's JSX nesting budget.
 *
 * The CTA is a TanStack `Link` wearing the catalog `Button` (Wallow-lrlm.4.3).
 * It used to be a raw `<a>`, which made an in-app hop to a route already in the
 * tree a full document load — router torn down, query cache thrown away, white
 * flash. Composing through `render` keeps the real anchor (so middle-click and
 * copy-link still work) while the router claims the click.
 *
 * The two props that look like incantations are not: `nativeButton={false}`
 * tells Base UI it is not wrapping a native `<button>` (left at its default it
 * logs a dev-mode "expected a native <button>" error on every render), and
 * `role={undefined}` strips the `role="button"` that flag would otherwise add —
 * `useButton` merges its own role BEFORE the caller's props, so the caller
 * wins. A navigation must stay announced as a link.
 *
 * `width="auto"` and `shape="pill"` override the recipe's `w-full`/`rounded-md`
 * defaults; the `className` keeps the shipped `px-6 py-2.5` footprint, which no
 * size on the scale reproduces exactly.
 */
function AppsHeader() {
  return (
    <div className="flex items-center justify-between mb-8">
      <h1 data-testid="apps-heading" className="text-3xl font-bold text-foreground">
        My Apps
      </h1>
      <Button
        render={<Link to="/dashboard/apps/register" />}
        nativeButton={false}
        role={undefined}
        shape="pill"
        width="auto"
        data-testid="apps-register-link"
        className="px-6 py-2.5 no-underline"
      >
        Register New App
      </Button>
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
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(appsGetUserAppsOptions({ client: context.sdk.client })),
  component: AppsIndexPage,
});
