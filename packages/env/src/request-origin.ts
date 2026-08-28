/**
 * The browser-facing origin of an inbound SSR request, honoring the scheme a
 * terminating reverse proxy reports in `X-Forwarded-Proto`.
 *
 * Behind an HTTPS-terminating ingress the app itself is reached over plain HTTP,
 * so `new URL(request.url).origin` says `http` while the browser that will
 * hydrate the page says `https`. The generated TanStack Query keys embed the
 * SDK's `baseUrl` verbatim, so that one-character difference is a cache-key
 * miss and every SSR-prefetched query refetches on hydration.
 *
 * The header is believed only when the immediate peer is inside the deployment's
 * trusted-proxy set — the same gate `resolveClientAddress` puts on
 * `X-Forwarded-For`, so the two forwarded headers are one trust policy rather
 * than two. Even from a trusted peer, only `http` and `https` are honored: a
 * misconfigured ingress can forward what a caller sent, and an unrecognized
 * value would otherwise travel into the SDK's `baseUrl` and into every query key
 * built from it. Falling back to the request's own scheme keeps that input inert.
 */

import {
  isTrustedPeer,
  type PeerRequest,
  parseTrustedProxies,
  type TrustedProxies,
  TRUSTED_PROXIES_ENV_KEY,
} from "./client-address";

/** What a terminating proxy names the scheme the browser actually used. */
const FORWARDED_PROTO_HEADER = "x-forwarded-proto";

/**
 * The only schemes any of these apps is ever served over — an allowlist rather
 * than a sanitizer, because this value is attacker-supplied and lands in the
 * SDK's `baseUrl`.
 */
const SERVED_SCHEMES: ReadonlySet<string> = new Set(["http", "https"]);

/** Trailing `:` of a `url.protocol`-shaped value, which the header itself omits. */
const SCHEME_TERMINATOR = /:$/u;

/**
 * The origin (`https://wallow.dev`, `http://localhost:3000`) the browser sees
 * for `request` — its own origin, unless the immediate `peer` is a trusted
 * proxy that reported a different scheme.
 */
export function resolveRequestOrigin(
  request: Request,
  peer: string | undefined,
  trusted: TrustedProxies,
): string {
  const url: URL = new URL(request.url);
  if (!isTrustedPeer(peer, trusted)) {
    // Any caller can send the header; only a configured proxy may rewrite the
    // origin the SDK builds its query keys from.
    return url.origin;
  }

  const forwarded: string | null = request.headers.get(FORWARDED_PROTO_HEADER);
  if (forwarded === null) {
    return url.origin;
  }

  // Every hop appends its own entry, so the left-most one is the scheme the
  // browser used to reach the outermost proxy.
  const [firstHop = ""] = forwarded.split(",");
  const scheme: string = firstHop.trim().replace(SCHEME_TERMINATOR, "").toLowerCase();
  if (!SERVED_SCHEMES.has(scheme)) {
    // Covers the misconfigured-but-harmless empty header as well as a hostile
    // one; neither is a scheme, so both leave the request's own origin standing.
    return url.origin;
  }

  // `host`, not `hostname`: dropping a non-default port would aim the SDK at :80.
  return `${scheme}://${url.host}`;
}

/**
 * Bind {@link resolveRequestOrigin} to a deployment's trusted-proxy list.
 *
 * The env record is a PARAMETER because this package must not read the
 * environment itself: every app's `start.ts` is aliased into the client module
 * graph as well as the server one, so a `process.env` read at module scope here
 * would either break the client build or leak a server value into it. Bind once
 * in server-only code — the CIDR parse is not per-request work.
 */
export function createRequestOriginResolver(
  env: Readonly<Record<string, string | undefined>>,
): (request: PeerRequest) => string {
  const trusted: TrustedProxies = parseTrustedProxies(env[TRUSTED_PROXIES_ENV_KEY]);

  return (request: PeerRequest): string => resolveRequestOrigin(request, request.ip, trusted);
}
