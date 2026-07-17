/**
 * Standalone SSR + reverse-proxy host for wallow-auth (Wallow-vec7.1.4,
 * SSR wired in Wallow-vec7.5.1.1).
 *
 * `pnpm start` runs this (`tsx server.ts`) — the host the Dockerfile/E2E
 * container runs. It answers three things, in order:
 *
 *  1. `/health`, `/v1/**`, `/connect/**` — bridged to the h3 proxy app built by
 *     {@link createAuthServer} (task 0.3), so `/health` returns `ready` and the
 *     API surface is reverse-proxied to Wallow.Api with per-request cookie
 *     passthrough.
 *  2. Built browser assets out of `dist/client` — at minimum `/client.js`, the
 *     path the document shell hardcodes (routes/__root.tsx).
 *  3. Everything else — server-rendered by the router. `render()` resolves 404
 *     vs 200 vs redirect (`/` -> `/login`) itself, so this host needs no route
 *     table of its own.
 *
 * Where `dev-server.ts` drives a live Vite server (middlewareMode +
 * `ssrLoadModule("/src/ssr.tsx")`) this host consumes the PRE-BUILT output of
 * `pnpm build`: `dist/server/ssr.js` and `dist/client/`. That is a hard
 * build-before-run requirement — `dist/` is gitignored and never committed, so
 * the image must build before it starts (see the Dockerfile task, 4.1b).
 *
 * The SSR entry is loaded eagerly at boot, on purpose. A lazy load would let
 * `/health` go green on an image whose render bundle is missing or broken, and
 * the container would then serve 500s to every auth screen behind a healthy
 * compose status — the exact failure this host was previously guilty of when it
 * 404'd every route but the proxy.
 */
import {
  createServer as createHttpServer,
  type IncomingMessage,
  type Server,
  type ServerResponse,
} from "node:http";
import { join } from "node:path";
import { Readable } from "node:stream";

import { createAuthServer, type AuthServer } from "./src/lib/auth-server";
import {
  createStaticAssetReader,
  type StaticAsset,
  type StaticAssetReader,
} from "./src/lib/static-assets";

const DEFAULT_PORT = "3002";
const port: number = Math.trunc(Number(process.env.PORT ?? DEFAULT_PORT));
const host: string = process.env.HOST ?? "0.0.0.0";

/** Shape of the built SSR entry (`dist/server/ssr.js`, built from src/ssr.tsx). */
interface SsrModule {
  render: (request: Request) => Promise<Response>;
}

const authServer: AuthServer = createAuthServer();
const readStaticAsset: StaticAssetReader = createStaticAssetReader(
  join(import.meta.dirname, "dist", "client"),
);

// Imported through a runtime URL rather than a static specifier: `dist/` is a
// build artefact, absent from a clean checkout, so a literal import would make
// `tsc --noEmit` (which includes this file) depend on having built first.
const ssrEntry: string = new URL("dist/server/ssr.js", import.meta.url).href;
const { render }: SsrModule = (await import(ssrEntry).catch((error: unknown): never => {
  throw new Error(
    `wallow-auth: cannot load the SSR entry at ${ssrEntry}. Run \`pnpm build\` before \`pnpm start\`.`,
    { cause: error },
  );
})) as SsrModule;

/**
 * Path prefixes answered by the reverse-proxy bridge rather than router SSR.
 *
 * Mirrors what {@link createAuthServer}'s router actually mounts — including
 * `/.well-known/**`, which `dev-server.ts`'s otherwise-identical helper omits.
 * It cannot be dropped here: until now this host handed EVERY path to the proxy,
 * so discovery and JWKS resolve today, and routing them to SSR instead would
 * 404 the `jwks_uri` this origin advertises.
 */
function isProxyRequest(pathname: string): boolean {
  return (
    pathname === "/health" ||
    pathname.startsWith("/v1/") ||
    pathname.startsWith("/connect/") ||
    pathname.startsWith("/.well-known/")
  );
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
  // Written as bytes rather than `await response.text()`: this host now serves
  // built assets, which are not all UTF-8 (e.g. `.ico`), and decoding them to a
  // string would corrupt them.
  if (response.body === null) {
    res.end();
    return;
  }
  res.end(Buffer.from(await response.arrayBuffer()));
}

/** Write an asset read out of `dist/client` to the response. */
function writeStaticAsset(res: ServerResponse, asset: StaticAsset): void {
  res.statusCode = 200;
  res.setHeader("content-type", asset.contentType);
  res.end(asset.contents);
}

async function handleRequest(req: IncomingMessage, res: ServerResponse): Promise<void> {
  const request: Request = toWebRequest(req);
  const pathname: string = new URL(request.url).pathname;

  if (isProxyRequest(pathname)) {
    await writeWebResponse(res, await authServer.handle(request));
    return;
  }

  const asset: StaticAsset | undefined = await readStaticAsset(pathname);
  if (asset !== undefined) {
    writeStaticAsset(res, asset);
    return;
  }

  // The router owns every remaining path, 404s included.
  await writeWebResponse(res, await render(request));
}

const server: Server = createHttpServer((req: IncomingMessage, res: ServerResponse): void => {
  handleRequest(req, res).catch((error: unknown): void => {
    // eslint-disable-next-line no-console
    console.error("server request error", error);
    if (!res.headersSent) {
      res.statusCode = 500;
    }
    res.end("Internal Server Error");
  });
});

server.listen(port, host, (): void => {
  // eslint-disable-next-line no-console
  console.log(`wallow-auth standalone SSR host listening on http://${host}:${port}`);
});
