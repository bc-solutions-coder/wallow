/**
 * The app's BFF host wiring — everything an external relying party needs is the
 * SDK preset itself. `createWallowBffServer()` loads its config from the
 * environment (`OIDC_*`, `COOKIE_*`, `BFF_API_BASE_URL`), connects to
 * Valkey/Redis when `REDIS_URL` is set (server-side sessions, real logout
 * revocation, back-channel logout support) and otherwise falls back to sealed
 * cookie sessions, and mounts login/callback/user/logout plus the front- and
 * back-channel logout receivers under `/bff` and the bearer-attaching proxy
 * under `/api`.
 *
 * Built on FIRST USE and memoised, never at module load: a Start server-route
 * module is evaluated as part of the server bundle, where a config throw would
 * take down SSR and every other route with it. `??=` does not cache a throw, so
 * a misconfigured environment fails per request, not permanently.
 */
import {
  createWallowBffServer,
  type PeerRequest,
  type WallowBffServer,
} from "@bc-solutions-coder/sdk/server";

let server: WallowBffServer | undefined;

function getBffServer(): WallowBffServer {
  server ??= createWallowBffServer();
  return server;
}

/** Handle a request under `/bff` — the OIDC tunnel (login, callback, user, logout). */
export function handleBffRequest(request: Request): Promise<Response> {
  return getBffServer().handleBff(request);
}

/**
 * Handle a request under `/api` — the reverse proxy that attaches the session's
 * bearer. The request goes through AS the server runtime handed it over, not as
 * a copy: the proxy reads the peer address off its `ip` property, and a
 * `new Request(request, …)` clone would both throw at runtime and drop it.
 */
export function handleApiRequest(request: PeerRequest): Promise<Response> {
  return getBffServer().handleApi(request);
}

/**
 * Handle `GET /health`. Goes through the preset so a misconfigured environment
 * makes the container healthcheck FAIL instead of reporting a healthy app that
 * cannot serve a login.
 */
export function handleHealthRequest(): Response {
  return getBffServer().handleHealth();
}
