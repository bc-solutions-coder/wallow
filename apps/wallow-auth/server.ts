/**
 * Standalone reverse-proxy host for wallow-auth (Wallow-vec7.1.4).
 *
 * `pnpm start` runs this (`tsx server.ts`) — the host used by the future
 * Dockerfile/E2E container. It bridges the Node HTTP server to the h3 proxy
 * app built by {@link createAuthServer} (task 0.3), so `/health` returns
 * `ready` and `/v1/**` + `/connect/**` are reverse-proxied to Wallow.Api with
 * per-request cookie passthrough; everything else is 404.
 *
 * Unlike `dev-server.ts`, this host does NOT server-render the router: the
 * standalone SSR path (serving the login/register/reset pages) is wired in a
 * later phase, mirroring wallow-web whose `start` host serves its built assets
 * rather than SSR. Phase 0 only needs this host to boot and expose the proxy.
 */
import {
  createServer as createHttpServer,
  type IncomingMessage,
  type Server,
  type ServerResponse,
} from "node:http";
import { Readable } from "node:stream";

import { createAuthServer, type AuthServer } from "./src/lib/auth-server";

const DEFAULT_PORT = "3000";
const port: number = Math.trunc(Number(process.env.PORT ?? DEFAULT_PORT));
const host: string = process.env.HOST ?? "0.0.0.0";

const authServer: AuthServer = createAuthServer();

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
  const body: string = response.body === null ? "" : await response.text();
  res.end(body);
}

async function handleRequest(req: IncomingMessage, res: ServerResponse): Promise<void> {
  const request: Request = toWebRequest(req);
  await writeWebResponse(res, await authServer.handle(request));
}

const server: Server = createHttpServer((req: IncomingMessage, res: ServerResponse): void => {
  handleRequest(req, res).catch((error: unknown): void => {
    // eslint-disable-next-line no-console
    console.error("server proxy error", error);
    if (!res.headersSent) {
      res.statusCode = 500;
    }
    res.end("Internal Server Error");
  });
});

server.listen(port, host, (): void => {
  // eslint-disable-next-line no-console
  console.log(`wallow-auth standalone proxy host listening on http://${host}:${port}`);
});
