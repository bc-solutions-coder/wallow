import { isApiFailure } from "@bc-solutions-coder/api-errors";
import { authUrlScript } from "@bc-solutions-coder/env/auth-origin";
import type { QueryClient } from "@bc-solutions-coder/query";
import type { WallowSdk } from "@bc-solutions-coder/sdk";
import {
  appIconUrl,
  type ForkLinks,
  forkLinksScript,
  forkResolvedBranding,
  renderThemeStyle,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";
import {
  Card,
  CardTitle,
  DocumentStyles,
  FailureBanner,
  FailureMessagesProvider,
  FailureToaster,
  FocusOnNavigate,
  MutedText,
  ThemeProvider,
  ThemeScript,
} from "@bc-solutions-coder/ui";
import {
  createRootRouteWithContext,
  type ErrorComponentProps,
  HeadContent,
  Outlet,
  rootRouteId,
  Scripts,
  useMatch,
  useMatches,
  useRouter,
} from "@tanstack/react-router";
import { getGlobalStartContext } from "@tanstack/react-start";
import type { ReactElement, ReactNode } from "react";

import { PublicLayout } from "@shared/components/PublicLayout";
import { ReadyIndicator } from "@shared/components/ready-indicator";
import { authUrl } from "@shared/lib/auth-url";
import { failureMessages } from "@shared/lib/failure-messages";
import { forkLinks } from "@shared/lib/fork-links";

// Side-effect import, NOT `?url` + a head() link. Start builds two Vite
// environments; a `?url` import resolved in the SSR graph yields a CSS hash the
// client build never emits, so the served page would link a stylesheet that
// 404s and every production page would ship unstyled. Imported for its side
// effect, Start's own route manifest owns the <link> and both environments
// agree on one asset.
import "../styles.css";

/** The fork's own palette/name — build-time data from `packages/styles/branding.json`, never request input. */
const branding: ResolvedBranding = forkResolvedBranding;

/**
 * The measure/centring the three root boundaries share. It sits on the `Card`
 * itself rather than a wrapper `div` so each boundary stays two elements deep —
 * `PublicLayout > Card > text` — which is both what wallow-auth's root does and
 * what the `react/jsx-max-depth` budget allows.
 */
const BOUNDARY_CARD_CLASS = "max-w-2xl mx-auto my-16";

/** The one API status the root boundary renders as a page that does not exist. */
const NOT_FOUND_STATUS = 404;

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

/** The auth origin this REQUEST resolved, with the same fallback story as {@link requestForkLinks}. */
function requestAuthUrl(): string | undefined {
  try {
    return getGlobalStartContext()?.authUrl;
  } catch {
    return undefined;
  }
}

/**
 * The providers every page renders under — its own component so the shell's
 * `<body>` stays within the `react/jsx-max-depth` budget. Order matters: the
 * toaster reads the theme, so both it and the registry provider sit inside
 * `<ThemeProvider/>`.
 */
function RootProviders({ children }: { readonly children: ReactNode }): ReactElement {
  return (
    <ThemeProvider defaultMode={branding.defaultMode}>
      <FailureMessagesProvider registry={failureMessages}>{children}</FailureMessagesProvider>
      <FailureToaster />
    </ThemeProvider>
  );
}

/**
 * The document shell for wallow-web: the full `<html>/<head>/<body>` every page
 * renders into.
 *
 * `<HeadContent/>` emits the `head()` output of the whole matched route branch
 * (this route's title/meta/icon plus anything a child adds) and `<Scripts/>`
 * emits the client entry — neither is hand-written any more, and the
 * `import.meta.env.DEV` branches that used to pick a dev-vs-built script and
 * stylesheet path are gone with the standalone host that needed them.
 *
 * `<DocumentStyles/>` still carries the fork's theme custom properties, with a
 * null href because the stylesheet link is Start's job now. `<html>` carries the
 * fork's default colour scheme as a class so `.dark`/`.light` resolve with no
 * client JS — that is the server's best guess, and `<ThemeScript/>` is what
 * corrects it. The script blocks in `<head>` on purpose: the visitor's own
 * choice has to be on `document.documentElement` BEFORE first paint, or they see
 * the fork default flash past on every navigation into the app.
 * `<ThemeProvider/>` then publishes what the script decided to the tree, so any
 * `ThemeToggle` below reads and writes one source of truth.
 *
 * The failure model's two root pieces sit inside it, composed in
 * {@link RootProviders}: `<FailureMessagesProvider/>` publishes the app registry
 * (`shared/lib/failure-messages.ts`) to every `FailureBanner` and
 * `useFailureMessage` below, and `<FailureToaster/>` is the one toaster the
 * query client's unhandled-failure callback speaks to. The toaster reads
 * `useTheme`, which is why it is inside the theme provider rather than beside it.
 *
 * `<ReadyIndicator/>` sits at the root so *every* page emits the hydration
 * marker the Playwright suites wait on.
 *
 * The last two scripts publish this deployment's outbound fork links and its
 * sign-in app's origin, resolved from the environment in `app/start.ts`. They
 * are here for the same reason `<ThemeScript/>` is: the browser cannot
 * re-derive the values (it has no environment to read) and an href that differs
 * across hydration is a mismatch, so the server states each answer in the
 * document and `forkLinks()` / `authUrl()` read it back — no context, no
 * provider, and nothing for React to re-render.
 */
function RootDocument({ children }: { readonly children: ReactNode }): ReactElement {
  return (
    <html lang="en" className={branding.defaultMode}>
      <head>
        <HeadContent />
        <DocumentStyles themeCss={renderThemeStyle(branding)} stylesheetHref={null} />
        <ThemeScript defaultMode={branding.defaultMode} />
        <script>{forkLinksScript(requestForkLinks() ?? forkLinks())}</script>
        <script>{authUrlScript(requestAuthUrl() ?? authUrl())}</script>
      </head>
      <body>
        <FocusOnNavigate />
        <RootProviders>{children}</RootProviders>
        <ReadyIndicator />
        <Scripts />
      </body>
    </html>
  );
}

/**
 * The screen for a URL no route claims.
 *
 * Registered on the ROOT route rather than as the router's
 * `defaultNotFoundComponent` so it renders in place of the shell's `children`: a
 * 404 keeps the head, theme, `<FocusOnNavigate/>` and `<ReadyIndicator/>` every
 * other page gets — the response is a page of this app, not a host fallthrough.
 *
 * The status code is not set here: Start derives the 404 from the router's
 * not-found state.
 */
function RootNotFound(): ReactElement {
  return (
    <BoundaryShell>
      <Card data-testid="root-not-found" className={BOUNDARY_CARD_CLASS}>
        <CardTitle>Page not found</CardTitle>
        <MutedText>There is nothing at this address.</MutedText>
      </Card>
    </BoundaryShell>
  );
}

/**
 * The chrome a boundary screen renders into. The boundary is also the router's
 * `defaultErrorComponent`, so it renders at the match that failed: directly
 * under the root that means painting the public shell, but below a layout
 * route (the dashboard) the layout is already on screen around this match, and
 * a second shell inside it would be wrong. Only an ancestor that renders
 * something counts: a pathless route with just a `beforeLoad` paints no chrome.
 */
function BoundaryShell({ children }: { readonly children: ReactNode }): ReactNode {
  const router = useRouter();
  const matchId: string = useMatch({ strict: false, select: (match) => match.id });
  const underLayout: boolean = useMatches({
    select: (matches) => {
      const index: number = matches.findIndex((match) => match.id === matchId);
      return matches
        .slice(0, Math.max(index, 0))
        .some(
          (match) =>
            match.routeId !== rootRouteId &&
            router.routesById[match.routeId]?.options.component !== undefined,
        );
    },
  });

  return underLayout ? children : <PublicLayout>{children}</PublicLayout>;
}

/** The card every root error renders into: the title is fixed, the body is the branch's. */
function RootErrorCard({ children }: { readonly children: ReactNode }): ReactElement {
  return (
    <BoundaryShell>
      <Card data-testid="root-error" className={BOUNDARY_CARD_CLASS}>
        <CardTitle>Something went wrong</CardTitle>
        {children}
      </Card>
    </BoundaryShell>
  );
}

/**
 * Root error boundary. The deleted standalone host owned error rendering, so
 * without this an uncaught render error would reach the browser as a blank
 * document.
 *
 * An API failure — a loader's read that came back a problem — is the failure
 * model's to render: a 404 is a page that does not exist, so it takes the
 * not-found screen; anything else gets a `FailureBanner`, whose "Try again"
 * invalidates the router so the loaders run again, and whose sign-in link (for
 * a missing session) and reference line are the banner's own. Registered on
 * the root AND as the router's `defaultErrorComponent`, so a server-rendered
 * loader failure lands here too (`app/router.tsx`).
 *
 * Everything else — a render bug — keeps fixed copy. The thrown error's message
 * is deliberately not shown. This origin serves anonymous visitors, and a render
 * error's message can carry internals (upstream URLs, session details) or
 * attacker-influenced text that a public page must not echo back.
 *
 * Exported for the router and its spec.
 */
export function RootErrorBoundary({ error }: ErrorComponentProps): ReactElement {
  const router = useRouter();

  if (!isApiFailure(error)) {
    return (
      <RootErrorCard>
        <MutedText>
          This page could not be loaded. Try again, or head back to the home page.
        </MutedText>
      </RootErrorCard>
    );
  }

  if (error.status === NOT_FOUND_STATUS) {
    return <RootNotFound />;
  }

  return (
    <RootErrorCard>
      <FailureBanner data-testid="root-failure" error={error} onRetry={() => router.invalidate()} />
    </RootErrorCard>
  );
}

/** Shown while a route's loaders resolve, branded to match every other screen. */
function RootPending(): ReactElement {
  return (
    <PublicLayout>
      <Card className={BOUNDARY_CARD_CLASS}>
        <MutedText data-testid="root-pending">Loading…</MutedText>
      </Card>
    </PublicLayout>
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
