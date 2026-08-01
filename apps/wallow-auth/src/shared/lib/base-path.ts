import { normalizeBasePath } from "@bc-solutions-coder/env/base-path";

/**
 * THIS build's base path — the half of `AUTH_BASE_PATH` support only the app can
 * own. The string arithmetic around it (`normalizeBasePath`, `toViteBase`,
 * `stripBasePath`, `withBasePath`) lives in `@bc-solutions-coder/env/base-path`;
 * import from there directly rather than re-exporting it through here.
 *
 * Why a build-time knob and not a runtime one: the prefix has to be baked into
 * every asset URL Vite emits, so it is read from `process.env.AUTH_BASE_PATH` by
 * `vite.config.ts` and travels into both bundles as Vite's own
 * `import.meta.env.BASE_URL`. A container started with a different value would
 * still serve HTML pointing at the old prefix, which is why the Dockerfile takes
 * it as an `ARG` promoted to `ENV` before `pnpm build` rather than as a runtime
 * `ENV`.
 *
 * That bundler substitution is also why {@link BASE_PATH} cannot move into the
 * package: Vite replaces `import.meta.env.BASE_URL` with a LITERAL at build time,
 * and a library build has no base, so the published `dist/` would freeze it to
 * `"/"` and silently break every based build downstream.
 */

/** Environment variable naming the URL prefix this app is served under. */
export const AUTH_BASE_PATH_ENV_KEY: string = "AUTH_BASE_PATH";

/**
 * This build's base path, in {@link normalizeBasePath} shape.
 *
 * Read from Vite's `import.meta.env.BASE_URL` rather than from
 * `process.env.AUTH_BASE_PATH`: the client bundle has no `process`, and
 * `BASE_URL` is the value Vite already derived from the `base` `vite.config.ts`
 * computed — so there is one source of truth rather than two that can disagree.
 * Guarded, because `vite.config.ts` imports this module in a plain Node context
 * where `import.meta.env` is `undefined`.
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
