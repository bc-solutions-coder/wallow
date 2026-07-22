/**
 * Standalone SSR + BFF host for wallow-web (Wallow-ffpq.3.3; host runtime
 * extracted to `@bc-solutions-coder/web-shell` in Wallow-0q2s.8.3).
 *
 * `pnpm start` runs this (`tsx server.ts`) — the host the Dockerfile/E2E
 * container runs. All host behavior lives in {@link createStandaloneHost}; this
 * file only supplies wallow-web's proxy topology:
 *
 *  1. `/health`, `/bff/*`, `/api/**` (`isBffProxyPath`) — bridged to the h3 BFF
 *     app built by `src/lib/bff-server.ts`'s {@link handleBffRequest}, so
 *     `/health` returns `ok`, the OIDC tunnel is reachable, and `/api/**` is
 *     reverse-proxied to Wallow.Api with a Bearer token and silent refresh.
 *     Unlike wallow-auth, this topology forwards no client IP.
 *  2. Built browser assets out of `dist/client`.
 *  3. Everything else — server-rendered by the built SSR entry.
 */
import { handleBffRequest } from "./src/lib/bff-server";
import { isBffProxyPath } from "./src/lib/proxy-topology";
import { createStandaloneHost, type ShellConfig } from "@bc-solutions-coder/web-shell/server";

const config: ShellConfig = {
  appName: "wallow-web",
  defaultPort: "3000",
  appDir: import.meta.dirname,
  isProxyPath: isBffProxyPath,
  handleProxy: handleBffRequest,
};

await createStandaloneHost(config);
