/**
 * Same-origin reverse-proxy bridge for wallow-auth — a thin wrapper over the
 * SDK's `createApiPassthrough` preset, replacing the 201-line hand-rolled h3 app
 * (`auth-server.ts`) it retires. This app is a PURE passthrough proxy, not a BFF
 * token tunnel like wallow-web's: it holds no session and no cookie jar.
 *
 * The three splat server routes (`/v1/$`, `/connect/$`, `/.well-known/$`) all
 * delegate here, so the forwarding rules — method, path, query, body and
 * `Cookie` forwarded verbatim; every upstream `Set-Cookie` returned untouched;
 * `X-Forwarded-Proto`/`Host` filled in only when the client did not send them —
 * live in exactly one place and come from the SDK. `/.well-known/**` is part of
 * that list because the discovery document's `jwks_uri` advertises this same
 * origin, so the signing keys must resolve here too.
 *
 * The passthrough is built LAZILY and memoised at module scope: constructing it
 * at module load would run inside the Start server bundle's evaluation, where a
 * throw takes down far more than the API routes.
 */
import { stripBasePath } from "@bc-solutions-coder/env/base-path";
import { type PeerRequest } from "@bc-solutions-coder/env/client-address";
import {
  CLIENT_IP_HEADER,
  createApiPassthrough,
  type ApiPassthrough,
} from "@bc-solutions-coder/sdk/server/passthrough";

import { BASE_PATH } from "./base-path";
import { clientAddressFor } from "./client-address.server";

let passthrough: ApiPassthrough | undefined;

/**
 * Forward an API-surface request upstream to `WALLOW_API_INTERNAL_URL`.
 *
 * The client IP is stamped onto {@link CLIENT_IP_HEADER} before handing over:
 * the passthrough appends that header to the outgoing `X-Forwarded-For` chain
 * and strips the seam header itself, but it can only do so if the host supplies
 * the address — without this the API rate-limits every request as if it came
 * from this proxy (Wallow-tt5j).
 *
 * Behind an ingress the peer is the INGRESS, so the address is resolved through
 * `clientAddressFor` rather than read off the connection: it consults the inbound
 * `X-Forwarded-For` only when the peer is a proxy `WALLOW_TRUSTED_PROXIES` names, so an
 * untrusted caller cannot stamp a chosen address into the API's rate-limit key
 * (Wallow-tvn3).
 *
 * The header is set ON THE INBOUND REQUEST rather than on a clone. The obvious
 * `new Request(request, { headers })` throws `Cannot read private member #state`
 * at runtime: srvx's request is its own class that only claims to be a `Request`
 * through `Symbol.hasInstance`, so undici's copy constructor passes the instance
 * check and then reads a private field that does not exist. Mutating is also
 * safe — the passthrough copies the headers before touching them, and the
 * request object is per-request and dead once this returns.
 *
 * @param request The inbound request, as srvx hands it to the server route.
 * @param basePath The URL prefix this app is served under, which must be removed
 *   before the request reaches the SDK: TanStack Start rebases the pathname it
 *   MATCHES against the route tree but passes the handler the original request,
 *   and the upstream API knows nothing about the prefix. Defaults to this
 *   build's {@link BASE_PATH}; the routes never pass it.
 */
export function handleApiPassthrough(
  request: PeerRequest,
  basePath: string = BASE_PATH,
): Promise<Response> {
  passthrough ??= createApiPassthrough();

  // Rebased in place, for the same reason the client-IP header is set in place:
  // srvx's request cannot survive undici's copy constructor. `url` is a getter on
  // `Request.prototype`, so an own data property shadows it.
  const url: URL = new URL(request.url);
  const unprefixed: string = stripBasePath(url.pathname, basePath);
  if (unprefixed !== url.pathname) {
    url.pathname = unprefixed;
    Object.defineProperty(request, "url", { value: url.toString(), configurable: true });
  }

  const clientIp: string | undefined = clientAddressFor(request);
  if (clientIp !== undefined && clientIp !== "") {
    request.headers.set(CLIENT_IP_HEADER, clientIp);
  } else {
    // Removed, not left alone. The seam header is a plain request header, so a
    // caller can send one; the proxy appends whatever it finds there to the
    // outbound chain and the API believes it. Stamping over it covers that on
    // every request that HAS a peer address, and this covers the rest.
    request.headers.delete(CLIENT_IP_HEADER);
  }

  return passthrough.handle(request);
}
