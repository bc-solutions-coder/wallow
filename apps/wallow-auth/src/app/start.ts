import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createMiddleware, createStart } from "@tanstack/react-start";

import { BASE_PATH, withBasePath } from "./lib/base-path";
import { resolveRequestOrigin } from "./lib/request-origin";

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
 * `@bc-solutions-coder/sdk/server` and every other Node-only import. The
 * `process.env` reads sit inside the server callback, which the browser never
 * runs — which is also why the internal-origin resolution below is spelled out
 * rather than imported from the SDK's Node-only `resolveInternalOrigin`.
 */

/**
 * Origin the SSR pass reaches ITSELF on, when it differs from the browser-facing
 * one. Shared spelling with the SDK's `INTERNAL_ORIGIN_ENV_KEY` and the other
 * Start apps, so one knob covers every host rather than one per app.
 */
const INTERNAL_ORIGIN_ENV_KEY = "WALLOW_WEB_INTERNAL_URL";

/** Strip trailing slashes so the override composes with a path the same way an origin does. */
function normalizeOrigin(value: string): string {
  return value.replace(/\/+$/u, "");
}

/**
 * The origin this host can fetch itself on: the explicit override, else the port
 * it actually listens on. The request origin is deliberately NOT the fallback —
 * both compose stacks publish this app on a different host port than the one the
 * container binds (`127.0.0.1:5051:3002`), so self-fetching the browser's origin
 * from inside the container is ECONNREFUSED. Mirrors the SDK's
 * `resolveInternalOrigin` order, minus its `requestOrigin` arm.
 */
function resolveInternalOrigin(): string | undefined {
  const override: string | undefined = process.env[INTERNAL_ORIGIN_ENV_KEY];
  if (override !== undefined && override !== "") {
    return normalizeOrigin(override);
  }

  const port: string | undefined = process.env.PORT;
  return port !== undefined && /^\d+$/u.test(port) ? `http://localhost:${port}` : undefined;
}

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
    internalOrigin: resolveInternalOrigin(),
    cookieHeader: request.headers.get("cookie") ?? undefined,
  });

  return next({ context: { sdk } });
});

export const startInstance = createStart(() => ({
  requestMiddleware: [sdkMiddleware],
}));
