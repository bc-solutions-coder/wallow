import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "./lib/query-client";
import { Route as rootRoute } from "./routes/__root";
import { Route as appsIndexRoute } from "./routes/dashboard/apps/index";
import { Route as organizationDetailRoute } from "./routes/dashboard/organizations/$orgId";
import { Route as organizationsIndexRoute } from "./routes/dashboard/organizations/index";
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

  const organizationsIndexWithParent = organizationsIndexRoute.update({
    id: "/dashboard/organizations",
    path: "/dashboard/organizations",
    getParentRoute: () => rootRoute,
  });

  const organizationDetailWithParent = organizationDetailRoute.update({
    id: "/dashboard/organizations/$orgId",
    path: "/dashboard/organizations/$orgId",
    getParentRoute: () => rootRoute,
  });

  const appsIndexWithParent = appsIndexRoute.update({
    id: "/dashboard/apps",
    path: "/dashboard/apps",
    getParentRoute: () => rootRoute,
  });

  const settingsWithParent = settingsRoute.update({
    id: "/dashboard/settings",
    path: "/dashboard/settings",
    getParentRoute: () => rootRoute,
  });

  const routeTree = rootRoute.addChildren([
    indexRouteWithParent,
    organizationsIndexWithParent,
    organizationDetailWithParent,
    appsIndexWithParent,
    settingsWithParent,
  ]);

  const queryClient = createQueryClient();

  return createTanStackRouter({ routeTree, context: { queryClient } });
}
