/**
 * Pure helpers for URL prefixes. The consuming app supplies its base path; this module reads no
 * environment or build-time globals.
 */

/**
 * Trim whitespace and outer slashes to produce an empty string for the root or a leading-slash
 * prefix without a trailing slash. Accepts auth, /auth, /auth/, and /. Does not validate or
 * normalize internal path segments.
 */
export function normalizeBasePath(value: string | undefined): string {
  const trimmed: string = (value ?? "").trim();
  const bare: string = trimmed.replace(/^\/+/u, "").replace(/\/+$/u, "");

  return bare === "" ? "" : `/${bare}`;
}

/**
 * Convert a normalized base path to Vite base syntax. An empty prefix becomes /; any other value
 * receives a trailing slash. Pass the result of normalizeBasePath.
 */
export function toViteBase(basePath: string): string {
  return basePath === "" ? "/" : `${basePath}/`;
}

/**
 * Remove a normalized base path from a pathname on a segment boundary. A request for the prefix
 * itself becomes /. Unmatched paths are returned unchanged, so /auth does not strip /authentic.
 */
export function stripBasePath(pathname: string, basePath: string): string {
  if (basePath === "" || !pathname.startsWith(basePath)) {
    return pathname;
  }

  const remainder: string = pathname.slice(basePath.length);
  if (remainder === "" || remainder === "/") {
    return "/";
  }

  // Anything else that survives `startsWith` matched mid-segment (`/authentic`
  // against `/auth`), so the prefix was never really there.
  return remainder.startsWith("/") ? remainder : pathname;
}

/**
 * Prefix a browser-facing origin with the base path, producing the base URL the
/**
 * Append a normalized base path to an origin. Pass an origin without a trailing slash and a prefix
 * from normalizeBasePath; this helper concatenates the inputs without validation.
 */
export function withBasePath(origin: string, basePath: string): string {
  return `${origin}${basePath}`;
}
