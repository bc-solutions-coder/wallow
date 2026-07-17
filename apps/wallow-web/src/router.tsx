import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "./lib/query-client";
import { Route as rootRoute } from "./routes/__root";
import { Route as bffDemoRoute } from "./routes/bff-demo";
import { Route as appsIndexRoute } from "./routes/dashboard/apps/index";
import { Route as registerAppRoute } from "./routes/dashboard/apps/register";
import { Route as inquiryDetailRoute } from "./routes/dashboard/inquiries/$inquiryId";
import { Route as inquiriesIndexRoute } from "./routes/dashboard/inquiries/index";
import { Route as organizationDetailRoute } from "./routes/dashboard/organizations/$orgId";
import { Route as organizationsIndexRoute } from "./routes/dashboard/organizations/index";
import { Route as dashboardRoute } from "./routes/dashboard/route";
import { Route as settingsRoute } from "./routes/dashboard/settings";
import { Route as indexRoute } from "./routes/index";

/**
 * Assembles the minimal route tree (root + public index) and constructs the
 * TanStack router that boots the Start app (Wallow-8w1h.2.2).
 *
 * The routes are authored file-route style (`createFileRoute('/')(options)`),
 * which leaves their `id`/`path`/parent unset — the file-based codegen would
 * normally fill those in. Until Start's route-tree codegen is wired (later
 * phase), we bind the index route to the root here so it registers at "/".
 */
export function createRouter(): AnyRouter {
  const indexRouteWithParent = indexRoute.update({
    id: "/",
    path: "/",
    getParentRoute: () => rootRoute,
  });

  // The dedicated `/bff-demo` route (Wallow-8w1h.8.2) — the React port of the
  // BFF smoke surface, bound under the root like the other manual routes.
  const bffDemoRouteWithParent = bffDemoRoute.update({
    id: "/bff-demo",
    path: "/bff-demo",
    getParentRoute: () => rootRoute,
  });

  // The `/dashboard` layout route (Wallow-8w1h.8.1) — the authenticated shell
  // that the dashboard verticals reparent under so they render inside its
  // `<Outlet/>` and share its `beforeLoad` auth gate.
  const dashboardRouteWithParent = dashboardRoute.update({
    id: "/dashboard",
    path: "/dashboard",
    getParentRoute: () => rootRoute,
  });

  // Children are reparented under `/dashboard`, so their paths are now RELATIVE
  // to the layout route. TanStack composes the full id/path from the parent's
  // id/path + the child's relative segment (parent "/dashboard" + "organizations"
  // -> id/path "/dashboard/organizations"), so the registered ids/paths are
  // unchanged from the previous flat tree — only the parent changes.
  const organizationsIndexWithParent = organizationsIndexRoute.update({
    path: "organizations",
    getParentRoute: () => dashboardRouteWithParent,
  });

  const organizationDetailWithParent = organizationDetailRoute.update({
    path: "organizations/$orgId",
    getParentRoute: () => dashboardRouteWithParent,
  });

  const appsIndexWithParent = appsIndexRoute.update({
    path: "apps",
    getParentRoute: () => dashboardRouteWithParent,
  });

  const registerAppWithParent = registerAppRoute.update({
    path: "apps/register",
    getParentRoute: () => dashboardRouteWithParent,
  });

  const settingsWithParent = settingsRoute.update({
    path: "settings",
    getParentRoute: () => dashboardRouteWithParent,
  });

  const inquiriesIndexWithParent = inquiriesIndexRoute.update({
    path: "inquiries",
    getParentRoute: () => dashboardRouteWithParent,
  });

  const inquiryDetailWithParent = inquiryDetailRoute.update({
    path: "inquiries/$inquiryId",
    getParentRoute: () => dashboardRouteWithParent,
  });

  const routeTree = rootRoute.addChildren([
    indexRouteWithParent,
    bffDemoRouteWithParent,
    dashboardRouteWithParent.addChildren([
      organizationsIndexWithParent,
      organizationDetailWithParent,
      appsIndexWithParent,
      registerAppWithParent,
      settingsWithParent,
      inquiriesIndexWithParent,
      inquiryDetailWithParent,
    ]),
  ]);

  const queryClient = createQueryClient();

  return createTanStackRouter({ routeTree, context: { queryClient } });
}
