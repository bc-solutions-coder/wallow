import type { WallowSdk } from "@bc-solutions-coder/sdk";
import {
  type ForkLinks,
  forkLinksScript,
  renderThemeStyle,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";
import {
  Card,
  DocumentStyles,
  FocusOnNavigate,
  MutedText,
  ThemeProvider,
  ThemeScript,
} from "@bc-solutions-coder/ui";
import type { QueryClient } from "@bc-solutions-coder/query";
import { createRootRouteWithContext, HeadContent, Outlet, Scripts } from "@tanstack/react-router";
import { getGlobalStartContext } from "@tanstack/react-start";
import type { ReactElement, ReactNode } from "react";

import { AuthLayout } from "@shared/components/auth-layout";
import { ReadyIndicator } from "@shared/components/ready-indicator";
import { ErrorPage } from "@features/error";
import { NotFoundPage } from "@features/not-found";
import { appIconUrl, forkResolvedBranding } from "@shared/lib/branding";
import { forkLinks } from "@shared/lib/fork-links";

// Side-effect import, NOT `?url` + a head() link. Start builds two Vite
// environments; a `?url` import resolved in the SSR graph yields a CSS hash the
// client build never emits, so the served page would link a stylesheet that
// 404s and every production page would ship unstyled. Imported for its side
// effect, Start's own route manifest owns the <link> and both environments
// agree on one asset.
import "../styles.css";

/**
 * The fork's own palette/name — build-time data from `packages/styles/branding.json`, never
 * request input. The per-client (`client_id`) branding overlay is resolved by the
 * route that renders {@link AuthLayout}; this shell shows the fork's, which is
 * also the fallback whenever no client is identified.
 */
const branding: ResolvedBranding = forkResolvedBranding;

/** What the router hands every route through `Route.useRouteContext()`. */
export interface RouterContext {
  readonly queryClient: QueryClient;
  readonly sdk: WallowSdk;
}

/**
 * The links this REQUEST resolved, off Start's global context, or `undefined`
 * with no request in scope. On the server `getGlobalStartContext()` THROWS
 * rather than answering `undefined` when no Start context surrounds the call —
 * a spec rendering the shell directly — and that is not an error here; in the
 * browser there is no request at all, and the fallback below is the value this
 * same script already published.
 */
function requestForkLinks(): ForkLinks | undefined {
  try {
    return getGlobalStartContext()?.forkLinks;
  } catch {
    return undefined;
  }
}

/**
 * The document shell for wallow-auth: the full `<html>/<head>/<body>` every auth
 * page renders into.
 *
 * `<HeadContent/>` emits the `head()` output of the whole matched route branch
 * (this route's title/meta/icon plus anything a child adds) and `<Scripts/>`
 * emits the client entry — neither is hand-written any more, and the
 * `import.meta.env.DEV` branches that used to pick a dev-vs-built script and
 * stylesheet path are gone with the standalone host that needed them.
 *
 * `<DocumentStyles/>` still carries the fork's theme custom properties (mirroring
 * the `<HeadContent>` block the Blazor `AuthLayout.razor` had), with a null href
 * because the stylesheet link is Start's job now. `<html>` carries the fork's
 * default colour scheme as a class so `.dark`/`.light` resolve with no client JS
 * — the server's best guess, which `<ThemeScript/>` corrects before first paint.
 * It matters more on this origin than on wallow-web: login is the first page a
 * visitor sees and the page a cross-origin OIDC hop lands on, so a visitor who
 * has chosen a theme must not be bounced back to the fork default on the way
 * through. `<ThemeProvider/>` publishes what the script decided to the tree,
 * which is what {@link AuthLayout}'s toggle reads and writes.
 *
 * `<ReadyIndicator/>` sits at the root so *every* auth page emits the hydration
 * marker the Playwright suites wait on.
 */
function RootDocument({ children }: { readonly children: ReactNode }): ReactElement {
  return (
    <html lang="en" className={branding.defaultMode}>
      <head>
        <HeadContent />
        <DocumentStyles themeCss={renderThemeStyle(branding)} stylesheetHref={null} />
        <ThemeScript defaultMode={branding.defaultMode} />
        <script>{forkLinksScript(requestForkLinks() ?? forkLinks())}</script>
      </head>
      <body>
        <FocusOnNavigate />
        <ThemeProvider defaultMode={branding.defaultMode}>{children}</ThemeProvider>
        <ReadyIndicator />
        <Scripts />
      </body>
    </html>
  );
}

/**
 * Root error boundary. The deleted standalone host owned error rendering, so
 * without this an uncaught render error would reach the browser as a blank
 * document.
 *
 * It renders {@link ErrorPage} with NO `reason`, which is that component's
 * generic arm. The thrown error's message is deliberately not shown: this is an
 * unauthenticated, publicly reachable surface, and a render error's message can
 * carry internals (or attacker-influenced text) that an auth screen must not
 * echo — the same refusal rule the `?reason=` mapping exists to enforce.
 */
function RootErrorBoundary(): ReactElement {
  return (
    <AuthLayout>
      <ErrorPage />
    </AuthLayout>
  );
}

/**
 * The screen for a URL no route claims (Wallow-ffpq.2.7).
 *
 * Registered on the ROOT route rather than as the router's
 * `defaultNotFoundComponent` so it renders in place of the shell's `children`: a
 * 404 keeps the head, theme, `<FocusOnNavigate/>` and `<ReadyIndicator/>` every
 * other page gets — the response is a page of this app, not a host fallthrough.
 *
 * `AuthLayout` gets no `branding` prop, so it falls back to the fork's own — the
 * same choice as `/error` and `/reset-password`. Nothing has identified a client
 * on a path that matched no route.
 *
 * The status code is not set here: Start derives the 404 from the router's
 * not-found state.
 */
function RootNotFound(): ReactElement {
  return (
    <AuthLayout>
      <NotFoundPage />
    </AuthLayout>
  );
}

/** Shown while a route's loaders resolve, branded to match every other screen. */
function RootPending(): ReactElement {
  return (
    <AuthLayout>
      <Card>
        <MutedText data-testid="root-pending">Loading…</MutedText>
      </Card>
    </AuthLayout>
  );
}

export const Route = createRootRouteWithContext<RouterContext>()({
  head: () => ({
    meta: [
      // oxlint-disable-next-line unicorn/text-encoding-identifier-case -- `utf-8` is the canonical HTML charset declaration, and what the other Wallow apps emit
      { charSet: "utf-8" },
      { name: "viewport", content: "width=device-width, initial-scale=1" },
      { title: branding.name },
    ],
    links: [{ rel: "icon", href: appIconUrl }],
  }),
  shellComponent: RootDocument,
  component: Outlet,
  errorComponent: RootErrorBoundary,
  notFoundComponent: RootNotFound,
  pendingComponent: RootPending,
});
