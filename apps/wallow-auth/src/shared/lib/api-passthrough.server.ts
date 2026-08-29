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
import {
  createApiPassthrough,
  type ApiPassthrough,
  type PeerRequest,
} from "@bc-solutions-coder/sdk/server/passthrough";

import { BASE_PATH } from "./base-path";

let passthrough: ApiPassthrough | undefined;

/**
 * Forward an API-surface request upstream to `WALLOW_API_INTERNAL_URL`.
 *
 * The request goes through AS srvx handed it over, not as a copy: the
 * passthrough reads the peer address off its `ip` property and resolves the
 * caller through `WALLOW_TRUSTED_PROXIES` itself, so the API's per-IP limiter
 * sees each visitor rather than this proxy as one client. The obvious
 * `new Request(request, { headers })` would also throw `Cannot read private
 * member #state` at runtime — srvx's request only claims to be a `Request`
 * through `Symbol.hasInstance` — and would drop `ip` on the way.
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

  // Rebased in place: srvx's request cannot survive undici's copy constructor,
  // and a copy would lose `ip`. `url` is a getter on `Request.prototype`, so an
  // own data property shadows it.
  const url: URL = new URL(request.url);
  const unprefixed: string = stripBasePath(url.pathname, basePath);
  if (unprefixed !== url.pathname) {
    url.pathname = unprefixed;
    Object.defineProperty(request, "url", { value: url.toString(), configurable: true });
  }

  return passthrough.handle(request);
}
