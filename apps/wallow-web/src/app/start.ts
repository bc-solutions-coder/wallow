import { resolveAuthUrl } from "@bc-solutions-coder/env/auth-origin";
import { resolveInternalOrigin } from "@bc-solutions-coder/env/internal-origin";
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import {
  createRequestOriginResolver,
  type PeerRequest,
} from "@bc-solutions-coder/sdk/server/forwarded";
import { type ForkLinks, resolveForkLinks } from "@bc-solutions-coder/styles";
import { createMiddleware, createStart } from "@tanstack/react-start";

/**
 * The Start instance — global request middleware that mints one SDK per request
 * and hands it down through the start context, which `getRouter()` lifts into
 * the router context.
 *
 * Per REQUEST, not per module: a module-global client would be shared by every
 * concurrent render in a server process, so configuring it per request would let
 * one user's forwarded cookie leak into another user's render. `createWallowSdk`
 * builds an instance owning its own client, cookie and interceptor list.
 *
 * Everything imported here lands in BOTH module graphs (Start aliases this file
 * as its entry for the client build too), so this file stays free of
 * `@bc-solutions-coder/sdk/server` and every other Node-only import. That is why
 * the origin resolver comes from the SDK's dependency-free
 * `./server/forwarded` subpath and the internal origin from
 * `@bc-solutions-coder/env` — neither reads any environment of its own: the one
 * `process.env` read is HERE, inside the server callback the browser never runs.
 */

/**
 * Where this app's API surface is mounted — the SDK's `WALLOW_API_MOUNT`, spelled
 * out rather than imported because that constant ships on the Node-only
 * `@bc-solutions-coder/sdk/server` entry and this module is isomorphic.
 */
const API_MOUNT = "/api";

/**
 * The origin resolver, bound lazily INSIDE the server callback rather than at
 * module scope like the `*.server.*` bindings: this file is in the client graph
 * too, so a module-scope `createRequestOriginResolver(process.env)` would run in
 * the browser. Memoized because parsing `WALLOW_TRUSTED_PROXIES` is start-up
 * work, not per-request work.
 */
let requestOriginFor: ((request: PeerRequest) => string) | undefined;

const sdkMiddleware = createMiddleware().server(({ next, request }) => {
  requestOriginFor ??= createRequestOriginResolver(process.env);
  // The browser-facing origin: this app answers its own BFF surface, so the
  // origin serving the page is also the origin the API proxy is reachable on.
  // Resolved through the helper so a trusted HTTPS-terminating ingress does not
  // leave the SSR pass building `http` query keys the hydrating browser never
  // matches — `x-forwarded-proto` is believed only from a peer inside
  // `WALLOW_TRUSTED_PROXIES`, the same gate the log ingest puts on
  // `x-forwarded-for`.
  const requestOrigin: string = requestOriginFor(request);
  // No `requestOrigin` argument: `docker/docker-compose.test.yml` publishes this
  // app as `127.0.0.1:5053:3000`, so self-fetching the browser's origin from
  // inside the container is ECONNREFUSED and every SSR'd page falls back to an
  // error boundary.
  const internalOrigin: string | undefined = resolveInternalOrigin(process.env);
  const cookieHeader: string | undefined = request.headers.get("cookie") ?? undefined;

  const sdk: WallowSdk = createWallowSdk({
    // Not the bare origin (which is what a pure passthrough app like wallow-auth
    // passes): this app's API surface is the BFF's `/api` mount, which strips
    // the prefix and forwards upstream with the session's bearer attached.
    baseUrl: `${requestOrigin}${API_MOUNT}`,
    internalOrigin,
    cookieHeader,
  });

  // The fork's outbound links for THIS deployment. Same reason the origin
  // helpers take `process.env` as an argument: `@bc-solutions-coder/styles`
  // ships a prebuilt bundle, so a read inside it would answer with the library's
  // build environment. `__root.tsx`'s loader carries the result to the browser —
  // the hydrating client has no environment to re-read it from, and an href that
  // differs between the two renders is a hydration mismatch.
  const forkLinks: ForkLinks = resolveForkLinks(process.env);

  // The sign-in app's public origin, for the same reason and by the same route:
  // resolved here, carried through the context, stated into the document by
  // `__root.tsx`, and read back by `@shared/lib/auth-url`.
  const authUrl: string = resolveAuthUrl(process.env);

  return next({ context: { sdk, forkLinks, authUrl } });
});

export const startInstance = createStart(() => ({
  requestMiddleware: [sdkMiddleware],
}));
