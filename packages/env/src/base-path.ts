/**
 * The string arithmetic behind serving an app under a URL prefix.
 *
 * Four total functions over plain strings — no environment read, no import, no
 * side effect — because the one caller that matters most is a `vite.config.ts`,
 * evaluated as plain Node ESM before any bundle exists. A module that could
 * trigger validation, or reach `process`, would make `vite build` start failing
 * on a missing RUNTIME variable, which is exactly backwards.
 *
 * What lives here is the SHAPE of a base path. What this build's base path
 * actually IS stays in the consuming app, for a reason worth knowing before
 * moving it: Vite replaces `import.meta.env.BASE_URL` with a literal at build
 * time, and a library build has no base, so a `BASE_PATH` constant compiled into
 * this package's `dist/` would freeze to `"/"` and silently break every based
 * build downstream. In-repo it would appear to work, because apps resolve this
 * package from source — a landmine that only detonates at publish.
 */

/**
 * Reduce a configured prefix to the one canonical shape everything else takes:
 * either the empty string (no prefix — the default) or a leading-slash path with
 * no trailing slash (`/auth`).
 *
 * Accepts what a fork would plausibly write — `auth`, `/auth`, `/auth/`, `/` —
 * because this value is copied by hand into a compose file and a CI build arg,
 * and a stray slash there must not change how the app serves.
 */
export function normalizeBasePath(value: string | undefined): string {
  const trimmed: string = (value ?? "").trim();
  const bare: string = trimmed.replace(/^\/+/u, "").replace(/\/+$/u, "");

  return bare === "" ? "" : `/${bare}`;
}

/**
 * The value Vite's own `base` option takes, which unlike {@link normalizeBasePath}
 * wants the TRAILING slash and spells "no prefix" as `/`.
 */
export function toViteBase(basePath: string): string {
  return basePath === "" ? "/" : `${basePath}/`;
}

/**
 * Remove the base path from an inbound pathname, on segment boundaries only.
 *
 * A passthrough needs this because TanStack Start rebases the pathname it
 * MATCHES against the route tree but hands the server handler the ORIGINAL
 * request, so a browser fetch of `/auth/v1/me` arrives at the `/v1/$` route with
 * its URL still carrying the prefix — and the upstream API knows nothing about
 * it.
 *
 * Segment boundaries only, for the same reason the SDK's own prefix allowlist
 * matches that way: `/authentic` is not below `/auth`.
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
 * SDK issues its `/v1/**` calls against. `https://wallow.dev` + `/auth` is where
 * a based app's passthrough actually answers.
 */
export function withBasePath(origin: string, basePath: string): string {
  return `${origin}${basePath}`;
}
