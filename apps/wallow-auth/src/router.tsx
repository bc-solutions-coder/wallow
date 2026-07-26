import { QueryClientProvider } from "@tanstack/react-query";
import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "@bc-solutions-coder/web-shell";
import { routeTree } from "./routeTree.gen";

// Side-effect import: registers the app's client configurator with the SDK query
// bootstrap. Loaded here so BOTH the client and SSR module graphs are armed
// before any route fires a query.
import "./lib/wallow-auth-sdk";

/**
 * Constructs the TanStack router that boots the wallow-auth Start app.
 *
 * The route tree is produced by TanStack Router's file-based codegen
 * (`src/routeTree.gen.ts`, regenerated via `pnpm routes:generate`); every route
 * under `src/routes/` is wired into it automatically, so no route is bound by
 * hand here. The paths themselves are the app's external contract — the stable
 * auth URLs any client links to.
 *
 * One `QueryClient` is minted per router (per SSR request) and used two ways: as
 * the router `context` client that loaders/`beforeLoad` reach via
 * `context.queryClient`, and — through the `Wrap` render-prop's
 * `QueryClientProvider` — as the client every routed component reads with React
 * Query hooks. Both are the SAME instance (Wallow-evd5.3.4); the root route used
 * to mint a second one for its own provider, which split the two sides onto
 * separate caches so loader-prefetched data never reached the components that
 * consume it.
 */
export function createRouter(): AnyRouter {
  const queryClient = createQueryClient();

  return createTanStackRouter({
    routeTree,
    context: { queryClient },
    Wrap: ({ children }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createRouter>;
  }
}
