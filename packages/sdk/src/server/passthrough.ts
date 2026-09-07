/**
 * Session-free reverse proxy for allowed API path prefixes. It forwards request cookies and bodies and preserves upstream responses, including Set-Cookie headers. The separate passthrough entrypoint avoids loading the BFF authentication dependencies; the host must bound upstream request time.
 */

import { ClientErrorCode, ErrorCode } from "@bc-solutions-coder/api-errors";

import { resolveRequestId } from "../request-id";
import { redact } from "./errors";
import {
  applyForwardedHeaders,
  resolveClientAddress,
  resolveTrustedProxies,
  type PeerRequest,
  type TrustedProxies,
} from "./forwarded";
import { problemResponse } from "./problem";

/**
 * The request shape a host hands {@link ApiPassthrough.handle}: a WHATWG
 * `Request` plus the peer address srvx exposes on `ip`.
 *
 * Re-exported rather than declared: this subpath is where a passthrough host
 * imports it from, but the BFF's own `/api` proxy reads the same property, so
 * the one definition lives in `./forwarded`. The trust helpers themselves
 * (`createClientAddressResolver`, `resolveRequestOrigin`, …) ship on
 * `./server/forwarded`.
 */
export { type PeerRequest } from "./forwarded";

/**
 * Standalone-dev default upstream when neither config nor
 * `WALLOW_API_INTERNAL_URL` is set: the local `dotnet run` API host. Every
 * managed context (Aspire, both compose stacks, Playwright) sets the env var
 * explicitly, so this is reached only by a bare `pnpm dev`.
 */
export const DEFAULT_API_INTERNAL_URL: string = "http://localhost:5001";

/**
 * The default proxied prefixes.
 *
 * `/.well-known/**` is REQUIRED, not optional: an OIDC client whose authority
 * points at this origin resolves discovery at
 * `${origin}/.well-known/openid-configuration` and then fetches signing keys
 * from the `jwks_uri` that document advertises — which is this origin too.
 * Omitting the prefix 404s discovery and breaks login with no useful error.
 */
export const DEFAULT_PASSTHROUGH_PREFIXES: readonly string[] = [
  "/v1/**",
  "/connect/**",
  "/.well-known/**",
];

/** Options for {@link createApiPassthrough}. */
export interface ApiPassthroughOptions {
  /**
   * Internal base URL of the API every allowlisted request is forwarded to.
   * When omitted it resolves from `WALLOW_API_INTERNAL_URL`, then
   * {@link DEFAULT_API_INTERNAL_URL}.
   */
  apiInternalUrl?: string;
  /**
   * Path prefixes this proxy answers. Each entry may be written as a subtree
   * wildcard (`/v1/**`) or as a bare prefix (`/v1`); both match the prefix
   * itself and everything below it, on segment boundaries only. Defaults to
   * {@link DEFAULT_PASSTHROUGH_PREFIXES}.
   */
  prefixes?: readonly string[];
  /**
   * Whether to append the resolved client address to the upstream
   * `X-Forwarded-For` chain. Defaults to `true`.
   */
  forwardClientIp?: boolean;
  /**
   * The proxies whose `X-Forwarded-For` may be believed, in the same notation as
   * `WALLOW_TRUSTED_PROXIES` (CIDRs, bare addresses, or the `loopback`,
   * `linklocal`, `uniquelocal`, `private` presets; comma- or space-separated).
   * When omitted it resolves from `WALLOW_TRUSTED_PROXIES`, then to trusting
   * nothing — the peer address IS the client. An empty string trusts nothing
   * even when the variable is set.
   */
  trustedProxies?: string;
  /**
   * Environment source for `WALLOW_API_INTERNAL_URL` and
   * `WALLOW_TRUSTED_PROXIES`. Defaults to `process.env`.
   */
  env?: NodeJS.ProcessEnv;
}

/** The passthrough surface a host mounts. */
export interface ApiPassthrough {
  /**
   * Forward an allowlisted request upstream; answer 404 for anything else. The
   * caller's address is resolved from `request.ip` and the trusted-proxy list,
   * never from anything the caller sent.
   */
  handle: (request: PeerRequest) => Promise<Response>;
  /**
   * Return whether a URL pathname matches a configured prefix on a segment boundary.
   * Prefixes match both their own path and descendants.
   */
  matches: (pathname: string) => boolean;
  /** The resolved upstream base URL. */
  readonly apiInternalUrl: string;
  /** The resolved prefix allowlist, as supplied. */
  readonly prefixes: readonly string[];
}

/** Status answered for a path outside the prefix allowlist. */
const NOT_FOUND_STATUS = 404;

/** Status answered when the upstream could not be reached. */
const NETWORK_FAILURE_STATUS = 503;

/** Path separator, also the shortest possible normalized prefix. */
const ROOT_PATH: string = "/";

/**
 * The optional subtree wildcard (`/**`) and any trailing slashes at the end of a
 * prefix entry — the part that carries no matching information, since every
 * entry guards its own subtree either way.
 */
