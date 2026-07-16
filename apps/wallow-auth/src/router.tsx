import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "./lib/query-client";
import { Route as rootRoute } from "./routes/__root";
import { Route as indexRoute } from "./routes/index";

/**
 * Assembles the minimal route tree (root + index) and constructs the TanStack
 * router that boots the wallow-auth Start app (Wallow-vec7.1.4).
 *
 * The routes are authored file-route style (`createFileRoute('/')(options)`),
 * which leaves their `id`/`path`/parent unset — the file-based codegen would
 * normally fill those in. The installed Start version ships no route-tree
 * codegen (see bd memory wallow-web-tanstack-manual-route-tree), so we bind the
 * index route to the root here so it registers at "/". Phase 0 only needs the
 * root + index (which redirects to `/login`); the login/register/reset routes
 * are added in a later phase.
 */
export function createRouter(): AnyRouter {
  const indexRouteWithParent = indexRoute.update({
    id: "/",
    path: "/",
    getParentRoute: () => rootRoute,
  });

  const routeTree = rootRoute.addChildren([indexRouteWithParent]);

  const queryClient = createQueryClient();

  return createTanStackRouter({ routeTree, context: { queryClient } });
}
