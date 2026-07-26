/**
 * Reverse-proxy topology for wallow-web (Wallow-ffpq.3.8).
 *
 * The single source of truth for which request paths the BFF bridge answers
 * versus which fall through to the router SSR. Both hosts — the `pnpm dev`
 * server (`dev-server.ts`) and the standalone production host (`server.ts`) —
 * currently inline this decision as a private `isBffRequest`; this module
 * exists so the topology is defined ONCE and unit-tested, and so the two hosts
 * consume the same definition rather than drifting copies.
 *
 * wallow-web's topology deliberately DIFFERS from wallow-auth's reverse proxy
 * (`auth-server.ts`, which passes `/v1/**`, `/connect/**`, `/.well-known/**`
 * verbatim through to Wallow.Api):
 *
 *   - There is no WebSocket-upgrade circuit in the React app, so legacy upgrade
 *     paths are never a proxy path — they are not routed anywhere and must fall
 *     through like any other unknown path.
 *   - `/api/**` is answered by the BFF's OWN handler (`bff-server.ts`'s
 *     `createApiProxy`, which attaches a Bearer token server-side and does
 *     silent refresh), NOT reverse-proxied verbatim to Wallow.Api. Naively
 *     copying wallow-auth's proxy would double-proxy `/api/**`.
 */

/**
 * Whether a request path is answered by the BFF bridge (`handleBffRequest`)
 * rather than the router SSR. The BFF bridge owns `/health`, `/bff/*`, and
 * `/api/**` — and nothing else. Legacy WebSocket-upgrade paths are intentionally absent.
 */
export function isBffProxyPath(pathname: string): boolean {
  return pathname === "/health" || pathname.startsWith("/bff/") || pathname.startsWith("/api/");
}
