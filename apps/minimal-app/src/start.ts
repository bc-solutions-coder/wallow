import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createMiddleware, createStart } from "@tanstack/react-start";

/**
 * One SDK instance per request, handed down through the Start context.
 *
 * PROTOTYPE NOTE: the two first-party apps resolve the request origin through
 * `@bc-solutions-coder/env` (trusted-proxy aware). That package is private, so
 * an external RP has only `request.url` — correct behind an ingress that
 * rewrites the URL, wrong behind one that only sets X-Forwarded-*. See the
 * README "Gaps" section.
 */
const sdkMiddleware = createMiddleware().server(({ next, request }) => {
  const sdk: WallowSdk = createWallowSdk({
    baseUrl: new URL(request.url).origin,
    cookieHeader: request.headers.get("cookie") ?? undefined,
  });
  return next({ context: { sdk } });
});

export const startInstance = createStart(() => ({
  requestMiddleware: [sdkMiddleware],
}));
