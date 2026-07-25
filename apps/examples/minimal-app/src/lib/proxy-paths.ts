/**
 * Reverse-proxy path topology — the single source of truth for which request
 * paths the proxy bridge ({@link createProxyServer}) answers versus which fall
 * through to router SSR. Both hosts (the `pnpm dev` server and the standalone
 * `pnpm start` host) consume this same definition so they can never drift.
 *
 * The bridge owns `/health` (liveness) plus the API surface it forwards verbatim
 * to Wallow.Api: `/v1/**`, `/connect/**`, and `/.well-known/**` (OIDC discovery
 * and JWKS, whose advertised URLs point at this same origin).
 */
export function isProxyRequest(pathname: string): boolean {
  return (
    pathname === "/health" ||
    pathname.startsWith("/v1/") ||
    pathname.startsWith("/connect/") ||
    pathname.startsWith("/.well-known/")
  );
}
