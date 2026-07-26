/**
 * SSR self-fetch origin resolution for wallow-web (Wallow-spb5).
 *
 * During a full-page render the SSR host fetches its OWN BFF surface
 * (`/bff/user`, `/api/**`). Node's `fetch` needs an absolute URL, and until now
 * that URL was built from the INCOMING request's origin — i.e. the address the
 * BROWSER used. A host generally cannot reach its own browser-facing origin:
 *
 *   - `docker/docker-compose.test.yml` publishes wallow-web as
 *     `127.0.0.1:5053:3000`, so the browser `Host` is `localhost:5053` while the
 *     container only listens on `localhost:3000`. Self-fetching
 *     `http://localhost:5053/api` inside the container is ECONNREFUSED, which
 *     surfaces as a 500 error boundary instead of a hydrated dashboard.
 *   - The same hazard exists behind any production reverse proxy or TLS
 *     terminator, where the public origin is not the listener's address.
 *
 * This module resolves the internally reachable origin from the environment so
 * the SDK's SSR client (`configureSsrClient`) and the current-user query can
 * target it, falling back to the request origin when nothing indicates a split
 * (`pnpm dev` / Aspire, where the published origin does loop back).
 *
 * Env precedence mirrors wallow-auth's `WALLOW_API_INTERNAL_URL` convention
 * (`auth-server.ts`): an explicit URL wins, an empty value counts as unset.
 */
import type { SsrRequestContext } from "@bc-solutions-coder/sdk";

/** Explicit override for the origin the SSR host reaches itself on. */
export const SSR_INTERNAL_ORIGIN_ENV_KEY = "WALLOW_WEB_INTERNAL_URL";

/**
 * The origin this host can fetch ITSELF on, or `undefined` when the environment
 * gives no reason to believe it differs from the incoming request's origin.
 *
 * Resolution order: {@link SSR_INTERNAL_ORIGIN_ENV_KEY}, then `http://localhost:${PORT}`
 * (the listener the host binds, per `@bc-solutions-coder/web-shell`'s `PORT`
 * handling), then `undefined`.
 */
export function resolveSsrInternalOrigin(
  env: Record<string, string | undefined> = process.env,
): string | undefined {
  const override: string | undefined = env[SSR_INTERNAL_ORIGIN_ENV_KEY];
  if (override !== undefined && override !== "") {
    return override.replace(/\/+$/u, "");
  }
  const port: string | undefined = env.PORT;
  if (port === undefined || !/^\d+$/u.test(port)) {
    return undefined;
  }
  return `http://localhost:${port}`;
}

/**
 * Build the per-request SSR context the app scopes with `AsyncLocalStorage`: the
 * browser-facing request origin, the incoming session cookie, and the internally
 * reachable origin SSR self-fetches must use.
 */
export function createSsrRequestContext(
  request: Request,
  env: Record<string, string | undefined> = process.env,
): SsrRequestContext {
  return {
    origin: new URL(request.url).origin,
    cookie: request.headers.get("cookie") ?? undefined,
    internalOrigin: resolveSsrInternalOrigin(env),
  };
}
