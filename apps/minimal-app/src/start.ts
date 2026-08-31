import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import {
  createRequestOriginResolver,
  type PeerRequest,
} from "@bc-solutions-coder/sdk/server/forwarded";
import { createMiddleware, createStart } from "@tanstack/react-start";

/**
 * The Start instance — global request middleware that mints one SDK per request
 * and hands it down through the start context, which `getRouter()` lifts into
 * the router context.
 *
 * Per REQUEST, not per module: a module-global client would be shared by every
 * concurrent render in a server process, so configuring it per request would
 * let one user's forwarded cookie leak into another user's render.
 *
 * Everything imported here lands in BOTH module graphs (Start aliases this file
 * as its entry for the client build too), so this file stays free of
 * `@bc-solutions-coder/sdk/server` and every other Node-only import. The origin
 * resolver comes from the SDK's dependency-free `./server/forwarded` subpath,
 * and the one `process.env` read is inside the server callback the browser
 * never runs.
 */

/**
 * Where this app's API surface is mounted — the BFF's `/api` proxy, which
 * strips the prefix and forwards upstream with the session's bearer attached.
 */
const API_MOUNT = "/api";

/**
 * The origin resolver, bound lazily INSIDE the server callback rather than at
 * module scope: this file is in the client graph too. Memoized because parsing
 * `WALLOW_TRUSTED_PROXIES` is start-up work, not per-request work.
 */
let requestOriginFor: ((request: PeerRequest) => string) | undefined;

const sdkMiddleware = createMiddleware().server(({ next, request }) => {
  requestOriginFor ??= createRequestOriginResolver(process.env);
  // The browser-facing origin, resolved trusted-proxy aware so an HTTPS-
  // terminating ingress (`WALLOW_TRUSTED_PROXIES`) does not leave the SSR pass
  // fetching `http` URLs the hydrating browser never used.
  const requestOrigin: string = requestOriginFor(request);

  const sdk: WallowSdk = createWallowSdk({
    baseUrl: `${requestOrigin}${API_MOUNT}`,
    // SSR self-fetches loop back through this same process: a container
    // publishing a different host port cannot reach itself on the browser's
    // origin, so the server-side hop goes to the local listener instead.
    internalOrigin: `http://localhost:${process.env.PORT ?? "3010"}`,
    cookieHeader: request.headers.get("cookie") ?? undefined,
  });

  return next({ context: { sdk } });
});

export const startInstance = createStart(() => ({
  requestMiddleware: [sdkMiddleware],
}));
