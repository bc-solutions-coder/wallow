import {
  appIconUrl,
  forkResolvedBranding,
  renderThemeStyle,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";
import { DocumentStyles, FocusOnNavigate } from "@bc-solutions-coder/ui";
import { createQueryClient } from "@bc-solutions-coder/web-shell";
import { type QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createRootRouteWithContext, Outlet } from "@tanstack/react-router";
import { useRef } from "react";

import { ReadyIndicator } from "../components/ready-indicator";

/**
 * The browser bundle to load. In dev, Vite serves the entry from its module
 * graph at the source path; a production build emits it at `/client.js` (pinned
 * by the web-shell Vite preset). The tag is rendered by the React tree — server
 * and client alike — so both passes agree on it and hydration stays clean.
 * `import.meta.env.DEV` is substituted at build time in both graphs.
 */
const clientEntry: string = import.meta.env.DEV ? "/src/client.tsx" : "/client.js";

/**
 * The compiled stylesheet, or `null` when none should be linked. The production
 * build extracts the entry CSS imported by `client.tsx` to `/client.css`; the
 * shell must link it or every route serves unstyled. In dev the link must NOT
 * render — Vite injects the CSS through the JS module graph and `/client.css`
 * does not exist on the dev server.
 */
const stylesheetHref: string | null = import.meta.env.DEV ? null : "/client.css";

/**
 * The SSR document shell: a full `<html>/<head>/<body>` wrapping the router
 * `<Outlet/>` that child routes render into.
 *
 * The shell stays free of router-context hooks so it can be server-rendered on
 * its own; the head is composed here directly. The fork's palette is emitted as
 * CSS custom properties in a `<style>` (generated from `api/branding.json` at
 * build time, never from request input), and `<ReadyIndicator/>` sits at the
 * root so every page emits the readiness signal once hydrated.
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
        <DocumentStyles themeCss={renderThemeStyle(branding)} stylesheetHref={stylesheetHref} />
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
 * routed child can call React Query hooks. The provider's client is sourced
 * standalone-safely — a lazily-initialised ref that mints one stable client on
 * first render — rather than from the router context, so the shell still
 * server-renders on its own without a `RouterProvider`.
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

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  component: RootComponent,
});
