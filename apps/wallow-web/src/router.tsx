import {
  dehydrate,
  type DehydratedState,
  hydrate,
  QueryClientProvider,
} from "@tanstack/react-query";
import { createRouter as createTanStackRouter, type AnyRouter } from "@tanstack/react-router";

import { createQueryClient } from "@bc-solutions-coder/web-shell";
import { routeTree } from "./routeTree.gen";
// Side-effect import: runs wallow-sdk.ts's module-scope `registerQueryBootstrap`
// in both the client and SSR graphs before any route fires an SDK query.
import "./lib/wallow-sdk";

/**
 * Per-request router state carried from the SSR document into the browser pass.
 *
 * The cache travels as a JSON string rather than as `DehydratedState` itself.
 * Router's serializer type-checks every field of what `dehydrate()` returns, and
 * React Query types cached entries as `unknown` (query data, query/mutation
 * keys), which cannot be proven serializable — so a structured payload is
 * rejected at compile time even though it round-trips fine. React Query's
 * dehydrated state is JSON by contract, so pre-serializing it is lossless here
 * and keeps the hooks free of casts.
 */
interface WallowDehydrated {
  queryClientState: string;
}

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
 *
 * The SSR pass and the browser pass each call this function, so the browser's
 * client would otherwise start empty and refetch everything the loaders already
 * prefetched. The `dehydrate`/`hydrate` router options close over that one
 * client and carry its cache across the boundary: TanStack Router serializes
 * `dehydrate()`'s return value into the SSR document and replays it through
 * `hydrate()` before `RouterClient` hands off, so `ssr.tsx` and `client.tsx`
 * need no wiring of their own.
 */
export function createRouter(): AnyRouter {
  const queryClient = createQueryClient();

  return createTanStackRouter({
    routeTree,
    context: { queryClient },
    dehydrate: (): WallowDehydrated => ({
      queryClientState: JSON.stringify(dehydrate(queryClient)),
    }),
    hydrate: (dehydrated: WallowDehydrated) => {
      hydrate(queryClient, JSON.parse(dehydrated.queryClientState) as DehydratedState);
    },
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
