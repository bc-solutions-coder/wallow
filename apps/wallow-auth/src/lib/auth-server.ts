/**
 * Reverse-proxy server bridge for wallow-auth (Wallow-vec7.1.3).
 *
 * The novel core of the Blazor -> TanStack Start auth migration: a PURE
 * passthrough reverse proxy (NOT a BFF token tunnel like wallow-web's
 * `bff-server.ts`). {@link createAuthServer} builds an h3 app that dispatches:
 *
 *   - `GET /health`      -> 200 `ready` (liveness for the E2E fixture).
 *   - `/v1/**`           -> reverse-proxied to `apiInternalUrl` verbatim.
 *   - `/connect/**`      -> reverse-proxied to `apiInternalUrl` verbatim.
 *   - everything else    -> 404.
 *
 * Proxying forwards the inbound method, path, query, body, and `Cookie` header
 * per request, and copies back the upstream status, body, and ALL `Set-Cookie`
 * headers verbatim — replacing the entire Blazor cookie-relay subsystem with
 * per-request cookie passthrough and no server-side session store or jar.
 *
 * Multi-cookie survival: the proxy handler returns the upstream WHATWG
 * `Response` directly, so h3's `sendWebResponse`/`toWebHandler` round-trip
 * preserves every `Set-Cookie` header (it splits and re-appends them, so the
 * two login cookies both reach the caller's `Response.headers.getSetCookie()`).
 */
import {
  createApp,
  createRouter,
  defineEventHandler,
  toWebHandler,
  toWebRequest,
  type App,
  type EventHandler,
  type H3Event,
  type Router,
  type WebHandler,
} from "h3";

/** Configuration for {@link createAuthServer}. */
export interface AuthServerConfig {
  /**
   * Internal base URL of Wallow.Api that `/v1/**` and `/connect/**` are
   * reverse-proxied to. When omitted, it is resolved from
   * `WALLOW_API_INTERNAL_URL` (Aspire default `http://wallow-api`).
   */
  apiInternalUrl?: string;
}

/** The reverse-proxy handler surface exposed to the SSR/standalone hosts. */
export interface AuthServer {
  /**
   * Bridge a WHATWG `Request` to a `Response` by driving the h3 proxy app.
   * The dev-server / standalone host forward every request here.
   */
  handle: (request: Request) => Promise<Response>;
}

/** Aspire service-discovery default for the API when no env/config is set. */
const DEFAULT_API_INTERNAL_URL = "http://wallow-api";

/** Liveness body returned by `GET /health`. */
const HEALTH_BODY = "ready";

/**
 * Resolve the upstream API base URL: explicit config wins, then
 * `WALLOW_API_INTERNAL_URL`, then the Aspire default.
 */
function resolveApiInternalUrl(config: AuthServerConfig): string {
  if (config.apiInternalUrl !== undefined && config.apiInternalUrl !== "") {
    return config.apiInternalUrl;
  }
  const fromEnv: string | undefined = process.env.WALLOW_API_INTERNAL_URL;
  if (fromEnv !== undefined && fromEnv !== "") {
    return fromEnv;
  }
  return DEFAULT_API_INTERNAL_URL;
}

/**
 * Build the reverse-proxy event handler. It forwards the inbound request's
 * method, path, query, body, and headers (including `Cookie`) to
 * `apiInternalUrl` and returns the upstream `Response` unchanged, so h3 relays
 * the status, body, and ALL `Set-Cookie` headers back to the caller. No session
 * store, no cookie jar, no relay — pure per-request passthrough.
 */
function createProxyHandler(apiInternalUrl: string): EventHandler {
  return defineEventHandler(async (event: H3Event): Promise<Response> => {
    const request: Request = toWebRequest(event);
    const incoming: URL = new URL(request.url);
    const target: string = `${apiInternalUrl}${incoming.pathname}${incoming.search}`;

    const headers: Headers = new Headers(request.headers);
    // Strip the inbound Host so fetch derives it from the upstream target.
    headers.delete("host");

    const hasBody: boolean = request.method !== "GET" && request.method !== "HEAD";
    const init: RequestInit = {
      method: request.method,
      headers,
      redirect: "manual",
      ...(hasBody ? { body: await request.arrayBuffer() } : {}),
    };

    return fetch(target, init);
  });
}

/**
 * Build the reverse-proxy server. Wires the h3 dispatch (`/health`, `/v1/**`,
 * `/connect/**`, else 404) and exposes a framework-agnostic
 * `handle(request): Promise<Response>` bridge via `toWebHandler`.
 */
export function createAuthServer(config: AuthServerConfig = {}): AuthServer {
  const apiInternalUrl: string = resolveApiInternalUrl(config);
  const proxy: EventHandler = createProxyHandler(apiInternalUrl);

  const app: App = createApp();
  const router: Router = createRouter();

  // Liveness for the E2E fixture health wait.
  router.get(
    "/health",
    defineEventHandler((): Response => new Response(HEALTH_BODY)),
  );

  // Wildcards forward the full subtree; the upstream path is preserved intact.
  router.use("/v1/**", proxy);
  router.use("/connect/**", proxy);

  app.use(router);

  const webHandler: WebHandler = toWebHandler(app);

  return {
    handle: (request: Request): Promise<Response> => webHandler(request),
  };
}
