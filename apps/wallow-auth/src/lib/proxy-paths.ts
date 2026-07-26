/**
 * Reverse-proxy path topology for wallow-auth (Wallow-xhw3).
 *
 * The single source of truth for which request paths the reverse-proxy bridge
 * ({@link createAuthServer}) answers versus which fall through to the router
 * SSR. Both hosts — the `pnpm dev` server (`dev-server.ts`) and the standalone
 * production/E2E host (`server.ts`) — previously inlined this decision as a
 * private `isProxyRequest`, and the two copies drifted: `server.ts` included
 * `/.well-known/**` (Wallow-vec7.5.1.1) while `dev-server.ts` omitted it, so
 * Dev OIDC discovery and JWKS 404'd as text/html under `pnpm dev`.
 *
 * This module exists so the topology is defined ONCE and unit-tested, and so
 * the two hosts consume the same definition rather than drifting again — the
 * root cause, not merely the missing prefix.
 *
 * The bridge passes `/v1/**`, `/connect/**`, and `/.well-known/**` verbatim
 * through to Wallow.Api, plus the `/health` liveness route. `/.well-known/**`
 * matters because the discovery document's `jwks_uri` advertises this same
 * origin, so the signing keys must resolve here too.
 */

/**
 * Whether a request path is answered by the reverse-proxy bridge rather than
 * router SSR. The bridge owns `/health`, `/v1/**`, `/connect/**`, and
 * `/.well-known/**` — and nothing else.
 */
export function isProxyRequest(pathname: string): boolean {
  return (
    pathname === "/health" ||
    pathname.startsWith("/v1/") ||
    pathname.startsWith("/connect/") ||
    pathname.startsWith("/.well-known/")
  );
}
