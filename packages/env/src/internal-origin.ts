/**
 * Resolve the origin an SSR host uses to fetch itself. The app supplies environment values at
 * request time; a public browser origin may be unreachable inside its container.
 */

/** Explicit override for the origin the SSR host reaches itself on. */
export const INTERNAL_ORIGIN_ENV_KEY = "WALLOW_WEB_INTERNAL_URL";

/** Drop trailing slashes so callers never build a `//api` target. */
function stripTrailingSlashes(value: string): string {
  return value.replace(/\/+$/u, "");
}

/**
 * Resolve the app self-fetch origin from WALLOW_WEB_INTERNAL_URL, then a digits-only PORT, then
 * requestOrigin. Removes trailing slashes from explicit origins and returns undefined when no
 * source is available.
 *
 * The override is not trimmed or URL-validated. Supply requestOrigin only when the server can
 * reach its own public address; WALLOW_WEB_INTERNAL_URL names the app, not the upstream API.
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
