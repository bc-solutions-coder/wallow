/**
 * The origin an SSR host can fetch ITSELF on, when it differs from the
 * browser-facing one.
 *
 * During a full-page render a Start app fetches its own API surface (`/bff/**`,
 * `/api/**`, `/v1/**`). Node's `fetch` needs an absolute URL, and deriving that
 * URL from the INCOMING request's origin — the address the BROWSER used — is
 * wrong whenever the host cannot reach its own public origin:
 *
 *   - `docker/docker-compose.test.yml` publishes wallow-web as
 *     `127.0.0.1:5053:3000`, so the browser `Host` is `localhost:5053` while the
 *     container only listens on `localhost:3000`. Self-fetching
 *     `http://localhost:5053/api` inside the container is ECONNREFUSED and every
 *     SSR'd page falls back to an error boundary.
 *   - The same hazard exists behind any reverse proxy or TLS terminator.
 *
 * The env record is a PARAMETER rather than a `process.env` read of this
 * module's own, and that is what lets this module ship here at all: every
 * caller is a Start app's `start.ts`, which Start aliases into the CLIENT
 * bundle too. The read stays at the call site, inside the server-only callback
 * the browser never runs.
 */

/** Explicit override for the origin the SSR host reaches itself on. */
export const INTERNAL_ORIGIN_ENV_KEY = "WALLOW_WEB_INTERNAL_URL";

/** Drop trailing slashes so callers never build a `//api` target. */
function stripTrailingSlashes(value: string): string {
  return value.replace(/\/+$/u, "");
}

/**
 * The origin this host can fetch ITSELF on.
 *
 * Resolution order: {@link INTERNAL_ORIGIN_ENV_KEY} (an empty value counts as
 * unset, mirroring the `WALLOW_API_INTERNAL_URL` convention), then
 * `http://localhost:${PORT}` (the listener the host binds — every app's
 * `vite.config.ts` spells out the same `PORT` convention), then the
 * caller-supplied `requestOrigin`, then `undefined` when nothing indicates a
 * reachable origin.
 *
 * `requestOrigin` is the arm a Start app deliberately omits: the browser's
 * origin is exactly the address that is unreachable from inside a container
 * publishing a different host port. Pass it only where the host is known to be
 * reachable on the address the browser used.
 */
export function resolveInternalOrigin(
  env: Readonly<Record<string, string | undefined>>,
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
