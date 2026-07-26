/**
 * Standalone SSR + reverse-proxy host for wallow-auth (Wallow-vec7.1.4,
 * SSR wired in Wallow-vec7.5.1.1; host runtime extracted to
 * `@bc-solutions-coder/web-shell` in Wallow-0q2s.8.3).
 *
 * `pnpm start` runs this (`tsx server.ts`) — the host the Dockerfile/E2E
 * container runs. All host behavior lives in {@link createStandaloneHost}; this
 * file only supplies wallow-auth's proxy topology:
 *
 *  1. `/health`, `/v1/**`, `/connect/**`, `/.well-known/**` (`isProxyRequest`) — bridged to the h3
 *     proxy app built by {@link createAuthServer}, so `/health` returns `ready`
 *     and the API surface is reverse-proxied to Wallow.Api with per-request
 *     cookie passthrough. Auth also forwards the peer socket address via
 *     {@link CLIENT_IP_HEADER}.
 *  2. Built browser assets out of `dist/client`.
 *  3. Everything else — server-rendered by the built SSR entry.
 */
import { CLIENT_IP_HEADER, createAuthServer, type AuthServer } from "./src/lib/auth-server";
import { isProxyRequest } from "./src/lib/proxy-paths";
import { createStandaloneHost, type ShellConfig } from "@bc-solutions-coder/web-shell/server";

const authServer: AuthServer = createAuthServer();

const config: ShellConfig = {
  appName: "wallow-auth",
  defaultPort: "3002",
  appDir: import.meta.dirname,
  isProxyPath: isProxyRequest,
  handleProxy: (request: Request): Promise<Response> => authServer.handle(request),
  clientIpHeader: CLIENT_IP_HEADER,
};

await createStandaloneHost(config);
