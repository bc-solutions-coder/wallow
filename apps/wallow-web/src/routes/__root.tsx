import { createRootRoute, Outlet } from "@tanstack/react-router";

/**
 * The root route owns the SSR document shell (Wallow-8w1h.2.2): a full
 * `<html>/<head>/<body>` wrapping the router `<Outlet/>` that child routes
 * render into.
 *
 * The shell is deliberately hook-free (no `HeadContent`/`Scripts`) so it can be
 * server-rendered on its own — those helpers require a live `RouterProvider`
 * context and belong with the client-hydration wiring in a later phase.
 */
function RootComponent() {
  return (
    <html lang="en">
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>Wallow</title>
      </head>
      <body>
        <Outlet />
      </body>
    </html>
  );
}

export const Route = createRootRoute({
  component: RootComponent,
});
