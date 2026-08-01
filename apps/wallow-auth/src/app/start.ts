import { withBasePath } from "@bc-solutions-coder/env/base-path";
import { resolveInternalOrigin } from "@bc-solutions-coder/env/internal-origin";
import { resolveRequestOrigin } from "@bc-solutions-coder/env/request-origin";
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { type ForkLinks, resolveForkLinks } from "@bc-solutions-coder/styles";
import { createMiddleware, createStart } from "@tanstack/react-start";

import { BASE_PATH } from "@shared/lib/base-path";

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

const sdkMiddleware = createMiddleware().server(({ next, request }) => {
  // The browser-facing base URL: this app proxies `/v1/**` under its own base
  // path, so the origin serving the page plus that prefix is where the API is
  // reachable. Under a based build the bare origin is whatever the ingress
  // serves at the root — a different app — so the prefix is not optional here.
  // Resolved through the helper so an HTTPS-terminating ingress does not leave
  // the SSR pass building `http` query keys the hydrating browser never matches.
  const requestOrigin: string = resolveRequestOrigin(request);

  const sdk: WallowSdk = createWallowSdk({
    baseUrl: withBasePath(requestOrigin, BASE_PATH),
    // No `requestOrigin` argument: both compose stacks publish this app on a
    // different host port than the one the container binds
    // (`127.0.0.1:5051:3002`), so self-fetching the browser's origin from inside
    // the container is ECONNREFUSED.
    internalOrigin: resolveInternalOrigin(process.env),
    cookieHeader: request.headers.get("cookie") ?? undefined,
  });

  // The fork's outbound links for THIS deployment. Same reason the origin
  // helpers take `process.env` as an argument: `@bc-solutions-coder/styles`
  // ships a prebuilt bundle, so a read inside it would answer with the library's
  // build environment. `__root.tsx` states the result in the document, which is
  // how the hydrating browser renders the same href — it has no environment of
  // its own to re-read.
  const forkLinks: ForkLinks = resolveForkLinks(process.env);

  return next({ context: { sdk, forkLinks } });
});

export const startInstance = createStart(() => ({
  requestMiddleware: [sdkMiddleware],
}));
