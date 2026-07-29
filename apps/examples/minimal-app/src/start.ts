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
 * `@bc-solutions-coder/sdk/server` and every other Node-only import. The one
 * `process.env` read sits inside the server callback, which the browser never
 * runs.
 */

/** Origin the SSR pass reaches ITSELF on, when it differs from the browser-facing one. */
const INTERNAL_ORIGIN_ENV_KEY = "WALLOW_WEB_INTERNAL_URL";

/** Strip trailing slashes so the override composes with a path the same way an origin does. */
function normalizeOrigin(value: string): string {
  return value.replace(/\/+$/u, "");
}

const sdkMiddleware = createMiddleware().server(({ next, request }) => {
  // The browser-facing origin: this app proxies `/v1/**` at its own root, so the
  // origin serving the page is also the origin the API is reachable on.
  const requestOrigin: string = new URL(request.url).origin;
  const override: string | undefined = process.env[INTERNAL_ORIGIN_ENV_KEY];

  const sdk: WallowSdk = createWallowSdk({
    baseUrl: requestOrigin,
    internalOrigin:
      override === undefined || override === "" ? undefined : normalizeOrigin(override),
    cookieHeader: request.headers.get("cookie") ?? undefined,
  });

  return next({ context: { sdk } });
});

export const startInstance = createStart(() => ({
  requestMiddleware: [sdkMiddleware],
}));
