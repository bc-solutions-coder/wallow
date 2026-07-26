/**
 * TanStack Start SSR dev server for wallow-web (Wallow-8w1h.2.2, extracted onto
 * the shared `createDevServer` factory in Wallow-0q2s.8.4).
 *
 * `pnpm dev` runs this: it boots a Vite `middlewareMode` HTTP listener that
 * server-renders the router's matched route for each request. Requests to the BFF
 * surface — `/health`, `/bff/*`, and `/api/**` (the shared topology in
 * `src/lib/proxy-topology.ts`) — are dispatched to `src/lib/bff-server.ts`'s
 * `handleBffRequest` (a web Request -> Response bridge over the same h3 app the
 * standalone `server.ts` builds); everything else falls through to the router SSR.
 * This keeps the whole BFF surface reachable at the exact URLs the C#
 * `BffFlowTests`/`DockerComposeFixture` assert, with no createServerFileRoute
 * dependency (Wallow-8w1h.2.3 spike).
 *
 * `@vitejs/plugin-react` is deliberately NOT wired in (`reactPluginInDev` left
 * false, mirroring wallow-auth): its only dev addition is React Fast Refresh,
 * whose `head-prepend` preamble breaks whole-document hydration (the root route
 * renders `<html>`/`<head>` itself). Vite's built-in esbuild still transforms the
 * JSX.
 *
 * The BFF module is loaded fresh on EVERY BFF-prefixed request (the factory holds
 * no memoization slot; wallow-web intentionally does not cache it), so plain
 * `pnpm dev` without BFF env still serves SSR — only BFF/api/health routes require
 * the OIDC env to be present.
 */
import { createDevServer, type DevServerConfig } from "@bc-solutions-coder/web-shell/server";
import { type ViteDevServer } from "vite";

import { isBffProxyPath } from "./src/lib/proxy-topology";

/** Shape of the Vite-loaded BFF bridge (`src/lib/bff-server.ts`). */
interface BffModule {
  handleBffRequest: (request: Request) => Promise<Response>;
}

const config: DevServerConfig = {
  appName: "wallow-web",
  defaultPort: "3000",
  appDir: import.meta.dirname,
  isProxyPath: isBffProxyPath,
  reactPluginInDev: false,
  loadProxyHandler: async (
    vite: ViteDevServer,
  ): Promise<(request: Request) => Promise<Response>> => {
    const { handleBffRequest }: BffModule = (await vite.ssrLoadModule(
      "/src/lib/bff-server.ts",
    )) as BffModule;
    return handleBffRequest;
  },
};

await createDevServer(config);
