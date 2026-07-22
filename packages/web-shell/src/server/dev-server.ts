/**
 * TanStack Start SSR dev-server factory shared by both apps' dev-server.ts
 * (Wallow-0q2s.8.4). Extracted from the near-duplicate apps/wallow-auth/dev-server.ts
 * and apps/wallow-web/dev-server.ts, parameterized by the proxy topology via
 * {@link DevServerConfig}.
 *
 * Unlike the standalone host (which serves a PRE-BUILT `dist/`), this factory
 * drives a live Vite dev server in `middlewareMode` and pulls both the render
 * entry (`/src/ssr.tsx`) and the proxy bridge through `ssrLoadModule`, keeping the
 * whole render path on ONE React copy and re-reading the module graph per request
 * (no refresh-server restart needed).
 *
 * It answers, in order:
 *   1. Proxy paths (`config.isProxyPath` — `/health` plus the app's API/BFF
 *      surface) — dispatched to `config.loadProxyHandler(vite)`.
 *   2. Everything else — offered to Vite's own middlewares (which serve the
 *      browser bundle + module graph in dev), falling through to router SSR when
 *      Vite does not own the path.
 *
 * `@vitejs/plugin-react` is registered in the dev Vite instance ONLY when
 * `config.reactPluginInDev` is true. Both apps pass it false today: the plugin's
 * sole dev addition is React Fast Refresh, whose `head-prepend` preamble breaks
 * whole-document hydration (the root route renders `<html>`/`<head>` itself). The
 * field is kept for future apps whose document head is owned by `<Scripts/>`.
 * `wallowStyles()` is ALWAYS spread in, or the `styles.css` entry is served with
 * `@import "tailwindcss"` verbatim and every screen renders unstyled.
 */
import {
  createServer as createHttpServer,
  type IncomingMessage,
  type Server,
  type ServerResponse,
} from "node:http";
import { join } from "node:path";
import { Readable } from "node:stream";

import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { tanstackRouter } from "@tanstack/router-plugin/vite";
import react from "@vitejs/plugin-react";
import { type InlineConfig, type PluginOption, type ViteDevServer } from "vite";

/**
 * The per-app seam over the dev server. Mirrors {@link ShellConfig}'s shared
 * fields (appName, defaultPort, appDir, isProxyPath, reactPluginInDev); the proxy
 * seam differs from the standalone host because dev loads the bridge THROUGH Vite
 * (`ssrLoadModule`) rather than importing it directly.
 */
export interface DevServerConfig {
  /** App identity, used in the boot log (e.g. "wallow-auth"). */
  appName: string;
  /** Port used when `PORT` is unset in the environment (auth "3002", web "3000"). */
  defaultPort: string;
  /** Absolute Vite root of the app whose dev server this is — its `import.meta.dirname`. */
  appDir: string;
  /** True for paths the proxy owns (`/health` plus the app's API/BFF surface). */
  isProxyPath: (pathname: string) => boolean;
  /**
   * Loads the app's proxy bridge THROUGH the given Vite dev server and returns a
   * request handler. Called on EVERY proxy-prefixed request; the app decides
   * whether to memoize — wallow-auth caches its `AuthServer` instance across
   * calls, wallow-web loads `handleBffRequest` fresh each call. The factory holds
   * no memoization slot of its own.
   */
  loadProxyHandler: (vite: ViteDevServer) => Promise<(request: Request) => Promise<Response>>;
  /**
   * Whether the dev Vite instance registers `@vitejs/plugin-react` (Fast Refresh).
   * Both apps pass false today (whole-document hydration); kept for future apps.
   */
  reactPluginInDev?: boolean;
  /**
   * Optional internal seam header, stamped with the peer socket address on every
   * proxied request (wallow-auth's `CLIENT_IP_HEADER`). Omitted for wallow-web,
   * whose topology does not forward a client IP. Mirrors {@link ShellConfig}'s
   * `clientIpHeader` so both hosts share one config shape.
   */
  clientIpHeader?: string;
}

/** Injectable dependencies, used to drive the factory in tests without a real Vite boot. */
export interface CreateDevServerDeps {
  /** Overrides the real `vite.createServer`; defaults to it when omitted. */
  createViteServer?: (options: InlineConfig) => Promise<ViteDevServer>;
}

/** Shape of the Vite-loaded SSR entry (`src/ssr.tsx`). */
interface SsrModule {
  render: (request: Request) => Promise<Response>;
}

/** Adapt an incoming Node request into a WHATWG `Request`. When `clientIpHeader`
 * is set, stamp it with the peer's socket address exactly as the standalone host
 * does (standalone-host.ts:154-156). */
function toWebRequest(req: IncomingMessage, port: number, clientIpHeader?: string): Request {
  const authority: string = req.headers.host ?? `localhost:${port}`;
  const url: URL = new URL(req.url ?? "/", `http://${authority}`);

  const headers: Headers = new Headers();
  for (const [key, value] of Object.entries(req.headers)) {
    if (Array.isArray(value)) {
      for (const item of value) {
        headers.append(key, item);
      }
    } else if (value !== undefined) {
      headers.set(key, value);
    }
  }

  // Stamp the immediate peer's socket address into the internal seam header so
  // the proxy can append it to X-Forwarded-For (the WHATWG Request it hands the
  // proxy carries no socket). Always OVERWRITTEN from the socket so this hop's
  // entry cannot be forged by an inbound header. Only wallow-auth's topology
  // forwards a client IP, so this is gated on `config.clientIpHeader`.
  if (clientIpHeader !== undefined) {
    headers.set(clientIpHeader, req.socket.remoteAddress ?? "");
  }

  const method: string = req.method ?? "GET";
  const hasBody: boolean = method !== "GET" && method !== "HEAD";

  return new Request(url, {
    method,
    headers,
    ...(hasBody ? { body: Readable.toWeb(req) as ReadableStream<Uint8Array>, duplex: "half" } : {}),
  });
}

