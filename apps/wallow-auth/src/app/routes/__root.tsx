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
import {
  type AuthorizeTransactionSearch,
  fetchAuthorizeContext,
  resolveTransactionBranding,
  type TransactionBranding,
} from "@shared/lib/authorize-context";
import { appIconUrl, forkResolvedBranding } from "@shared/lib/branding";
import { forkLinks } from "@shared/lib/fork-links";
import { requestWebAppUrl } from "@shared/lib/web-app-url.request";
import { webAppUrlScript } from "@shared/lib/web-app-url";

// Side-effect import, NOT `?url` + a head() link. Start builds two Vite
// environments; a `?url` import resolved in the SSR graph yields a CSS hash the
// client build never emits, so the served page would link a stylesheet that
// 404s and every production page would ship unstyled. Imported for its side
// effect, Start's own route manifest owns the <link> and both environments
// agree on one asset.
import "../styles.css";

/**
 * The fork's own palette/name — build-time data from `packages/styles/branding.json`, never
 * request input. The per-client branding overlay is resolved once by this
 * route's loader (see below); this shell shows the fork's, which is also the
 * fallback whenever no transaction identifies a client.
 */
const branding: ResolvedBranding = forkResolvedBranding;

/** What the router hands every route through `Route.useRouteContext()`. */
export interface RouterContext {
  readonly queryClient: QueryClient;
  readonly sdk: WallowSdk;
  /** The main app's public URL (`WALLOW_WEB_URL`), when the deployment named one. */
  readonly webAppUrl?: string;
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
        <ClientThemeStyle />
        <ThemeScript defaultMode={branding.defaultMode} />
        <script>{forkLinksScript(requestForkLinks() ?? forkLinks())}</script>
        <script>{webAppUrlScript(requestWebAppUrl())}</script>
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
 * The requesting client's curated theme, as a second `<style>` AFTER
 * {@link DocumentStyles} so its custom properties override the fork's at equal
 * specificity. It cannot ride `head()`'s `styles` array: `<HeadContent/>`
 * renders BEFORE `<DocumentStyles/>` (the charset meta must stay inside the
 * document's first kilobyte), so a head-emitted style would lose the cascade to
 * the fork theme.
 *
 * Rendered from the root loader's answer, so it exists exactly when a
 * transaction resolved a third-party client — the fork screens and every
 * fallback path (no transaction, first-party client, failed fetch) emit
 * nothing and keep the fork palette. `loaderData` is read defensively: the
 * shell also wraps {@link RootErrorBoundary}, which can render before or
 * without the loader's data.
 */
function ClientThemeStyle(): ReactElement | null {
  const loaderData = Route.useLoaderData() as RootLoaderData | undefined;
  const client: TransactionBranding | null = resolveTransactionBranding(
    loaderData?.authorizeContext,
  );

  return client === null ? null : <style>{renderThemeStyle(client.branding)}</style>;
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

/** What the root loader resolves for the whole matched branch. */
interface RootLoaderData {
  /** The pending transaction's client context, or `null` for fork chrome. */
  readonly authorizeContext: Awaited<ReturnType<typeof fetchAuthorizeContext>>;
}

/**
 * Narrow the raw search string down to the two parameters the context lookup
 * is keyed by. The root route declares no `validateSearch` — each screen owns
 * its own query-string contract — so the values arrive untyped and anything
 * non-string (TanStack's parser JSON-parses scalars) is treated as absent, the
 * same narrowing every route applies.
 */
function toTransactionSearch(search: unknown): AuthorizeTransactionSearch {
  const raw = search as Record<string, unknown>;

  return {
    returnUrl: typeof raw.returnUrl === "string" ? raw.returnUrl : undefined,
    scope: typeof raw.scope === "string" ? raw.scope : undefined,
  };
}

export const Route = createRootRouteWithContext<RouterContext>()({
  loaderDeps: ({ search }) => toTransactionSearch(search),
  /*
   * The ONE place the authorize transaction's client context is resolved
   * (issue #142): every in-transaction screen — and the document head and
   * theme — reads this answer rather than fetching per screen. The helper
   * returns `null` for anything that is not a transaction (wrong path, no
   * safe `/connect/authorize` returnUrl) and for every failure: branding is
   * chrome, and no fetch problem may block an auth screen.
   */
  loader: async ({ context, location, deps }): Promise<RootLoaderData> => {
    const authorizeContext = await fetchAuthorizeContext({
      queryClient: context.queryClient,
      client: context.sdk.client,
      pathname: location.pathname,
      search: deps,
    });

    return { authorizeContext };
  },
  head: ({ loaderData }) => {
    const client: TransactionBranding | null = resolveTransactionBranding(
      loaderData?.authorizeContext,
    );

    return {
      meta: [
        // oxlint-disable-next-line unicorn/text-encoding-identifier-case -- `utf-8` is the canonical HTML charset declaration, and what the other Wallow apps emit
        { charSet: "utf-8" },
        { name: "viewport", content: "width=device-width, initial-scale=1" },
        // A third-party transaction titles the tab for the client being signed
        // in to; everything else keeps the fork's name. The favicon below stays
        // the fork's either way — the ADDRESS BAR identity is the fork's, and a
        // client-supplied icon in it would be impersonation surface.
        { title: client === null ? branding.name : `Sign in · ${client.branding.name}` },
      ],
      links: [{ rel: "icon", href: appIconUrl }],
    };
  },
  shellComponent: RootDocument,
  component: Outlet,
  errorComponent: RootErrorBoundary,
  notFoundComponent: RootNotFound,
  pendingComponent: RootPending,
});
