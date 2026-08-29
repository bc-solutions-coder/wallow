/**
 * The `X-Forwarded-*` rules shared by BOTH server topologies (Wallow-vufu.4.2).
 *
 * These rules were born in `passthrough.ts`, which is the only place that had
 * them: the BFF's own `/api` proxy forwarded a two-header allowlist
 * (`content-type`, `accept`) and nothing else, so every request it made reached
 * the API wearing the proxy's own address and the API's rate limiter treated a
 * whole app's users as one client. They live here rather than in either proxy
 * so both hops apply one identical rule set.
 *
 * This module is deliberately dependency-free — no config, no session, no
 * fetch, no environment read at module scope — so `passthrough.ts` can import
 * it without violating its own rule that nothing on the `/server/passthrough`
 * subpath may reach into the BFF handler/proxy graph, and so an isomorphic
 * Start entry (`start.ts`, which is aliased into the CLIENT graph too) can
 * import the origin resolver from the `./server/forwarded` subpath without
 * pulling `openid-client` into the browser bundle.
 *
 * The trust decision behind both forwarded headers lives beside them:
 * `x-forwarded-for` and `x-forwarded-proto` are believed only when the
 * immediate peer is inside the deployment's trusted-proxy set
 * (`WALLOW_TRUSTED_PROXIES`) — one policy for both, re-exported here from
 * `./client-address` and `./request-origin`.
 */

export {
  createClientAddressResolver,
  resolveTrustedProxies,
  isTrustedPeer,
  parseTrustedProxies,
  resolveClientAddress,
  TRUST_NO_PROXIES,
  TRUSTED_PROXIES_ENV_KEY,
  type PeerRequest,
  type TrustedProxies,
} from "./client-address";
export { createRequestOriginResolver, resolveRequestOrigin } from "./request-origin";

/**
 * A header a host used to stamp the peer address onto before the SDK read the
 * peer itself. No host stamps it any more, and nothing reads it: it is stripped
 * so a caller who sends it — the only remaining author — cannot smuggle a
 * self-chosen address past this hop.
 */
const STRIPPED_CLIENT_IP_HEADER: string = "x-wallow-client-ip";

/**
 * Set `X-Forwarded-Proto`/`X-Forwarded-Host` for the upstream hop, deriving each
 * from the inbound request ONLY when the client did not already send it. An
 * outer TLS-terminating ingress is the only hop that knows the browser's real
 * scheme, so its header must win — overwriting it with this proxy's own
 * plain-HTTP leg would downgrade the API's view to `http` and trip OpenIddict's
 * HTTPS check (ID2083).
 *
 * `X-Forwarded-For` follows the same append-not-overwrite rule: `clientAddress`
 * — the caller as {@link resolveClientAddress} settled it from the socket peer
 * and the trusted-proxy list — is APPENDED to any inbound chain, so an outer
 * ingress's entries survive ahead of it while the API, which pops the
 * rightmost entry, keys on the address this hop vouches for. `undefined` (no
 * peer known) appends nothing: a bogus entry is worse for the rate limiter
 * than none.
 */
export function applyForwardedHeaders(
  headers: Headers,
  incoming: URL,
  clientAddress: string | undefined,
): void {
  if (!headers.has("x-forwarded-proto")) {
    headers.set("x-forwarded-proto", incoming.protocol.replace(":", ""));
  }
  if (!headers.has("x-forwarded-host")) {
    headers.set("x-forwarded-host", incoming.host);
  }

  if (clientAddress !== undefined && clientAddress !== "") {
    const existing: string | null = headers.get("x-forwarded-for");
    headers.set(
      "x-forwarded-for",
      existing !== null && existing !== "" ? `${existing}, ${clientAddress}` : clientAddress,
    );
  }
  headers.delete(STRIPPED_CLIENT_IP_HEADER);
}
