import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { QueryClient } from "@tanstack/react-query";
import { createRouter as createTanStackRouter } from "@tanstack/react-router";
import { setupRouterSsrQueryIntegration } from "@tanstack/react-router-ssr-query";
import { getGlobalStartContext } from "@tanstack/react-start";

import { routeTree } from "./routeTree.gen";

/**
 * Build the router. Request-scoped like every Wallow app's: a fresh QueryClient
 * per request, and the request's own SDK instance — from the `start.ts`
 * middleware on the server, minted same-origin in the browser.
 *
 * A plain `new QueryClient()` rather than the web-shell helper: this scratch app
 * consumes only the two packed tarballs (sdk + styles), so it must stand up
 * without the rest of the workspace.
 */
export function getRouter() {
  const queryClient: QueryClient = new QueryClient();
  const sdk: WallowSdk =
    getGlobalStartContext()?.sdk ?? createWallowSdk({ baseUrl: globalThis.location.origin });

  const router = createTanStackRouter({
    routeTree,
    context: { queryClient, sdk },
    scrollRestoration: true,
  });

  setupRouterSsrQueryIntegration({ router, queryClient });

  return router;
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof getRouter>;
  }
}
