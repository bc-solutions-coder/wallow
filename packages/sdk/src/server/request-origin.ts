/**
 * Derive the browser-visible request origin for SSR. A trusted peer may supply the forwarded scheme; preserving the public origin keeps browser and server query keys consistent.
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
 * Resolve the browser-facing origin of an incoming request.
 *
 * For a trusted socket peer, honors the first X-Forwarded-Proto value when it is http or https.
 * Preserves the request URL host and port; forwarded host headers are not used. Otherwise
 * returns the request URL origin.
 *
 * @param request Incoming request with an absolute URL.
 *
 * @param peer Socket peer supplied by the server runtime.
 *
 * @param trusted Parsed proxy trust ranges.
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
 * Create a reusable browser-origin resolver for SSR requests.
 *
 * Reads WALLOW_TRUSTED_PROXIES from the supplied environment once. The returned function passes
 * request.ip to resolveRequestOrigin, so trusted TLS proxies can supply the browser scheme used
 * in SDK base URLs.
 */
export function createRequestOriginResolver(
  env: Readonly<Record<string, string | undefined>>,
): (request: PeerRequest) => string {
  const trusted: TrustedProxies = parseTrustedProxies(env[TRUSTED_PROXIES_ENV_KEY]);

  return (request: PeerRequest): string => resolveRequestOrigin(request, request.ip, trusted);
}