/** Copy a WHATWG `Response`'s status, headers (incl. multiple `Set-Cookie`),
 * and body onto the Node response. Dev never serves binary assets — Vite's own
 * middlewares do — so the body is read as text. */
async function writeWebResponse(res: ServerResponse, response: Response): Promise<void> {
  res.statusCode = response.status;
  response.headers.forEach((value: string, key: string): void => {
    if (key.toLowerCase() !== "set-cookie") {
      res.setHeader(key, value);
    }
  });
  for (const cookie of response.headers.getSetCookie()) {
    res.appendHeader("set-cookie", cookie);
  }
  const body: string = response.body === null ? "" : await response.text();
  res.end(body);
}

/** Default Vite boot: dynamically imports `vite` so the factory does not pull it
 * into its static import graph (and so tests can inject a fake instead). */
async function bootViteServer(options: InlineConfig): Promise<ViteDevServer> {
  const { createServer } = await import("vite");
  return createServer(options);
}

/**
 * Build and start the Vite-backed SSR dev server. Resolves to the listening Node
 * HTTP server so callers can inspect its address and close it.
 */
export async function createDevServer(
  config: DevServerConfig,
  deps: CreateDevServerDeps = {},
): Promise<Server> {
  const port: number = Math.trunc(Number(process.env.PORT ?? config.defaultPort));
  const host: string = process.env.HOST ?? "0.0.0.0";
  const createViteServer: (options: InlineConfig) => Promise<ViteDevServer> =
    deps.createViteServer ?? bootViteServer;

  // configFile: false means the app's vite.config.ts is not read, so anything the
  // module graph needs from Vite must be re-declared here. `@vitejs/plugin-react`
  // (whose sole dev addition is Fast Refresh) is registered ONLY when
  // `reactPluginInDev` is true — both apps pass false so their whole-document
  // hydration is not broken by the Fast Refresh `head-prepend` preamble.
  // `wallowStyles()` is ALWAYS spread in, or the `styles.css` entry is served with
  // `@import "tailwindcss"` verbatim and every screen renders unstyled.
  const plugins: PluginOption[] = [
    tanstackRouter({
      target: "react",
      routesDirectory: join(config.appDir, "src", "routes"),
      generatedRouteTree: join(config.appDir, "src", "routeTree.gen.ts"),
      autoCodeSplitting: false,
    }),
    ...(config.reactPluginInDev === true ? [react()] : []),
    ...wallowStyles(),
  ];

  const vite: ViteDevServer = await createViteServer({
    configFile: false,
    root: config.appDir,
    appType: "custom",
    plugins,
    server: { middlewareMode: true },
  });

  /** Answer an API/BFF-surface request from the proxy bridge, loaded THROUGH Vite
   * on EVERY request — the factory holds no memoization slot; the app owns that. */
  async function handleProxy(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const handler: (request: Request) => Promise<Response> = await config.loadProxyHandler(vite);
    await writeWebResponse(res, await handler(toWebRequest(req, port, config.clientIpHeader)));
  }

  /** Server-render the router's matched route for this request. */
  async function handleSsr(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const { render }: SsrModule = (await vite.ssrLoadModule("/src/ssr.tsx")) as SsrModule;
    await writeWebResponse(res, await render(toWebRequest(req, port, config.clientIpHeader)));
  }

  function onError(res: ServerResponse, error: unknown): void {
    vite.ssrFixStacktrace(error as Error);
    // eslint-disable-next-line no-console
    console.error("dev-server render error", error);
    if (!res.headersSent) {
      res.statusCode = 500;
    }
    res.end("Internal Server Error");
  }

  const server: Server = createHttpServer((req: IncomingMessage, res: ServerResponse): void => {
    const authority: string = req.headers.host ?? `localhost:${port}`;
    const pathname: string = new URL(req.url ?? "/", `http://${authority}`).pathname;

    if (config.isProxyPath(pathname)) {
      handleProxy(req, res).catch((error: unknown): void => {
        onError(res, error);
      });
      return;
    }

    // Everything else is offered to Vite's middlewares first, then falls through
    // to router SSR. This is what serves the browser bundle in dev out of the same
    // module graph the SSR render uses, so both sides share one React copy. Vite
    // calls `next()` for anything it does not own, which is every real route.
    vite.middlewares(req, res, (): void => {
      handleSsr(req, res).catch((error: unknown): void => {
        onError(res, error);
      });
    });
  });

  await new Promise<void>((resolve): void => {
    server.listen(port, host, (): void => {
      // eslint-disable-next-line no-console
      console.log(`${config.appName} Start SSR dev server listening on http://${host}:${port}`);
      resolve();
    });
  });

  return server;
}
