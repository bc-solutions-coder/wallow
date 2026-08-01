import { withBasePath } from "@bc-solutions-coder/env/base-path";
import { createQueryClient, type QueryClient } from "@bc-solutions-coder/query";
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createRouter as createTanStackRouter } from "@tanstack/react-router";
import { setupRouterSsrQueryIntegration } from "@tanstack/react-router-ssr-query";
import { getGlobalStartContext } from "@tanstack/react-start";

import { BASE_PATH } from "@shared/lib/base-path";
import { routeTree } from "./routeTree.gen";

/**
 * Build the router that boots the wallow-auth app. Start calls this ONCE PER
 * REQUEST on the server (and once on the client), so everything it constructs is
 * request-scoped: a fresh `QueryClient` — never a module-global one, which would
 * hand one user's cached data to the next — and the request's own SDK instance.
 *
 * On the server that SDK comes from the global request middleware in `start.ts`,
 * the only place the inbound cookie is in scope; in the browser there is no
 * request, so it mints its own same-origin instance.
 *
 * `setupRouterSsrQueryIntegration` owns the dehydrate/hydrate handoff of the
 * React Query cache across the SSR boundary and installs the single
 * `QueryClientProvider` this app used to wire by hand through a `Wrap`
 * render-prop. Loaders (router context) and components (React Query hooks)
 * therefore still share ONE cache per request (Wallow-evd5.3.4).
 *
 * The route paths remain the app's external contract — the stable auth URLs any
 * client links to — but the tree binding them is codegen'd into
 * `src/routeTree.gen.ts` by the Start plugin as a side effect of `vite dev` /
 * `vite build`, so there is no `routes:generate` step to remember.
 *
 * The return type is inferred deliberately: annotating it `AnyRouter` erases the
 * route-tree types that make `Link`/`useParams` typed.
 */
export function getRouter() {
  const queryClient: QueryClient = createQueryClient();
  const sdk: WallowSdk =
    getGlobalStartContext()?.sdk ??
    createWallowSdk({ baseUrl: withBasePath(globalThis.location.origin, BASE_PATH) });

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
