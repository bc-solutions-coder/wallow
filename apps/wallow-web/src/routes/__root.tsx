import { type QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, Outlet } from "@tanstack/react-router";

import { ReadyIndicator } from "../components/ready-indicator";
import {
  appIconUrl,
  forkResolvedBranding,
  renderThemeStyle,
  type ResolvedBranding,
} from "../lib/branding";
import { DocumentStyles, FocusOnNavigate } from "@bc-solutions-coder/ui";

/**
 * The browser bundle to load. In dev, Vite serves the entry straight out of its
 * module graph at its source path; a production build emits it at `/client.js`
 * (pinned by `vite.config.ts`).
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
 * The SSR document shell (Wallow-8w1h.2.2): a full `<html>/<head>/<body>`
 * wrapping the router `<Outlet/>` that child routes render into.
 *
 * The shell is deliberately hook-free of router context (no
 * `HeadContent`/`Scripts`, no `Route.useRouteContext()`) so it can be
 * server-rendered on its own — those helpers require a live `RouterProvider`
 * context and belong with the client-hydration wiring in a later phase.
 *
 * Two things it composes are the hydration wiring added by Wallow-ffpq.3.1:
 * the `<script type="module">` that loads the client bundle so the SSR'd HTML
 * hydrates at all, and the `<ReadyIndicator/>` at the root so *every* route
 * emits the `data-app-ready` signal once interactive.
 *
 * Adopting the shared styles package (Wallow-ffpq.3.4) adds two more, mirroring
 * wallow-auth's shell:
 *
 *  - **Theme**: the fork's resolved palette is emitted as CSS custom properties
 *    in a `<style>`, so every route resolves the `@theme` tokens the class names
 *    reference. The stylesheet text is a plain string child generated from
 *    api/branding.json at build time, never from request input; `<html>` carries
 *    the fork's default colour scheme as a class so `.dark`/`.light` resolve
 *    with no client JS.
 *  - **Favicon**: the fork's icon is served at the site root (`appIconUrl` is
 *    root-relative), so it resolves identically on every nested route.
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
 * The root route (Wallow-8w1h.3.1) renders the document shell directly.
 *
 * The `QueryClientProvider` no longer lives here: the router owns the single
 * per-request client and supplies it through its `Wrap` render-prop
 * (`router.tsx`), so loaders (router context) and components (React Query hooks)
 * share ONE cache (Wallow-evd5.2.4). The shell itself establishes no client, so
 * a standalone `renderToString(<Shell/>)` reads its `QueryClient` from whatever
 * provider the caller supplies.
 */
function RootComponent() {
  return <DocumentShell />;
}

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  component: RootComponent,
});
