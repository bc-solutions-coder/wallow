/**
 * TanStack Start SSR dev server for wallow-auth (Wallow-vec7.1.4).
 *
 * `pnpm dev` runs this: it boots an HTTP listener that server-renders the
 * router's matched route for each request and returns the SSR HTML shell (or,
 * for `/`, the redirect to `/login`). Requests to the API surface — `/health`,
 * `/v1/**`, `/connect/**`, and `/.well-known/**` (the shared topology in
 * `src/lib/proxy-paths.ts`) — are dispatched to the reverse-proxy bridge in
 * `src/lib/auth-server.ts` (task 0.3) instead of the router. Everything else is
 * offered to Vite's own middlewares — which serve the browser bundle and its
 * module graph in dev — and falls through to the router SSR when Vite does not
 * own the path.
 *
 * We run Vite in `middlewareMode` and pull the render entry via `ssrLoadModule`,
 * which keeps the whole render path on one React copy. JSX is transformed by
 * Vite's built-in esbuild pass, which picks up `jsx: "react-jsx"` from the
 * tsconfig `extends` chain — the standalone `tsx` runner is what could not, and
 * the reason this file drives Vite at all.
 *
 * `@vitejs/plugin-react` is deliberately NOT wired in here, though the
 * production build (vite.config.ts) does use it. The plugin's only addition in
 * dev is React Fast Refresh, and Fast Refresh needs a preamble that Vite injects
 * via `transformIndexHtml` at `head-prepend`. This app hydrates the WHOLE
 * document — the root route renders `<html>`/`<head>` itself (routes/__root.tsx)
 * — so anything injected into `<head>` shifts the very nodes React is trying to
 * hydrate against and the hydration mismatches, taking the readiness signal down
 * with it (Wallow-vec7.1.5). Trade-off: no HMR, so a dev edit needs a browser
 * refresh; SSR and the client graph are both re-read per request, so a refresh
 * is all it needs. Fast Refresh comes back when the TanStack Start vite plugin
 * lands and `<Scripts/>`/`<HeadContent/>` own the document head.
 *
 * The proxy bridge is imported lazily on the first API-prefixed request and the
 * resulting `AuthServer` is memoised, so plain `pnpm dev` boots without needing
 * the API reachable — only actual `/v1`/`/connect` requests hit the upstream.
 */
import {
  createServer as createHttpServer,
  type IncomingMessage,
  type Server,
  type ServerResponse,
} from "node:http";
import { Readable } from "node:stream";

import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { createServer as createViteServer, type ViteDevServer } from "vite";

import { CLIENT_IP_HEADER, type AuthServer, type AuthServerConfig } from "./src/lib/auth-server";
import { isProxyRequest } from "./src/lib/proxy-paths";

const DEFAULT_PORT = "3002";
const port: number = Math.trunc(Number(process.env.PORT ?? DEFAULT_PORT));
const host: string = process.env.HOST ?? "0.0.0.0";

/** Shape of the Vite-loaded SSR entry (`src/ssr.tsx`). */
interface SsrModule {
  render: (request: Request) => Promise<Response>;
}

/** Shape of the Vite-loaded proxy bridge (`src/lib/auth-server.ts`). */
interface AuthServerModule {
  createAuthServer: (config?: AuthServerConfig) => AuthServer;
}

const vite: ViteDevServer = await createViteServer({
  configFile: false,
  root: import.meta.dirname,
  appType: "custom",
  // configFile: false means vite.config.ts is not read, so anything the app's
  // module graph needs from Vite must be re-declared here. `@vitejs/plugin-react`
  // stays out (its Fast Refresh preamble breaks whole-document hydration — see
  // the header), but `wallowStyles()` must be spread in or the `styles.css` entry
  // `src/client.tsx` imports is served with `@import "tailwindcss"` left verbatim
  // and `pnpm dev` renders every screen unstyled. The factory also carries the
  // brand-assets plugin that sets publicDir to the shared package's assets dir,
  // so the fork icon resolves at the root instead of 404ing off a nonexistent
  // ./public.
  plugins: [...wallowStyles()],
  server: { middlewareMode: true },
});

/** Lazily-built, memoised proxy bridge (created on the first API request). */
let authServer: AuthServer | undefined;

async function getAuthServer(): Promise<AuthServer> {
  if (authServer === undefined) {
    const { createAuthServer }: AuthServerModule = (await vite.ssrLoadModule(
      "/src/lib/auth-server.ts",
    )) as AuthServerModule;
    authServer = createAuthServer();
  }
  return authServer;
}

/** Adapt an incoming Node request into a WHATWG `Request`. */
function toWebRequest(req: IncomingMessage): Request {
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
  // entry cannot be forged by an inbound header.
  headers.set(CLIENT_IP_HEADER, req.socket.remoteAddress ?? "");

  const method: string = req.method ?? "GET";
  const hasBody: boolean = method !== "GET" && method !== "HEAD";

  return new Request(url, {
    method,
    headers,
    ...(hasBody ? { body: Readable.toWeb(req) as ReadableStream<Uint8Array>, duplex: "half" } : {}),
  });
}

/** Copy a WHATWG `Response`'s status, headers (incl. multiple `Set-Cookie`),
 * and body onto the Node response. */
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

/** Answer an API-surface request from the reverse-proxy bridge. */
async function handleProxy(req: IncomingMessage, res: ServerResponse): Promise<void> {
  const server: AuthServer = await getAuthServer();
  await writeWebResponse(res, await server.handle(toWebRequest(req)));
}

/** Server-render the router's matched route for this request. */
async function handleSsr(req: IncomingMessage, res: ServerResponse): Promise<void> {
  const { render }: SsrModule = (await vite.ssrLoadModule("/src/ssr.tsx")) as SsrModule;
  await writeWebResponse(res, await render(toWebRequest(req)));
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

  if (isProxyRequest(pathname)) {
    handleProxy(req, res).catch((error: unknown): void => {
      onError(res, error);
    });
    return;
  }

  // Everything else is offered to Vite's middlewares first, then falls through
  // to router SSR. This is what serves the browser bundle in dev: the shell asks
  // for `/src/client.tsx` (Wallow-vec7.1.5), and Vite transforms and serves it —
  // along with its dependency graph, source maps, and HMR client — out of the
  // same module graph the SSR render uses, so both sides share one React copy.
  // Vite calls `next()` for anything it does not own, which is every real route.
  vite.middlewares(req, res, (): void => {
    handleSsr(req, res).catch((error: unknown): void => {
      onError(res, error);
    });
  });
});

server.listen(port, host, (): void => {
  // eslint-disable-next-line no-console
  console.log(`wallow-auth Start SSR dev server listening on http://${host}:${port}`);
});
