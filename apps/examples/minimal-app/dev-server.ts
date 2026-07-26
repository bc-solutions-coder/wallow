/**
 * TanStack Start SSR dev server (`pnpm dev`), built on the shared
 * `createDevServer` factory. It boots a Vite `middlewareMode` HTTP listener that
 * server-renders the router's matched route per request; requests to the API
 * surface (`isProxyRequest` in `src/lib/proxy-paths.ts`) are dispatched to the
 * reverse-proxy bridge instead of the router.
 *
 * `@vitejs/plugin-react` is deliberately NOT wired in (`reactPluginInDev: false`).
 * Its only dev addition is React Fast Refresh, whose preamble Vite prepends to
 * `<head>`; this app hydrates the WHOLE document (the root route renders
 * `<html>`/`<head>` itself), so a prepended node shifts what React hydrates
 * against and breaks the readiness signal. Trade-off: no HMR, so a dev edit needs
 * a browser refresh.
 *
 * The proxy bridge is loaded lazily on the first API-prefixed request and the
 * resulting `ProxyServer` is memoised here, so a bare `pnpm dev` boots without
 * needing the API reachable — only actual `/v1`/`/connect` requests hit upstream.
 */
import { createDevServer, type DevServerConfig } from "@bc-solutions-coder/web-shell/server";
import { type ViteDevServer } from "vite";

import { isProxyRequest } from "./src/lib/proxy-paths";
import { type ProxyServer, type ProxyServerConfig } from "./src/lib/proxy-server";

/** Shape of the Vite-loaded proxy bridge (`src/lib/proxy-server.ts`). */
interface ProxyServerModule {
  createProxyServer: (config?: ProxyServerConfig) => ProxyServer;
}

/** Lazily-built, memoised proxy bridge (created on the first API request). */
let proxyServer: ProxyServer | undefined;

const config: DevServerConfig = {
  appName: "example-minimal-app",
  defaultPort: "3010",
  appDir: import.meta.dirname,
  isProxyPath: isProxyRequest,
  reactPluginInDev: false,
  loadProxyHandler: async (
    vite: ViteDevServer,
  ): Promise<(request: Request) => Promise<Response>> => {
    if (proxyServer === undefined) {
      const { createProxyServer }: ProxyServerModule = (await vite.ssrLoadModule(
        "/src/lib/proxy-server.ts",
      )) as ProxyServerModule;
      proxyServer = createProxyServer();
    }
    const server: ProxyServer = proxyServer;
    return (request: Request): Promise<Response> => server.handle(request);
  },
};

await createDevServer(config);
