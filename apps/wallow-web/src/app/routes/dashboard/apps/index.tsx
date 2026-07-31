import { Button, PageHeader } from "@bc-solutions-coder/ui";
import { createFileRoute, Link } from "@tanstack/react-router";

import { AppList, appsGetUserAppsOptions } from "@features/apps";
import { PAGE_CONTAINER } from "@shared/lib/page-container";

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
 * The gold pill CTA the header carries in its trailing actions slot.
 *
 * The CTA is a TanStack `Link` wearing the catalog `Button` (Wallow-lrlm.4.3).
 * It used to be a raw `<a>`, which made an in-app hop to a route already in the
 * tree a full document load — router torn down, query cache thrown away, white
 * flash. Composing through `render` keeps the real anchor (so middle-click and
 * copy-link still work) while the router claims the click.
 *
 * The prop that looks like an incantation is not: `nativeButton={false}` tells
 * Base UI it is not wrapping a native `<button>` (left at its default it logs a
 * dev-mode "expected a native <button>" error on every render). The `link` role
 * used to be spelled here too, as `role={undefined}` to strip the `role="button"`
 * Base UI stamps on any non-native element; the catalog `Button` now announces a
 * mounted anchor as a link on its own (Wallow-lrlm.12), and it sees through the
 * `Link` component to do it.
 *
 * `width="auto"` and `shape="pill"` override the recipe's `w-full`/`rounded-md`
 * defaults; the `className` keeps the shipped `px-6 py-2.5` footprint, which no
 * size on the scale reproduces exactly.
 */
const registerCta = (
  <Button
    render={<Link to="/dashboard/apps/register" />}
    nativeButton={false}
    shape="pill"
    width="auto"
    data-testid="apps-register-link"
    className="px-6 py-2.5 no-underline"
  >
    Register New App
  </Button>
);

/**
 * The page title block is the catalog `PageHeader` (Wallow-lrlm.5.1), which owns
 * the row layout, the heading element and its type scale; the page names the
 * header once and the inner testids (`apps-header-title`, `apps-header-actions`)
 * derive from it. The content width is the shared `PAGE_CONTAINER` rule rather
 * than a width written into this page.
 */
function AppsIndexPage() {
  return (
    <div data-testid="dashboard-apps" className={PAGE_CONTAINER}>
      <PageHeader data-testid="apps-header" title="My Apps" actions={registerCta} />
      <AppList />
    </div>
  );
}

export const Route = createFileRoute("/dashboard/apps/")({
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(appsGetUserAppsOptions({ client: context.sdk.client })),
  component: AppsIndexPage,
});
