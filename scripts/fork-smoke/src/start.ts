import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { createMiddleware, createStart } from "@tanstack/react-start";

/**
 * Global request middleware minting one SDK per request, exactly as a fork's own
 * `src/start.ts` does.
 *
 * This is the smoke test for the SDK's BROWSER entry (`@bc-solutions-coder/sdk`):
 * Start aliases this file into both module graphs, so if the packed tarball's
 * main entry pulled in anything Node-only the client build would fail here.
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
