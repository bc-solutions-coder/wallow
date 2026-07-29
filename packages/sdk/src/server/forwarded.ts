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
 * fetch — so `passthrough.ts` can import it without violating its own rule that
 * nothing on the `/server/passthrough` subpath may reach into the BFF
 * handler/proxy graph.
 */

/**
 * Internal request header carrying the immediate peer's socket address, stamped
 * by the Node host before it calls a proxy — a WHATWG `Request` has no socket,
 * so neither proxy can read the peer address itself. The value is APPENDED to
 * any inbound `X-Forwarded-For` chain (so an outer ingress's leftmost
 * real-client entry survives) and then STRIPPED, so the internal seam header
 * never reaches the upstream API.
 */
export const CLIENT_IP_HEADER: string = "x-wallow-client-ip";

/**
 * Set `X-Forwarded-Proto`/`X-Forwarded-Host` for the upstream hop, deriving each
 * from the inbound request ONLY when the client did not already send it. An
 * outer TLS-terminating ingress is the only hop that knows the browser's real
 * scheme, so its header must win — overwriting it with this proxy's own
 * plain-HTTP leg would downgrade the API's view to `http` and trip OpenIddict's
 * HTTPS check (ID2083).
 *
 * `X-Forwarded-For` follows the same append-not-overwrite rule: the Node host
 * stamps this hop's real peer address into {@link CLIENT_IP_HEADER}, which is
 * APPENDED to any inbound chain so an outer ingress's leftmost real-client entry
 * survives. The seam header is then STRIPPED either way, so it never reaches the
 * upstream API.
 *
 * @param headers The outgoing headers, mutated in place.
 * @param incoming The inbound request URL, the source of the proto/host fallback.
 * @param forwardClientIp Whether to append the seam header's address to the
 *   chain. The seam header is stripped regardless.
 */
export function applyForwardedHeaders(
  headers: Headers,
  incoming: URL,
  forwardClientIp: boolean,
): void {
  if (!headers.has("x-forwarded-proto")) {
    headers.set("x-forwarded-proto", incoming.protocol.replace(":", ""));
  }
  if (!headers.has("x-forwarded-host")) {
    headers.set("x-forwarded-host", incoming.host);
  }

  const clientIp: string | null = headers.get(CLIENT_IP_HEADER);
  if (forwardClientIp && clientIp !== null && clientIp !== "") {
    const existing: string | null = headers.get("x-forwarded-for");
    headers.set(
      "x-forwarded-for",
      existing !== null && existing !== "" ? `${existing}, ${clientIp}` : clientIp,
    );
  }
  headers.delete(CLIENT_IP_HEADER);
}
