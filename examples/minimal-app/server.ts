/**
 * Standalone SSR + reverse-proxy host (`pnpm start`) — the host a Dockerfile or
 * E2E container would run against the pre-built `dist/`. All host behavior lives
 * in {@link createStandaloneHost}; this file only supplies the app's proxy
 * topology (the one `ShellConfig` seam the design keeps per-app).
 */
import { createStandaloneHost, type ShellConfig } from "@bc-solutions-coder/web-shell/server";

import { isProxyRequest } from "./src/lib/proxy-paths";
import { createProxyServer, type ProxyServer } from "./src/lib/proxy-server";

const proxyServer: ProxyServer = createProxyServer();

const config: ShellConfig = {
  appName: "example-minimal-app",
  defaultPort: "3010",
  appDir: import.meta.dirname,
  isProxyPath: isProxyRequest,
  handleProxy: (request: Request): Promise<Response> => proxyServer.handle(request),
};

await createStandaloneHost(config);
