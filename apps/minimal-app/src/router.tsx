import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createRouter as createTanStackRouter } from "@tanstack/react-router";
import { getGlobalStartContext } from "@tanstack/react-start";

import { routeTree } from "./routeTree.gen";

/**
 * Where this app's API surface is mounted, as the BROWSER addresses it: the BFF
 * `/api` proxy on this same origin. Relative on purpose — the browser resolves
 * it against its own origin, and Node (where no `location` exists) never uses
 * this fallback off a request anyway.
 */
const BROWSER_API_BASE_URL = "/api";

/**
 * The request's SDK off Start's global context, or `undefined` when there is no
 * request in scope. On the server `getGlobalStartContext()` THROWS rather than
 * answering `undefined` when no Start context surrounds the call — which is not
 * an error here: it means "no request SDK to inherit", the browser's answer too.
 */
function readRequestSdk(): WallowSdk | undefined {
  try {
    return getGlobalStartContext()?.sdk;
  } catch {
    return undefined;
  }
}

/**
 * Build the router that boots the app. Start calls this ONCE PER REQUEST on the
 * server (and once on the client), so the SDK instance it lifts into the router
 * context is request-scoped: on the server it comes from the global request
 * middleware in `start.ts` — the only place the inbound cookie is in scope —
 * and in the browser it is a fresh same-origin instance against the BFF proxy.
 *
 * The route tree is codegen'd into `src/routeTree.gen.ts` by the Start plugin
 * as a side effect of `vite dev` / `vite build`.
 */
export function getRouter() {
  const sdk: WallowSdk = readRequestSdk() ?? createWallowSdk({ baseUrl: BROWSER_API_BASE_URL });

  return createTanStackRouter({
    routeTree,
    context: { sdk },
    scrollRestoration: true,
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof getRouter>;
  }
}
