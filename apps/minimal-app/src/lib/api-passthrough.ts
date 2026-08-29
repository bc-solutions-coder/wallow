/**
 * Same-origin reverse-proxy bridge — the app-specific proxy topology, now a thin
 * wrapper over the SDK's `createApiPassthrough` preset instead of the 139-line
 * hand-rolled h3 app it replaces. It is the simpler of the two golden-path
 * topologies (the other being wallow-web's BFF token tunnel).
 *
 * The three splat server routes (`/v1/$`, `/connect/$`, `/.well-known/$`) all
 * delegate here, so the forwarding rules — method, path, query, body and
 * `Cookie` forwarded verbatim; every upstream `Set-Cookie` returned untouched;
 * `X-Forwarded-Proto`/`Host` filled in only when the client did not send them —
 * live in exactly one place and come from the SDK.
 *
 * The passthrough is built LAZILY and memoised at module scope: constructing it
 * at module load would run inside the Start server bundle's evaluation, where a
 * throw takes down far more than the API routes.
 */
import {
  createApiPassthrough,
  type ApiPassthrough,
  type PeerRequest,
} from "@bc-solutions-coder/sdk/server/passthrough";

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
 * through `Symbol.hasInstance` — and would drop `ip` on the way. This is the
 * scaffold a fork copies, so it needs nothing beyond the SDK.
 */
export function handleApiPassthrough(request: PeerRequest): Promise<Response> {
  passthrough ??= createApiPassthrough();

  return passthrough.handle(request);
}
