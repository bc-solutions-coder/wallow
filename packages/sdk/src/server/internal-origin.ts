/**
 * Internally reachable origin resolution (Wallow-pu6a.3.5, ported from
 * wallow-web's `src/lib/ssr-origin.ts` per Wallow-spb5).
 *
 * During a full-page render an SSR host fetches its OWN BFF surface
 * (`/bff/user`, `/api/**`). Node's `fetch` needs an absolute URL, and deriving
 * that URL from the INCOMING request's origin — the address the BROWSER used —
 * is wrong whenever the host cannot reach its own public origin:
 *
 *   - `docker/docker-compose.test.yml` publishes wallow-web as
 *     `127.0.0.1:5053:3000`, so the browser `Host` is `localhost:5053` while the
 *     container only listens on `localhost:3000`. Self-fetching
 *     `http://localhost:5053/api` inside the container is ECONNREFUSED.
 *   - The same hazard exists behind any reverse proxy or TLS terminator.
 *
 * This lives in the SDK's server entry so app hosts stop hand-rolling it; the
 * value it returns is what a per-request SDK instance passes as
 * `createWallowSdk({ internalOrigin })`.
 */

/** Explicit override for the origin the SSR host reaches itself on. */
export const INTERNAL_ORIGIN_ENV_KEY = "WALLOW_WEB_INTERNAL_URL";

/**
 * The origin this host can fetch ITSELF on.
 *
 * Resolution order: {@link INTERNAL_ORIGIN_ENV_KEY} (an empty value counts as
 * unset, mirroring wallow-auth's `WALLOW_API_INTERNAL_URL` convention), then
 * `http://localhost:${PORT}` (the listener the host binds — every app's
 * `vite.config.ts` spells out the same `PORT` convention), then the caller-supplied
 * `requestOrigin`, then `undefined` when nothing indicates a reachable origin.
 */
export function resolveInternalOrigin(
  env: Record<string, string | undefined>,
  requestOrigin?: string | undefined,
): string | undefined {
  const override: string | undefined = env[INTERNAL_ORIGIN_ENV_KEY];
  if (override !== undefined && override !== "") {
    return stripTrailingSlashes(override);
  }

  const port: string | undefined = env.PORT;
  if (port !== undefined && /^\d+$/u.test(port)) {
    return `http://localhost:${port}`;
  }

  if (requestOrigin !== undefined && requestOrigin !== "") {
    return stripTrailingSlashes(requestOrigin);
  }

  return undefined;
}

/** Drop trailing slashes so callers never build a `//api` target. */
function stripTrailingSlashes(value: string): string {
  return value.replace(/\/+$/u, "");
}