const PREFIX_TAIL_PATTERN: RegExp = /(?:\/\*\*)?\/*$/u;

/**
 * Resolve the API passthrough target URL.
 *
 * Uses a nonempty options.apiInternalUrl, then WALLOW_API_INTERNAL_URL from options.env or
 * process.env, then http://localhost:5001. Returns the configured string without validating the
 * URL.
 */
export function resolveApiInternalUrl(options: ApiPassthroughOptions = {}): string {
  if (options.apiInternalUrl !== undefined && options.apiInternalUrl !== "") {
    return options.apiInternalUrl;
  }
  const env: NodeJS.ProcessEnv = options.env ?? process.env;
  const fromEnv: string | undefined = env.WALLOW_API_INTERNAL_URL;
  if (fromEnv !== undefined && fromEnv !== "") {
    return fromEnv;
  }
  return DEFAULT_API_INTERNAL_URL;
}

/**
 * Reduce a prefix entry to the bare path it guards, accepting both the subtree
 * wildcard form (`/v1/**`) and the bare form (`/v1`). Trailing slashes go too,
 * so every entry ends up in the one shape {@link pathMatchesPrefix} compares
 * against.
 */
function normalizePrefix(entry: string): string {
  const prefix: string = entry.trim().replace(PREFIX_TAIL_PATTERN, "");
  return prefix === "" ? ROOT_PATH : prefix;
}

/**
 * Whether a path lies at or below a normalized prefix, on segment boundaries
 * only. A bare `startsWith` would also accept `/v1extra` and
 * `/.well-knownsuffix`, turning the allowlist — which is this preset's entire
 * security boundary — into a prefix-collision game.
 */
function pathMatchesPrefix(pathname: string, prefix: string): boolean {
  return pathname === prefix || pathname.startsWith(`${prefix}${ROOT_PATH}`);
}

/** The message of a thrown value, when it is an `Error` at all. */
function errorMessage(value: unknown): string | undefined {
  return value instanceof Error ? value.message : undefined;
}

/**
 * Create a session-free reverse proxy for API and OIDC routes.
 *
 * Defaults to /v1, /connect, and /.well-known subtrees, forwarding the original path and query
 * to the internal API. Relays caller cookies and authorization headers, and returns upstream
 * redirects without following them. Unmatched paths return 404; network failures return a 503
 * problem response.
 *
 * @param options Internal target, allowed prefixes, environment source, and client-address
 * forwarding policy.
 *
 * @returns A matches predicate and web-standard handle function. Pass the runtime request.ip
 * when client-address forwarding is enabled.
 */
export function createApiPassthrough(options: ApiPassthroughOptions = {}): ApiPassthrough {
  const apiInternalUrl: string = resolveApiInternalUrl(options);
  const prefixes: readonly string[] = options.prefixes ?? DEFAULT_PASSTHROUGH_PREFIXES;
  const normalized: readonly string[] = prefixes.map((entry: string): string =>
    normalizePrefix(entry),
  );
  const forwardClientIp: boolean = options.forwardClientIp ?? true;
  const trusted: TrustedProxies = resolveTrustedProxies(
    options.trustedProxies,
    options.env ?? process.env,
  );

  const matches = (pathname: string): boolean =>
    normalized.some((prefix: string): boolean => pathMatchesPrefix(pathname, prefix));

  return {
    matches,
    apiInternalUrl,
    prefixes,
    handle: async (request: PeerRequest): Promise<Response> => {
      const incoming: URL = new URL(request.url);
      if (!matches(incoming.pathname)) {
        return problemResponse(NOT_FOUND_STATUS, ErrorCode.HTTP_NOT_FOUND, {
          requestId: resolveRequestId(request.headers),
        });
      }

      const headers: Headers = new Headers(request.headers);
      // Strip the inbound Host so fetch derives it from the upstream target.
      headers.delete("host");
      applyForwardedHeaders(
        headers,
        incoming,
        forwardClientIp ? resolveClientAddress(request, request.ip, trusted) : undefined,
      );

      const hasBody: boolean = request.method !== "GET" && request.method !== "HEAD";
      const init: RequestInit = {
        method: request.method,
        headers,
        // The upstream's own 3xx belongs to the browser, not to this hop.
        redirect: "manual",
        ...(hasBody ? { body: await request.arrayBuffer() } : {}),
      };

      try {
        return await fetch(`${apiInternalUrl}${incoming.pathname}${incoming.search}`, init);
      } catch (error: unknown) {
        // The transport's message (undici's `fetch failed`, its cause) is for
        // the log; the browser gets fixed wording under a code it already
        // knows how to render. The record names the upstream base, not the
        // request path or query: `/v1/**` carries one-time tokens in both.
        const requestId: string = resolveRequestId(request.headers);
        console.warn(
          "wallow-passthrough: forward failed",
          redact({
            upstream: apiInternalUrl,
            method: request.method,
            requestId,
            detail: errorMessage(error),
            cause: error instanceof Error ? errorMessage(error.cause) : undefined,
          }),
        );
        return problemResponse(NETWORK_FAILURE_STATUS, ClientErrorCode.TRANSPORT_NETWORK_ERROR, {
          requestId,
        });
      }
    },
  };
}
