import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, Outlet } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { ReadyIndicator } from "../components/ready-indicator";
import { NotFoundPage } from "../features/not-found/components/NotFoundPage";
import {
  appIconUrl,
  forkResolvedBranding,
  renderThemeStyle,
  type ResolvedBranding,
} from "../lib/branding";
import { DocumentStyles, FocusOnNavigate } from "@bc-solutions-coder/ui";

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
 * The stylesheet to link from the document head. The production build extracts
 * the entry CSS imported by `client.tsx` to `/client.css` (pinned by
 * `assetFileNames` in `vite.config.ts`), and nothing references it from
 * `client.js` — Vite does not auto-inject entry CSS for a JS entry — so the
 * shell must link it or every route serves unstyled. In dev the link targets
 * the source entry with Vite's `?direct` query, which serves the compiled CSS
 * as a plain stylesheet; without the link the first paint is unstyled until
 * the JS module graph loads and injects the CSS (FOUC). `client.tsx` still
 * imports the same file so CSS HMR keeps working — the HMR-injected copy is
 * appended after this link and wins the cascade. Same build-time
 * `import.meta.env.DEV` substitution as `clientEntry`, for the same
 * hydration-agreement reason.
 */
const stylesheetHref: string = import.meta.env.DEV ? "/src/styles.css?direct" : "/client.css";

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
 *    emits it once hydrated. The root route is this app's version of "every
 *    auth page" — the single shell every screen renders through.
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
 * The root route renders the document shell directly.
 *
 * The `QueryClientProvider` no longer lives here: the router owns the single
 * per-request client and supplies it through its `Wrap` render-prop
 * (`router.tsx`), so loaders (router context) and components (React Query hooks)
 * share ONE cache (Wallow-evd5.3.4). The shell itself establishes no client, so
 * a standalone `renderToString(<Shell/>)` reads its `QueryClient` from whatever
 * provider the caller supplies.
 */
function RootComponent() {
  return <DocumentShell />;
}

/**
 * The screen for a URL no route claims (Wallow-ffpq.2.7).
 *
 * Registered on the ROOT route rather than as the router's
 * `defaultNotFoundComponent` for two reasons. It renders in place of
 * `DocumentShell`'s `<Outlet/>`, so a 404 keeps the head, theme,
 * `<FocusOnNavigate/>`, and `<ReadyIndicator/>` every other page gets — the
 * response is a page of this app, not a host fallthrough. And `src/router.tsx` is
 * read as source TEXT by `src/router-codegen.test.ts`, which forbids any import
 * from `./routes/` there; wiring it here keeps the component next to the layout it
 * needs.
 *
 * `AuthLayout` gets no `branding` prop, so it falls back to the fork's own — the
 * same choice as `/error` and `/reset-password`. Nothing has identified a client
 * on a path that matched no route.
 *
 * The status code is not set here: TanStack's render handler derives the 404 from
 * the router's not-found state, and the standalone host propagates it verbatim.
 */
function NotFoundRoute() {
  return (
    <AuthLayout>
      <NotFoundPage />
    </AuthLayout>
  );
}

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  component: RootComponent,
  notFoundComponent: NotFoundRoute,
});
