/**
 * TanStack Start SSR dev server for wallow-auth (Wallow-vec7.1.4, extracted onto
 * the shared `createDevServer` factory in Wallow-0q2s.8.4).
 *
 * `pnpm dev` runs this: it boots a Vite `middlewareMode` HTTP listener that
 * server-renders the router's matched route for each request (or, for `/`, the
 * redirect to `/login`). Requests to the API surface — `/health`, `/v1/**`,
 * `/connect/**`, and `/.well-known/**` (the shared topology in
 * `src/lib/proxy-paths.ts`) — are dispatched to the reverse-proxy bridge in
 * `src/lib/auth-server.ts` instead of the router; everything else is offered to
 * Vite's middlewares and falls through to router SSR.
 *
 * `@vitejs/plugin-react` is deliberately NOT wired in (`reactPluginInDev` left
 * false). The plugin's only dev addition is React Fast Refresh, whose preamble
 * Vite injects at `head-prepend`; this app hydrates the WHOLE document (the root
 * route renders `<html>`/`<head>` itself — routes/__root.tsx), so a prepended
 * node shifts what React hydrates against and the readiness signal goes down with
 * it (Wallow-vec7.1.5). Trade-off: no HMR, so a dev edit needs a browser refresh;
 * SSR and the client graph are both re-read per request, so a refresh is all it
 * needs.
 *
 * The proxy bridge is loaded lazily on the first API-prefixed request and the
 * resulting `AuthServer` is memoised HERE (the factory holds no memoization slot),
 * so plain `pnpm dev` boots without needing the API reachable — only actual
 * `/v1`/`/connect` requests hit the upstream.
 */
import { createDevServer, type DevServerConfig } from "@bc-solutions-coder/web-shell/server";
import { type ViteDevServer } from "vite";

import { type AuthServer, type AuthServerConfig, CLIENT_IP_HEADER } from "./src/lib/auth-server";
import { isProxyRequest } from "./src/lib/proxy-paths";

/** Shape of the Vite-loaded proxy bridge (`src/lib/auth-server.ts`). */
interface AuthServerModule {
  createAuthServer: (config?: AuthServerConfig) => AuthServer;
}

/** Lazily-built, memoised proxy bridge (created on the first API request). */
let authServer: AuthServer | undefined;

const config: DevServerConfig = {
  appName: "wallow-auth",
  defaultPort: "3002",
  appDir: import.meta.dirname,
  isProxyPath: isProxyRequest,
  reactPluginInDev: false,
  clientIpHeader: CLIENT_IP_HEADER,
  loadProxyHandler: async (
    vite: ViteDevServer,
  ): Promise<(request: Request) => Promise<Response>> => {
    if (authServer === undefined) {
      const { createAuthServer }: AuthServerModule = (await vite.ssrLoadModule(
        "/src/lib/auth-server.ts",
      )) as AuthServerModule;
      authServer = createAuthServer();
    }
    const server: AuthServer = authServer;
    return (request: Request): Promise<Response> => server.handle(request);
  },
};

await createDevServer(config);
