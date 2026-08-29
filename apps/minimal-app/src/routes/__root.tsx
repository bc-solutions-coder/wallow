import type { WallowSdk } from "@bc-solutions-coder/sdk";
import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, HeadContent, Outlet, Scripts } from "@tanstack/react-router";
import type { ReactElement, ReactNode } from "react";

import "../styles.css";

export interface RouterContext {
  readonly queryClient: QueryClient;
  readonly sdk: WallowSdk;
}

function RootDocument({ children }: { readonly children: ReactNode }): ReactElement {
  return (
    <html lang="en">
      <head>
        <HeadContent />
      </head>
      <body data-app-ready="true">
        {children}
        <Scripts />
      </body>
    </html>
  );
}

export const Route = createRootRouteWithContext<RouterContext>()({
  head: () => ({
    meta: [
      { charSet: "utf8" },
      { name: "viewport", content: "width=device-width, initial-scale=1" },
      { title: "Example RP" },
    ],
  }),
  shellComponent: RootDocument,
  component: Outlet,
});
