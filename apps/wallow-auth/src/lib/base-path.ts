/**
 * The one knob that lets this app be served under a URL prefix — `AUTH_BASE_PATH`
 * — reduced to the four string shapes the rest of the app needs.
 *
 * Why a build-time knob and not a runtime one: the prefix has to be baked into
 * every asset URL Vite emits, so it is read from `process.env.AUTH_BASE_PATH` by
 * `vite.config.ts` and travels into both bundles as Vite's own
 * `import.meta.env.BASE_URL`. A container started with a different value would
 * still serve HTML pointing at the old prefix, which is why the Dockerfile takes
 * it as an `ARG` promoted to `ENV` before `pnpm build` rather than as a runtime
 * `ENV`.
 *
 * This module is imported from THREE contexts and must stay pure string work for
 * all of them:
 *
 *  - `vite.config.ts`, evaluated as plain Node ESM where `import.meta.env` does
 *    not exist at all (hence the optional read behind {@link BASE_PATH});
 *  - the SSR bundle (`start.ts`, the passthrough wrapper);
 *  - the client bundle (`router.tsx`).
 *
 * So: no `node:` imports, no `process.env` reads outside the config's own call.
 */

/** Environment variable naming the URL prefix this app is served under. */
export const AUTH_BASE_PATH_ENV_KEY: string = "AUTH_BASE_PATH";

/**
 * Reduce a configured prefix to the one canonical shape everything else here
 * takes: either the empty string (no prefix — the default, unchanged behavior)
 * or a leading-slash path with no trailing slash (`/auth`).
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
 * The passthrough needs this because TanStack Start rebases the pathname it
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
 * this app's passthrough actually answers.
 */
export function withBasePath(origin: string, basePath: string): string {
  return `${origin}${basePath}`;
}

/**
 * This build's base path, in {@link normalizeBasePath} shape.
 *
 * Read from Vite's `import.meta.env.BASE_URL` rather than from
 * `process.env.AUTH_BASE_PATH`: the client bundle has no `process`, and
 * `BASE_URL` is the value Vite already derived from the `base` this same module
 * computed for the config — so there is one source of truth rather than two that
 * can disagree. Guarded, because `vite.config.ts` imports this module in a plain
 * Node context where `import.meta.env` is `undefined`.
 */
export const BASE_PATH: string = normalizeBasePath(import.meta.env?.BASE_URL);

/**
 * Prefix a root-relative, app-internal `href` with this build's base path.
 *
 * A literal `href="/login"` is invisible to the router, so it keeps pointing at
 * the SITE root under a based build — and behind the path-based ingress this
 * whole knob exists for, the site root is a DIFFERENT app (wallow-web), which
 * throws the user out of the auth flow mid-login.
 *
 * These stay plain anchors rather than becoming router `Link`s on purpose: every
 * one of them crosses an auth boundary whose route work happens on the server
 * (terms and privacy also open in a new tab), so a full document load is the
 * behavior being kept — the only thing missing was the prefix.
 */
export function toAppHref(path: string, basePath: string = BASE_PATH): string {
  return `${basePath}${path}`;
}
