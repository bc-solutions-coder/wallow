/**
 * Pure reverse-proxy passthrough preset (Wallow-pu6a.3.7).
 *
 * The second of the two golden-path server topologies. Where
 * `createWallowBffServer` owns an OIDC session and attaches a bearer token,
 * {@link createApiPassthrough} owns nothing: it forwards the inbound method,
 * path, query, body, and `Cookie` header to the internal API and returns the
 * upstream `Response` unchanged, so every `Set-Cookie` reaches the browser
 * verbatim. No session store, no cookie jar, no relay.
 *
 * This absorbs the near-duplicate hand-rolled proxies the apps used to ship
 * (the deleted `apps/wallow-auth/src/lib/auth-server.ts` and
 * `apps/minimal-app/src/lib/proxy-server.ts`), generalizing their
 * hardcoded prefix list into {@link ApiPassthroughOptions.prefixes} and their
 * `X-Forwarded-For` stamping into
 * {@link ApiPassthroughOptions.forwardClientIp}.
 *
 * It ships from its OWN subpath (`@bc-solutions-coder/sdk/server/passthrough`)
 * so a passthrough-only app never pulls `openid-client` into its server bundle.
 * Nothing in this module may import the BFF handler/proxy graph.
 */

import {
  applyForwardedHeaders,
  resolveClientAddress,
  resolveTrustedProxies,
  type PeerRequest,
  type TrustedProxies,
} from "./forwarded";

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
  /** Whether this proxy — rather than router SSR — owns the given path. */
  matches: (pathname: string) => boolean;
  /** The resolved upstream base URL. */
  readonly apiInternalUrl: string;
  /** The resolved prefix allowlist, as supplied. */
  readonly prefixes: readonly string[];
}

/** Status answered for a path outside the prefix allowlist. */
const NOT_FOUND_STATUS = 404;

/** Path separator, also the shortest possible normalized prefix. */
const ROOT_PATH: string = "/";

/**
 * The optional subtree wildcard (`/**`) and any trailing slashes at the end of a
 * prefix entry — the part that carries no matching information, since every
 * entry guards its own subtree either way.
 */
const PREFIX_TAIL_PATTERN: RegExp = /(?:\/\*\*)?\/*$/u;

/**
 * Resolve the upstream API base URL: explicit config wins, then
 * `WALLOW_API_INTERNAL_URL`, then {@link DEFAULT_API_INTERNAL_URL}.
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

/**
 * Build the reverse-proxy passthrough.
 *
 * @param options Upstream target, prefix allowlist, and client-IP forwarding.
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
        return new Response(null, { status: NOT_FOUND_STATUS });
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

      return fetch(`${apiInternalUrl}${incoming.pathname}${incoming.search}`, init);
    },
  };
}
