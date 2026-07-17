/**
 * Standalone SSR + BFF host for wallow-web (Wallow-ffpq.3.3).
 *
 * `pnpm start` runs this (`tsx server.ts`) — the host the Dockerfile/E2E
 * container runs. It answers three things, in order:
 *
 *  1. `/health`, `/bff/*`, `/api/**` — bridged to the h3 BFF app built by
 *     `src/lib/bff-server.ts`'s {@link handleBffRequest}, so `/health` returns
 *     `ok`, the OIDC tunnel is reachable, and `/api/**` is reverse-proxied to
 *     Wallow.Api with a Bearer token and silent refresh. The prefix set mirrors
 *     `dev-server.ts`'s `isBffRequest` exactly: no `/_blazor`, and `/api/**` is
 *     the BFF proxy's OWN mount — it is NOT a second passthrough to Wallow.Api
 *     (the topology difference from wallow-auth, see Wallow-ffpq.3.8).
 *  2. Built browser assets out of `dist/client` — at minimum `/client.js`, the
 *     path the document shell hardcodes (routes/__root.tsx).
 *  3. Everything else — server-rendered by the router. `render()` resolves 404
 *     vs 200 vs redirect itself, so this host needs no route table of its own.
 *
 * Where `dev-server.ts` drives a live Vite server (middlewareMode +
 * `ssrLoadModule("/src/ssr.tsx")`) this host consumes the PRE-BUILT output of
 * `pnpm build`: `dist/server/ssr.js` and `dist/client/`. That is a hard
 * build-before-run requirement — `dist/` is gitignored and never committed, so
 * the image must build before it starts.
 *
 * The SSR entry is loaded eagerly at boot, on purpose. A lazy load would let
 * `/health` go green on an image whose render bundle is missing or broken, and
 * the container would then serve 500s to every route behind a healthy compose
 * status — the exact failure this host was previously guilty of when it served
 * only `public/` and the BFF surface.
 */
import {
  createServer as createHttpServer,
  type IncomingMessage,
  type Server,
  type ServerResponse,
} from "node:http";
import { join } from "node:path";
import { Readable } from "node:stream";

import { handleBffRequest } from "./src/lib/bff-server";
import { isBffProxyPath } from "./src/lib/proxy-topology";
import {
  createStaticAssetReader,
  type StaticAsset,
  type StaticAssetReader,
} from "./src/lib/static-assets";

const DEFAULT_PORT = "3000";
const port: number = Math.trunc(Number(process.env.PORT ?? DEFAULT_PORT));
const host: string = process.env.HOST ?? "0.0.0.0";

/** Shape of the built SSR entry (`dist/server/ssr.js`, built from src/ssr.tsx). */
interface SsrModule {
  render: (request: Request) => Promise<Response>;
}

const readStaticAsset: StaticAssetReader = createStaticAssetReader(
  join(import.meta.dirname, "dist", "client"),
);

// Imported through a runtime URL rather than a static specifier: `dist/` is a
// build artefact, absent from a clean checkout, so a literal import would make
// `tsc --noEmit` (which includes this file) depend on having built first.
const ssrEntry: string = new URL("dist/server/ssr.js", import.meta.url).href;
const { render }: SsrModule = (await import(ssrEntry).catch((error: unknown): never => {
  throw new Error(
    `wallow-web: cannot load the SSR entry at ${ssrEntry}. Run \`pnpm build\` before \`pnpm start\`.`,
    { cause: error },
  );
})) as SsrModule;

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

  if (isBffProxyPath(pathname)) {
    await writeWebResponse(res, await handleBffRequest(request));
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
  console.log(`wallow-web standalone SSR host listening on http://${host}:${port}`);
});
