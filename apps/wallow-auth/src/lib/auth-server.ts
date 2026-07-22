/**
 * Reverse-proxy server bridge for wallow-auth (Wallow-vec7.1.3).
 *
 * The novel core of the auth server: a PURE
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
 * headers verbatim — a stateless per-request cookie passthrough with no
 * server-side session store or jar.
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
   * `WALLOW_API_INTERNAL_URL` (standalone-dev default `http://localhost:5001`).
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

/**
 * Standalone-dev default for the API when no env/config is set. Points at the
 * local API host (`dotnet run` on :5001) so a bare `pnpm --filter
 * ./apps/wallow-auth dev` outside Aspire resolves a working upstream. Every
 * managed context (Aspire, both Docker compose stacks, the Playwright config)
 * sets `WALLOW_API_INTERNAL_URL` explicitly, so this constant is reached ONLY
 * by standalone dev — where `http://wallow-api` would fail with
 * `getaddrinfo ENOTFOUND wallow-api` (Wallow-vpnt).
 */
const DEFAULT_API_INTERNAL_URL = "http://localhost:5001";

/** Liveness body returned by `GET /health`. */
const HEALTH_BODY = "ready";

/**
 * Internal request header carrying the immediate peer's socket address
 * (`req.socket.remoteAddress`), stamped by the Node host — `server.ts` /
 * `dev-server.ts` in their `toWebRequest` — because the framework-agnostic
 * `handle(request: Request)` bridge is driven with a WHATWG `Request` that has
 * no socket. The proxy APPENDS this to any inbound `X-Forwarded-For` chain so
 * the API rate-limits by the real client IP (RFC 7239 / de-facto XFF), then
 * STRIPS it before the upstream hop so the seam never leaks past this proxy.
 *
 * Trust boundary (Wallow-tt5j): the host always OVERWRITES this header from the
 * socket, so a client cannot forge THIS hop's entry. Inbound `X-Forwarded-For`
 * is still trusted and appended-to (matching `Wallow.Api` Program.cs's
 * `KnownProxies.Clear()` wide-open policy) so an outer TLS-terminating ingress's
 * real-client entry survives as the leftmost value.
 */
export const CLIENT_IP_HEADER = "x-wallow-client-ip";

/**
 * Resolve the upstream API base URL: explicit config wins, then
 * `WALLOW_API_INTERNAL_URL`, then the standalone-dev localhost default.
 */
export function resolveApiInternalUrl(config: AuthServerConfig): string {
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
 * from the inbound request ONLY when the client did not already send it. An
 * outer TLS-terminating ingress is the only hop that knows the browser's real
 * scheme, so its header must win — overwriting it with this proxy's own
 * plain-HTTP leg would downgrade the API's view to `http`.
 *
 * `X-Forwarded-For` follows the same append-not-overwrite rule (Wallow-tt5j):
 * the Node host stamps this hop's real peer address into {@link CLIENT_IP_HEADER}
 * (a WHATWG `Request` has no socket), which is APPENDED to any inbound XFF chain
 * so an outer ingress's leftmost real-client entry survives, then STRIPPED so the
 * internal seam header never reaches the upstream API.
 */
function applyForwardedHeaders(headers: Headers, incoming: URL): void {
  if (!headers.has("x-forwarded-proto")) {
    headers.set("x-forwarded-proto", incoming.protocol.replace(":", ""));
  }
  if (!headers.has("x-forwarded-host")) {
    headers.set("x-forwarded-host", incoming.host);
  }

  const clientIp: string | null = headers.get(CLIENT_IP_HEADER);
  if (clientIp !== null && clientIp !== "") {
    const existing: string | null = headers.get("x-forwarded-for");
    headers.set(
      "x-forwarded-for",
      existing !== null && existing !== "" ? `${existing}, ${clientIp}` : clientIp,
    );
  }
  headers.delete(CLIENT_IP_HEADER);
}

/**
 * Build the reverse-proxy event handler. It forwards the inbound request's
 * method, path, query, body, and headers (including `Cookie`) to
 * `apiInternalUrl` and returns the upstream `Response` unchanged, so h3 relays
 * the status, body, and ALL `Set-Cookie` headers back to the caller. No session
 * store, no cookie jar, no relay — pure per-request passthrough.
 *
 * The one thing it adds is `X-Forwarded-Proto`/`X-Forwarded-Host`: the API's
 * `UseForwardedHeaders` computes the Identity cookie's `Secure` attribute (and
 * OpenIddict's HTTPS check, ID2083) from the scheme it sees, and the
 * wallow-auth -> API leg is plain HTTP even when the browser leg is HTTPS.
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
 * Build the reverse-proxy server. Wires the h3 dispatch (`/health`, `/v1/**`,
 * `/connect/**`, `/.well-known/**`, else 404) and exposes a framework-agnostic
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
  // The whole subtree, not just openid-configuration: the discovery document's
  // jwks_uri advertises this same origin, so signing keys must resolve too.
  router.use("/.well-known/**", proxy);

  app.use(router);

  const webHandler: WebHandler = toWebHandler(app);

  return {
    handle: (request: Request): Promise<Response> => webHandler(request),
  };
}
