import type { WallowSdk } from "@bc-solutions-coder/sdk";
import {
  appIconUrl,
  forkResolvedBranding,
  renderThemeStyle,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";
import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, HeadContent, Outlet, Scripts } from "@tanstack/react-router";
import type { ReactElement, ReactNode } from "react";

// Side-effect import, NOT `?url` + a head() link: under Start's two-environment
// build a `?url` import resolved in the SSR graph yields a CSS hash the client
// build never emits. Same rule the workspace apps follow.
import "../styles.css";

/**
 * The fork's resolved palette/name, baked into the packed styles tarball from
 * `api/branding.json`. Reading it here is the smoke test for that bake: if the
 * JSON were resolved at consumer build time instead, this import would fail
 * outside the repo.
 */
const branding: ResolvedBranding = forkResolvedBranding;

/** What the router hands every route through `Route.useRouteContext()`. */
export interface RouterContext {
  readonly queryClient: QueryClient;
  readonly sdk: WallowSdk;
}

/** The document shell every route renders into. */
function RootDocument({ children }: { readonly children: ReactNode }): ReactElement {
  return (
    <html lang="en" className={branding.defaultMode}>
      <head>
        <HeadContent />
        {/* The fork's theme custom properties, inlined rather than taken from
            the ui package's <DocumentStyles/>: this app depends on the sdk and
            styles tarballs only. The CSS is build-time branding data, never
            request input. */}
        <style>{renderThemeStyle(branding)}</style>
      </head>
      <body>
        {children}
        <Scripts />
      </body>
    </html>
  );
}

export const Route = createRootRouteWithContext<RouterContext>()({
  head: () => ({
    meta: [
      // oxlint-disable-next-line unicorn/text-encoding-identifier-case -- `utf-8` is the canonical HTML charset declaration, and what every workspace app emits
      { charSet: "utf-8" },
      { name: "viewport", content: "width=device-width, initial-scale=1" },
      { title: branding.name },
    ],
    links: [{ rel: "icon", href: appIconUrl }],
  }),
  shellComponent: RootDocument,
  component: Outlet,
});
