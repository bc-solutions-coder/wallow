/**
 * Shared forwarding headers for the BFF proxy and session-free passthrough. Keep this module independent of BFF authentication dependencies so both entrypoints use the same forwarding rules.
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
 * Strip the unsupported x-wallow-client-ip header; client addresses come from request.ip and the
 * trusted proxy chain.
 */
const STRIPPED_CLIENT_IP_HEADER: string = "x-wallow-client-ip";

/**
 * Update outgoing forwarded headers in place.
 *
 * Preserves existing X-Forwarded-Proto and X-Forwarded-Host values, filling absent values from
 * incoming. Appends a nonempty clientAddress to X-Forwarded-For and removes x-wallow-client-ip.
 * This function does not authenticate incoming forwarded headers.
 *
 * @param headers Mutable outgoing request headers.
 *
 * @param incoming Inbound URL used for missing scheme and host values.
 *
 * @param clientAddress Trusted caller address resolved from the socket peer, or undefined to
 * append nothing.
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
