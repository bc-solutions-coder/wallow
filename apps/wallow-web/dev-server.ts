/**
 * TanStack Start SSR dev server for wallow-web (Wallow-8w1h.2.2).
 *
 * `pnpm dev` runs this: it boots an HTTP listener that server-renders the
 * router's matched route for each request and returns the SSR HTML shell. The
 * public home route emits `data-testid="home-heading"`, so `curl localhost/`
 * returns that markup — the task's acceptance criterion.
 *
 * JSX is transformed by Vite's `@vitejs/plugin-react` (the standalone `tsx`
 * runner can't resolve the workspace's automatic-runtime JSX config through the
 * `extends` chain). We run Vite in `middlewareMode` and pull the render entry
 * via `ssrLoadModule`, which also keeps the whole render path on one React copy.
 *
 * BFF MOUNT (Wallow-8w1h.2.3 spike): the installed TanStack stack exposes no
 * file-based server-route creator (`createServerFileRoute`/`createServerRoute`
 * are absent from @tanstack/react-start@1.168.28 / react-router@1.170.18), so
 * the design's documented fallback is taken: the SDK's h3 BFF/proxy handlers are
 * mounted at THIS server layer. Requests to `/health`, `/bff/*`, and `/api/**`
 * are dispatched to `src/lib/bff-server.ts`'s `handleBffRequest` (a web
 * Request -> Response bridge over the same h3 app the old `server.ts` built);
 * everything else falls through to the router SSR. This keeps the whole BFF
 * surface reachable at the exact URLs the C# `BffFlowTests`/`DockerComposeFixture`
 * assert, in one Node process, with no createServerFileRoute dependency.
 *
 * The BFF module is imported lazily on the first BFF-prefixed request: it builds
 * its config/store/handlers at import time (mirroring `server.ts`), so plain
 * `pnpm dev` without BFF env still serves SSR — only BFF/api/health routes
 * require the OIDC env to be present.
 *
 * `build` (library bundle to `public/app.js`) and `start` (`tsx server.ts`, the
 * standalone h3 BFF host) are intentionally left as-is: the Dockerfile and E2E
 * path still use them until the docker-wiring follow-up task migrates them.
 */
import {
  createServer as createHttpServer,
  type IncomingMessage,
  type Server,
  type ServerResponse,
} from "node:http";
import { Readable } from "node:stream";

import { brandAssetsDir } from "@bc-solutions-coder/styles/assets";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { createServer as createViteServer, type ViteDevServer } from "vite";

import { isBffProxyPath } from "./src/lib/proxy-topology";

const DEFAULT_PORT = "3000";
const port: number = Math.trunc(Number(process.env.PORT ?? DEFAULT_PORT));
const host: string = process.env.HOST ?? "0.0.0.0";

/** Shape of the Vite-loaded SSR entry (`src/ssr.tsx`). */
interface SsrModule {
  render: (request: Request) => Promise<Response>;
}

/** Shape of the Vite-loaded BFF bridge (`src/lib/bff-server.ts`). */
interface BffModule {
  handleBffRequest: (request: Request) => Promise<Response>;
}

const vite: ViteDevServer = await createViteServer({
  configFile: false,
  root: import.meta.dirname,
  appType: "custom",
  server: { middlewareMode: true },
  // configFile: false means vite.config.ts is not read, so anything the app's
  // module graph needs from Vite must be re-declared here. `@tailwindcss/vite`
  // must be wired in or the `styles.css` entry `src/client.tsx` imports is
  // served with `@import "tailwindcss"` left verbatim, and `pnpm dev` renders
  // every screen unstyled (Wallow-ffpq.3.4).
  plugins: [react(), tailwindcss()],
  // publicDir must be set here too or the fork icon 404s under `pnpm dev` (it
  // would default to a nonexistent ./public). Serves the shared styles package's
  // assets at the root.
  publicDir: brandAssetsDir,
});

/** Adapt an incoming Node request into a WHATWG `Request` for the router. */
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

async function handleRequest(req: IncomingMessage, res: ServerResponse): Promise<void> {
  const request: Request = toWebRequest(req);
  const pathname: string = new URL(request.url).pathname;

  if (isBffProxyPath(pathname)) {
    const { handleBffRequest }: BffModule = (await vite.ssrLoadModule(
      "/src/lib/bff-server.ts",
    )) as BffModule;
    await writeWebResponse(res, await handleBffRequest(request));
    return;
  }

  const { render }: SsrModule = (await vite.ssrLoadModule("/src/ssr.tsx")) as SsrModule;
  await writeWebResponse(res, await render(request));
}

const server: Server = createHttpServer((req: IncomingMessage, res: ServerResponse): void => {
  handleRequest(req, res).catch((error: unknown): void => {
    vite.ssrFixStacktrace(error as Error);
    // eslint-disable-next-line no-console
    console.error("dev-server render error", error);
    if (!res.headersSent) {
      res.statusCode = 500;
    }
    res.end("Internal Server Error");
  });
});

server.listen(port, host, (): void => {
  // eslint-disable-next-line no-console
  console.log(`wallow-web Start SSR dev server listening on http://${host}:${port}`);
});
