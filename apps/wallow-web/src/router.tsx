import { QueryClientProvider } from "@tanstack/react-query";
import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "@bc-solutions-coder/web-shell";
import { routeTree } from "./routeTree.gen";
// Side-effect import: runs wallow-sdk.ts's module-scope `registerQueryBootstrap`
// in both the client and SSR graphs before any route fires an SDK query.
import "./lib/wallow-sdk";

/**
 * Constructs the TanStack router that boots the wallow-web Start app.
 *
 * The route tree is produced by TanStack Router's file-based codegen
 * (`src/routeTree.gen.ts`, regenerated via `pnpm routes:generate`); every route
 * under `src/routes/` — including the `/dashboard` layout shell and the verticals
 * nested beneath it — is wired into it automatically, so no route is reparented
 * by hand here.
 *
 * One `QueryClient` is minted per router (per SSR request) and used two ways: as
 * the router `context` client that loaders/`beforeLoad` reach via
 * `context.queryClient`, and — through the `Wrap` render-prop's
 * `QueryClientProvider` — as the client every routed component reads with React
 * Query hooks. Both are the SAME instance, so SSR-prefetched loader data reaches
 * the components that consume it.
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
