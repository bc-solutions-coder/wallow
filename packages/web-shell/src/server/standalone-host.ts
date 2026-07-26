/**
 * Standalone SSR + reverse-proxy host shared by both apps' server.ts
 * (Wallow-0q2s.8.3). Extracted from the near-duplicate apps/wallow-auth/server.ts
 * and apps/wallow-web/server.ts, parameterized by the one thing that differs —
 * the proxy topology — via {@link ShellConfig}.
 *
 * It answers three things, in order:
 *
 *  1. Proxy paths (`config.isProxyPath` — `/health` plus the app's API/BFF
 *     surface) — bridged to `config.handleProxy`.
 *  2. Built browser assets out of `dist/client` — at minimum `/client.js`, the
 *     path the document shell hardcodes (routes/__root.tsx).
 *  3. Everything else — server-rendered by the built SSR entry, which resolves
 *     404 vs 200 vs redirect itself, so this host needs no route table.
 *
 * This host consumes the PRE-BUILT output of `pnpm build`
 * (`dist/server/ssr.js` and `dist/client/`) rather than driving a live Vite
 * server. The SSR entry is loaded eagerly at boot, on purpose: a lazy load would
 * let a proxy health check go green on an image whose render bundle is missing
 * or broken, and the container would then serve 500s to every screen behind a
 * healthy compose status.
 */
import {
  createServer as createHttpServer,
  type IncomingMessage,
  type Server,
  type ServerResponse,
} from "node:http";
import { join } from "node:path";
import { Readable } from "node:stream";
import { pathToFileURL } from "node:url";

import {
  createStaticAssetReader,
  type StaticAsset,
  type StaticAssetReader,
} from "../static-assets";

/**
 * The per-app seam over the standalone host. Both apps supply the same host
 * behavior; only these values differ (the proxy topology plus auth's client-IP
 * stamp and each app's default port).
 */
export interface ShellConfig {
  /** App identity, used in the boot log and the build-first guard message (e.g. "wallow-auth"). */
  appName: string;
  /** Port used when `PORT` is unset in the environment (auth "3002", web "3000"). */
  defaultPort: string;
  /**
   * Absolute directory of the app whose host this is — its `import.meta.dirname`.
   * The built `dist/client` asset root and the `dist/server/ssr.js` SSR entry are
   * resolved relative to THIS directory (the app), not the web-shell package.
   */
  appDir: string;
  /** True for paths the proxy owns (`/health` plus the app's API/BFF surface). */
  isProxyPath: (pathname: string) => boolean;
  /** Handles a proxied request (auth: `AuthServer.handle`; web: `handleBffRequest`). */
  handleProxy: (request: Request) => Promise<Response>;
  /**
   * Optional internal seam header, stamped with the peer socket address on every
   * proxied request (wallow-auth's `CLIENT_IP_HEADER`). Omitted for wallow-web,
   * whose topology does not forward a client IP.
   */
  clientIpHeader?: string;
  /**
   * Whether dev mode wires `@vitejs/plugin-react`. Consumed by the dev-server
   * factory (Wallow-0q2s.8.4), not the standalone host; carried here so both
   * factories share one config shape.
   */
  reactPluginInDev?: boolean;
}

/** Shape of the built SSR entry (`dist/server/ssr.js`, built from src/ssr.tsx). */
interface SsrModule {
  render: (request: Request) => Promise<Response>;
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

/**
 * Build and start the standalone SSR + reverse-proxy host. Loads the built SSR
 * entry eagerly (failing fast with a `pnpm build` message that names the app),
 * serves built client assets out of `dist/client`, dispatches proxy paths to
 * `config.handleProxy`, and server-renders everything else. Resolves to the
 * listening Node HTTP server so callers can inspect its address and close it.
 */
export async function createStandaloneHost(config: ShellConfig): Promise<Server> {
  const port: number = Math.trunc(Number(process.env.PORT ?? config.defaultPort));
  const host: string = process.env.HOST ?? "0.0.0.0";

  const readStaticAsset: StaticAssetReader = createStaticAssetReader(
    join(config.appDir, "dist", "client"),
  );

  // Imported through a runtime URL rather than a static specifier: `dist/` is a
  // build artefact, absent from a clean checkout, so a literal import would make
  // `tsc --noEmit` depend on having built first.
  const ssrEntry: string = pathToFileURL(join(config.appDir, "dist", "server", "ssr.js")).href;
  const { render }: SsrModule = (await import(ssrEntry).catch((error: unknown): never => {
    throw new Error(
      `${config.appName}: cannot load the SSR entry at ${ssrEntry}. Run \`pnpm build\` before \`pnpm start\`.`,
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

    // Stamp the immediate peer's socket address into the internal seam header so
    // the proxy can append it to X-Forwarded-For (the WHATWG Request it hands the
    // proxy carries no socket). Always OVERWRITTEN from the socket so this hop's
    // entry cannot be forged by an inbound header. Only wallow-auth's topology
    // forwards a client IP, so this is gated on `config.clientIpHeader`.
    if (config.clientIpHeader !== undefined) {
      headers.set(config.clientIpHeader, req.socket.remoteAddress ?? "");
    }

    const method: string = req.method ?? "GET";
    const hasBody: boolean = method !== "GET" && method !== "HEAD";

    return new Request(url, {
      method,
      headers,
      ...(hasBody
        ? { body: Readable.toWeb(req) as ReadableStream<Uint8Array>, duplex: "half" }
        : {}),
    });
  }

  async function handleRequest(req: IncomingMessage, res: ServerResponse): Promise<void> {
    const request: Request = toWebRequest(req);
    const pathname: string = new URL(request.url).pathname;

    if (config.isProxyPath(pathname)) {
      await writeWebResponse(res, await config.handleProxy(request));
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

  await new Promise<void>((resolve): void => {
    server.listen(port, host, (): void => {
      // eslint-disable-next-line no-console
      console.log(`${config.appName} standalone SSR host listening on http://${host}:${port}`);
      resolve();
    });
  });

  return server;
}
