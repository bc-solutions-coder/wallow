import type { WallowSdk } from "@bc-solutions-coder/sdk";
import { createRootRouteWithContext, HeadContent, Outlet, Scripts } from "@tanstack/react-router";
import { type ReactElement, type ReactNode, useEffect } from "react";

// Side-effect import, NOT `?url` + a head() link: Start's own route manifest
// owns the <link> so both build environments agree on one asset.
import "../styles.css";

/** What the router hands every route through `Route.useRouteContext()`. */
export interface RouterContext {
  readonly sdk: WallowSdk;
}

/**
 * Stamps the `data-app-ready` hydration marker the E2E suites wait on. It must be written
 * from an effect — never SSR markup — so finding it proves React has committed and event
 * handlers are attached; a server-rendered attribute would satisfy the wait before the page
 * is interactive.
 */
function ReadyIndicator(): null {
  useEffect((): (() => void) => {
    document.body.dataset.appReady = "true";
    return (): void => {
      delete document.body.dataset.appReady;
    };
  }, []);
  return null;
}

function RootDocument({ children }: { readonly children: ReactNode }): ReactElement {
  return (
    <html lang="en">
      <head>
        <HeadContent />
      </head>
      <body>
        {children}
        <ReadyIndicator />
        <Scripts />
      </body>
    </html>
  );
}

export const Route = createRootRouteWithContext<RouterContext>()({
  head: () => ({
    meta: [
      // oxlint-disable-next-line unicorn/text-encoding-identifier-case -- `utf-8` is the canonical HTML charset declaration
      { charSet: "utf-8" },
      { name: "viewport", content: "width=device-width, initial-scale=1" },
      { title: "Example relying party" },
    ],
  }),
  shellComponent: RootDocument,
  component: Outlet,
});
