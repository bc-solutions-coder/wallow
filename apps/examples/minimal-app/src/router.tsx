import { createQueryClient, type QueryClient } from "@bc-solutions-coder/query";
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createRouter as createTanStackRouter } from "@tanstack/react-router";
import { setupRouterSsrQueryIntegration } from "@tanstack/react-router-ssr-query";
import { getGlobalStartContext } from "@tanstack/react-start";

import { routeTree } from "./routeTree.gen";

/**
 * Build the router that boots the app. Start calls this ONCE PER REQUEST on the
 * server (and once on the client), so everything it constructs is request-scoped:
 * a fresh `QueryClient` — never a module-global one, which would hand one user's
 * cached data to the next — and the request's own SDK instance.
 *
 * On the server that SDK comes from the global request middleware in `start.ts`,
 * the only place the inbound cookie is in scope; in the browser there is no
 * request, so it mints its own same-origin instance.
 *
 * `setupRouterSsrQueryIntegration` owns the dehydrate/hydrate handoff of the
 * React Query cache across the SSR boundary — the deleted host did that by hand.
 *
 * The return type is inferred deliberately: annotating it `AnyRouter` erases the
 * route-tree types that make `Link`/`useParams` typed. The route tree itself is
 * codegen'd into `src/routeTree.gen.ts` by the Start plugin as a side effect of
 * `vite dev`/`vite build`, so there is no `routes:generate` step to remember.
 */
export function getRouter() {
  const queryClient: QueryClient = createQueryClient();
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
