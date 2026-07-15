import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "./lib/query-client";
import { Route as rootRoute } from "./routes/__root";
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

  const routeTree = rootRoute.addChildren([indexRouteWithParent]);

  const queryClient = createQueryClient();

  return createTanStackRouter({ routeTree, context: { queryClient } });
}
