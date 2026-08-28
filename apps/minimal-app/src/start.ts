import { type PeerRequest } from "@bc-solutions-coder/env/client-address";
import { resolveInternalOrigin } from "@bc-solutions-coder/env/internal-origin";
import { createRequestOriginResolver } from "@bc-solutions-coder/env/request-origin";
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createMiddleware, createStart } from "@tanstack/react-start";

/**
 * The Start instance — global request middleware that mints one SDK per request
 * and hands it down through the start context, which `getRouter()` lifts into
 * the router context.
 *
 * Per REQUEST, not per module: the SDK's module-global client is shared by every
 * concurrent render in a server process, so configuring it per request would let
 * one user's forwarded cookie leak into another user's render. `createWallowSdk`
 * builds an instance owning its own client, cookie and interceptor list.
 *
 * Everything imported here lands in BOTH module graphs (Start aliases this file
 * as its entry for the client build too), so this file stays free of
 * `@bc-solutions-coder/sdk/server` and every other Node-only import. That is why
 * the two origin helpers come from `@bc-solutions-coder/env`, whose subpaths
 * declare no dependencies and read no environment of their own: the one
 * `process.env` read is HERE, inside the server callback the browser never runs.
 */

/**
 * The origin resolver, bound lazily INSIDE the server callback rather than at
 * module scope: this file is in the client graph too, so a module-scope
 * `createRequestOriginResolver(process.env)` would run in the browser. Memoized
 * because parsing `WALLOW_TRUSTED_PROXIES` is start-up work, not per-request
 * work.
 */
let requestOriginFor: ((request: PeerRequest) => string) | undefined;

const sdkMiddleware = createMiddleware().server(({ next, request }) => {
  requestOriginFor ??= createRequestOriginResolver(process.env);
  // The browser-facing origin: this app proxies `/v1/**` at its own root, so the
  // origin serving the page is also the origin the API is reachable on. Resolved
  // through the helper so a trusted HTTPS-terminating ingress
  // (`WALLOW_TRUSTED_PROXIES`) does not leave the SSR pass building `http` query
  // keys the hydrating browser never matches.
  const requestOrigin: string = requestOriginFor(request);

  const sdk: WallowSdk = createWallowSdk({
    baseUrl: requestOrigin,
    // No `requestOrigin` argument: the browser's origin is exactly the address a
    // container publishing a different host port cannot reach itself on.
    internalOrigin: resolveInternalOrigin(process.env),
    cookieHeader: request.headers.get("cookie") ?? undefined,
  });

  return next({ context: { sdk } });
});

export const startInstance = createStart(() => ({
  requestMiddleware: [sdkMiddleware],
}));
