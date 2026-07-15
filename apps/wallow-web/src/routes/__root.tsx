import { type QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRootRoute, Outlet } from "@tanstack/react-router";
import { useRef } from "react";

import { createQueryClient } from "../lib/query-client";

/**
 * The SSR document shell (Wallow-8w1h.2.2): a full `<html>/<head>/<body>`
 * wrapping the router `<Outlet/>` that child routes render into.
 *
 * The shell is deliberately hook-free of router context (no
 * `HeadContent`/`Scripts`, no `Route.useRouteContext()`) so it can be
 * server-rendered on its own — those helpers require a live `RouterProvider`
 * context and belong with the client-hydration wiring in a later phase.
 */
function DocumentShell() {
  return (
    <html lang="en">
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>Wallow</title>
      </head>
      <body>
        <Outlet />
      </body>
    </html>
  );
}

/**
 * The root route (Wallow-8w1h.3.1) wraps the document shell in a
 * `QueryClientProvider` so every routed child can call React Query hooks.
 *
 * The provider's client is sourced standalone-safely — a lazily-initialised ref
 * that mints one stable client on first render — rather than from the router
 * context, so the shell still server-renders on its own without a
 * `RouterProvider`. The router-context client (for loaders) and this
 * React-tree provider client are wired independently this phase.
 */
function RootComponent() {
  const queryClientRef = useRef<QueryClient | null>(null);
  queryClientRef.current ??= createQueryClient();

  return (
    <QueryClientProvider client={queryClientRef.current}>
      <DocumentShell />
    </QueryClientProvider>
  );
}

export const Route = createRootRoute({
  component: RootComponent,
});
