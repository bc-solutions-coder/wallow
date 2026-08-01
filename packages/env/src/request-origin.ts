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
 * Only `http` and `https` are honored. The header is attacker-supplied on any
 * deployment whose ingress does not overwrite it, and an unrecognized value
 * would otherwise travel into the SDK's `baseUrl` and into every query key built
 * from it; falling back to the request's own scheme keeps that input inert.
 */

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
 * The origin (`https://wallow.dev`, `http://localhost:3000`) the browser sees for
 * `request` — its own origin, unless a proxy in front reported a different
 * scheme.
 */
export function resolveRequestOrigin(request: Request): string {
  const url: URL = new URL(request.url);
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
