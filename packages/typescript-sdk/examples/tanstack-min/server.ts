/**
 * Minimal BFF host for the @bc-solutions-coder/sdk TanStack Start reference example.
 *
 * Mounts the SDK's BFF tunnel handlers under `/bff/*`, the reverse API proxy
 * under `/api/**`, serves the static browser client from `public/`, and exposes
 * a `/health` liveness endpoint for the E2E `DockerComposeFixture`.
 *
 * The h3 handlers returned by `createBffHandlers`/`createApiProxy` are the same
 * `defineEventHandler` objects a TanStack Start server route would export, so
 * the mounting shape here mirrors the guide in README.md.
 */
import { createServer, type Server } from "node:http";
import { readFile } from "node:fs/promises";
import { dirname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";

import {
  createApiProxy,
  createBffHandlers,
  loadBffConfigFromEnv,
  type BffConfig,
} from "@bc-solutions-coder/sdk/server";
import {
  createApp,
  createRouter,
  defineEventHandler,
  getRequestPath,
  setResponseHeader,
  setResponseStatus,
  toNodeListener,
  type App,
  type H3Event,
  type Router,
} from "h3";

const currentDir: string = dirname(fileURLToPath(import.meta.url));
const publicDir: string = join(currentDir, "public");
const port: number = Number.parseInt(process.env.PORT ?? "3000", 10);

const contentTypes: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".map": "application/json; charset=utf-8",
  ".ico": "image/x-icon",
  ".json": "application/json; charset=utf-8",
};

const config: BffConfig = loadBffConfigFromEnv();
const bff: ReturnType<typeof createBffHandlers> = createBffHandlers(config);
const apiProxy: ReturnType<typeof createApiProxy> = createApiProxy(config);

const app: App = createApp();
const router: Router = createRouter();

// Liveness for the E2E DockerComposeFixture health wait.
router.get(
  "/health",
  defineEventHandler((): { status: string } => ({ status: "ok" })),
);

// BFF OIDC tunnel. `/bff/login` and `/bff/logout` issue redirects; `/bff/user`
// reflects the sealed session; `/bff/callback` completes the code exchange.
router.use("/bff/login", bff.login);
router.use("/bff/callback", bff.callback);
router.get("/bff/user", bff.user);
router.use("/bff/logout", bff.logout);

// Reverse proxy: everything under `/api` is forwarded to the downstream API
// with a Bearer token and silent refresh. The proxy strips the `/api` prefix
// itself, so the full request path must reach it intact.
router.use("/api/**", apiProxy);

app.use(router);

// Static client fallback: serve `public/` with `index.html` for the root.
app.use(
  defineEventHandler(async (event: H3Event): Promise<unknown> => {
    const requestPath: string = getRequestPath(event).split("?")[0];
    const relative: string =
      requestPath === "/" || requestPath === ""
        ? "index.html"
        : requestPath.replace(/^\/+/, "");

    // Contain path traversal within publicDir.
    const resolved: string = normalize(join(publicDir, relative));
    if (!resolved.startsWith(publicDir)) {
      setResponseStatus(event, 403);
      return "Forbidden";
    }

    try {
      const contents: Buffer = await readFile(resolved);
      const extension: string = resolved.slice(resolved.lastIndexOf("."));
      setResponseHeader(
        event,
        "content-type",
        contentTypes[extension] ?? "application/octet-stream",
      );
      return contents;
    } catch {
      setResponseStatus(event, 404);
      return "Not found";
    }
  }),
);

const server: Server = createServer(toNodeListener(app));
server.listen(port, (): void => {
  // eslint-disable-next-line no-console
  console.log(`tanstack-min BFF example listening on http://0.0.0.0:${port}`);
});
