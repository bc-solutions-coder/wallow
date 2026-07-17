import { type QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRootRoute, Outlet } from "@tanstack/react-router";
import { useRef } from "react";

import { FocusOnNavigate } from "../components/focus-on-navigate";
import { ReadyIndicator } from "../components/ready-indicator";
import {
  appIconUrl,
  forkResolvedBranding,
  renderThemeStyle,
  type ResolvedBranding,
} from "../lib/branding";
import { createQueryClient } from "../lib/query-client";

/**
 * The browser bundle to load. In dev, Vite serves the entry straight out of its
 * module graph at its source path (`dev-server.ts` offers non-API requests to
 * `vite.middlewares` before falling through to SSR); a production build emits it
 * at `/client.js` (see `vite.config.ts`, which pins that filename).
 *
 * The tag is rendered by the React tree — server and client alike — rather than
 * injected into the HTML string, so both passes agree on it and hydration stays
 * clean. `import.meta.env.DEV` is substituted at build time in both graphs, so
 * the two can never disagree about which path this is.
 */
const clientEntry: string = import.meta.env.DEV ? "/src/client.tsx" : "/client.js";

/**
 * The SSR document shell for wallow-auth (Wallow-vec7.1.4): a full
 * `<html>/<head>/<body>` wrapping the router `<Outlet/>` that child routes
 * render into.
 *
 * The shell stays free of router-context hooks (no `HeadContent`/`Scripts`, no
 * `Route.useRouteContext()`) so it can be server-rendered on its own; the head
 * is composed here directly instead. Two things it composes are worth calling
 * out, both added by Wallow-vec7.1.5:
 *
 *  - **Theme**: the fork's palette is emitted as CSS custom properties in a
 *    `<style>`, mirroring the `<HeadContent>` block in `AuthLayout.razor`. The
 *    stylesheet text is a plain string child (React escapes nothing into it and
 *    no markup is interpolated); it is generated from `api/branding.json` at
 *    build time and never from request input. `<html>` carries the fork's
 *    default colour scheme as a class so `.dark`/`.light` resolve with no
 *    client JS.
 *  - **Ready signal**: `<ReadyIndicator/>` sits at the root so *every* auth page
 *    emits it once hydrated. Blazor's equivalent lives in `AuthLayout.razor`
 *    because every auth page renders through that layout — the root route is
 *    this app's version of "every auth page".
 *
 * The per-client (`client_id`) branding overlay is resolved by the route that
 * renders `AuthLayout`; this shell shows the fork's, which is also the fallback
 * whenever no client is identified.
 */
function DocumentShell() {
  const branding: ResolvedBranding = forkResolvedBranding;

  return (
    <html lang="en" className={branding.defaultMode}>
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>{branding.name}</title>
        <link rel="icon" href={appIconUrl} />
        <style>{renderThemeStyle(branding)}</style>
        <script type="module" src={clientEntry} />
      </head>
      <body>
        <FocusOnNavigate />
        <Outlet />
        <ReadyIndicator />
      </body>
    </html>
  );
}

/**
 * The root route wraps the document shell in a `QueryClientProvider` so every
 * routed child can call React Query hooks.
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
