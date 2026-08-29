import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { QueryClient } from "@tanstack/react-query";
import { createRouter as createTanStackRouter } from "@tanstack/react-router";
import { setupRouterSsrQueryIntegration } from "@tanstack/react-router-ssr-query";
import { getGlobalStartContext } from "@tanstack/react-start";

import { routeTree } from "./routeTree.gen";

/** Per request on the server, once in the browser: a fresh QueryClient + the request's SDK. */
export function getRouter() {
  const queryClient = new QueryClient();
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
