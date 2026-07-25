/**
 * Same-origin reverse-proxy bridge — the app-specific proxy topology the
 * web-shell host factory delegates to. This is the "proxy topology" seam the
 * host/dev-server factories keep per-app (see the New App Bootstrap guide): a
 * pure passthrough reverse proxy, the simpler of the two golden-path topologies
 * (the other being wallow-web's BFF token tunnel via `@bc-solutions-coder/sdk/server`).
 *
 * {@link createProxyServer} builds an h3 app that dispatches:
 *
 *   - `GET /health`      -> 200 `ready` (liveness the standalone host / E2E wait on).
 *   - `/v1/**`           -> reverse-proxied to `apiInternalUrl` verbatim.
 *   - `/connect/**`      -> reverse-proxied to `apiInternalUrl` verbatim.
 *   - `/.well-known/**`  -> reverse-proxied to `apiInternalUrl` verbatim.
 *   - everything else    -> 404.
 *
 * Proxying forwards the inbound method, path, query, body, and `Cookie` header
 * per request and returns the upstream `Response` unchanged, so h3 relays the
 * status, body, and every `Set-Cookie` header back to the caller — a stateless
 * per-request cookie passthrough with no server-side session store.
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

/** Configuration for {@link createProxyServer}. */
export interface ProxyServerConfig {
  /**
   * Internal base URL of Wallow.Api that `/v1/**`, `/connect/**`, and
   * `/.well-known/**` are reverse-proxied to. When omitted it resolves from
   * `WALLOW_API_INTERNAL_URL`, then the standalone-dev localhost default.
   */
  apiInternalUrl?: string;
}

/** The proxy handler surface exposed to the SSR/standalone hosts. */
export interface ProxyServer {
  /** Bridge a WHATWG `Request` to a `Response` by driving the h3 proxy app. */
  handle: (request: Request) => Promise<Response>;
}

/** Standalone-dev default when no env/config is set — the local `dotnet run` API host. */
const DEFAULT_API_INTERNAL_URL = "http://localhost:5001";

/** Liveness body returned by `GET /health`. */
const HEALTH_BODY = "ready";

/** Resolve the upstream API base URL: explicit config wins, then env, then the localhost default. */
export function resolveApiInternalUrl(config: ProxyServerConfig): string {
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
 * Set `X-Forwarded-Proto`/`X-Forwarded-Host` for the upstream hop, deriving each
 * from the inbound request only when the client did not already send it — an
 * outer TLS-terminating ingress is the only hop that knows the browser's real
 * scheme, so its header must win. The API computes the Identity cookie's `Secure`
 * attribute from the scheme it sees, and this app -> API leg is plain HTTP.
 */
function applyForwardedHeaders(headers: Headers, incoming: URL): void {
  if (!headers.has("x-forwarded-proto")) {
    headers.set("x-forwarded-proto", incoming.protocol.replace(":", ""));
  }
  if (!headers.has("x-forwarded-host")) {
    headers.set("x-forwarded-host", incoming.host);
  }
}

/**
 * Build the reverse-proxy event handler: forward the inbound request's method,
 * path, query, body, and headers (including `Cookie`) to `apiInternalUrl` and
 * return the upstream `Response` unchanged.
 */
function createProxyHandler(apiInternalUrl: string): EventHandler {
  return defineEventHandler(async (event: H3Event): Promise<Response> => {
    const request: Request = toWebRequest(event);
    const incoming: URL = new URL(request.url);
    const target: string = `${apiInternalUrl}${incoming.pathname}${incoming.search}`;

    const headers: Headers = new Headers(request.headers);
    // Strip the inbound Host so fetch derives it from the upstream target.
    headers.delete("host");
    applyForwardedHeaders(headers, incoming);

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
 * Build the reverse-proxy server: wire the h3 dispatch (`/health`, `/v1/**`,
 * `/connect/**`, `/.well-known/**`, else 404) and expose a framework-agnostic
 * `handle(request): Promise<Response>` bridge via `toWebHandler`.
 */
export function createProxyServer(config: ProxyServerConfig = {}): ProxyServer {
  const apiInternalUrl: string = resolveApiInternalUrl(config);
  const proxy: EventHandler = createProxyHandler(apiInternalUrl);

  const app: App = createApp();
  const router: Router = createRouter();

  router.get(
    "/health",
    defineEventHandler((): Response => new Response(HEALTH_BODY)),
  );
  router.use("/v1/**", proxy);
  router.use("/connect/**", proxy);
  router.use("/.well-known/**", proxy);

  app.use(router);

  const webHandler: WebHandler = toWebHandler(app);

  return {
    handle: (request: Request): Promise<Response> => webHandler(request),
  };
}
