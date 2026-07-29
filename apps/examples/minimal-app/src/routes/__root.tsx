import type { WallowSdk } from "@bc-solutions-coder/sdk";
import {
  appIconUrl,
  forkResolvedBranding,
  renderThemeStyle,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";
import {
  Card,
  CardTitle,
  CenteredCardLayout,
  DocumentStyles,
  FocusOnNavigate,
  MutedText,
} from "@bc-solutions-coder/ui";
import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, HeadContent, Outlet, Scripts } from "@tanstack/react-router";
import type { ReactElement, ReactNode } from "react";

import { ReadyIndicator } from "../components/ready-indicator";

// Side-effect import, NOT `?url` + a head() link. Start builds two Vite
// environments; a `?url` import resolved in the SSR graph yields a CSS hash the
// client build never emits, so the served page would link a stylesheet that
// 404s and every production page would ship unstyled. Imported for its side
// effect, Start's own route manifest owns the <link> and both environments
// agree on one asset.
import "../styles.css";

/** The fork's resolved palette/name — build-time data from `api/branding.json`, never request input. */
const branding: ResolvedBranding = forkResolvedBranding;

/** What the router hands every route through `Route.useRouteContext()`. */
export interface RouterContext {
  readonly queryClient: QueryClient;
  readonly sdk: WallowSdk;
}

/**
 * The document shell: the full `<html>/<head>/<body>` every route renders into.
 *
 * `<HeadContent/>` emits the `head()` output of the whole matched route branch
 * (this route's title/meta/icon plus anything a child adds) and `<Scripts/>`
 * emits the client entry — neither is hand-written any more, and there is no
 * `import.meta.env.DEV` branch picking a dev-vs-built asset path.
 *
 * `<DocumentStyles/>` still carries the fork's theme custom properties, with a
 * null href because the stylesheet link is Start's job now.
 */
function RootDocument({ children }: { readonly children: ReactNode }): ReactElement {
  return (
    <html lang="en" className={branding.defaultMode}>
      <head>
        <HeadContent />
        <DocumentStyles themeCss={renderThemeStyle(branding)} stylesheetHref={null} />
      </head>
      <body>
        <FocusOnNavigate />
        {children}
        <ReadyIndicator />
        <Scripts />
      </body>
    </html>
  );
}

/** Branded card used by all three boundaries below, so they cannot drift apart visually. */
function BoundaryCard({ title, detail }: { readonly title: string; readonly detail: string }) {
  return (
    <CenteredCardLayout data-testid="boundary-layout">
      <Card>
        <CardTitle data-testid="boundary-title">{title}</CardTitle>
        <MutedText data-testid="boundary-detail">{detail}</MutedText>
      </Card>
    </CenteredCardLayout>
  );
}

/**
 * Root error boundary. The deleted standalone host owned error rendering, so
 * without this an uncaught render error would reach the browser as a blank
 * document. Only the error's message is shown — never a stack — because this
 * component renders in production too.
 */
function RootErrorBoundary({ error }: { readonly error: Error }): ReactElement {
  return <BoundaryCard title="Something went wrong" detail={error.message} />;
}

/** Root not-found boundary, branded to match rather than the router's bare default. */
function RootNotFound(): ReactElement {
  return <BoundaryCard title="Page not found" detail="That page does not exist." />;
}

/** Shown while a route's loaders resolve. */
function RootPending(): ReactElement {
  return <BoundaryCard title={branding.name} detail="Loading…" />;
}

export const Route = createRootRouteWithContext<RouterContext>()({
  head: () => ({
    meta: [
      // oxlint-disable-next-line unicorn/text-encoding-identifier-case -- `utf-8` is the canonical HTML charset declaration, and what both product apps emit
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
