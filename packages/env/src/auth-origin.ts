/**
 * The public origin of the deployment's SIGN-IN app (apps/wallow-auth), for
 * pages that link a person over to it — wallow-web's "Preview sign-in" button
 * on the client-branding editor.
 *
 * Resolved from `WALLOW_AUTH_URL` because the two apps share no build: one
 * wallow-web image is run against `https://wallow.dev/auth` in production and
 * `http://localhost:3002` on a laptop, and rebuilding to move a link is not a
 * deployment step anyone should need. The default is the auth app's local dev
 * listener, the same one the backend's `ServiceUrls:AuthUrl` defaults to.
 *
 * The env record is a PARAMETER, never a `process.env` read of this module's
 * own (see this package's CLAUDE.md): the caller is a Start app's `start.ts`,
 * aliased into the client bundle too, so the one read stays inside the
 * server-only callback. The value then crosses to the browser the same way the
 * fork links do — the server renders {@link authUrlScript} into `<head>`, the
 * browser reads it back with {@link readInjectedAuthUrl} — because an href
 * that differs between the SSR pass and the hydrating render is a hydration
 * mismatch.
 */

/** The environment variable {@link resolveAuthUrl} reads, by name. */
export const AUTH_URL_ENV_KEY = "WALLOW_AUTH_URL";

/** The auth app's local dev listener — the backend's `ServiceUrls:AuthUrl` default. */
export const DEFAULT_AUTH_URL = "http://localhost:3002";

/** Drop trailing slashes so callers never build a `//login` href. */
function stripTrailingSlashes(value: string): string {
  return value.replace(/\/+$/u, "");
}

/**
 * The sign-in app's public origin for ONE deployment: `WALLOW_AUTH_URL` if the
 * environment names it, else {@link DEFAULT_AUTH_URL}. A variable set to blank
 * counts as unset — that is what an unsubstituted `WALLOW_AUTH_URL=` in a
 * compose env file produces, and a link with no origin is worse than the
 * default one.
 */
export function resolveAuthUrl(env: Readonly<Record<string, string | undefined>> = {}): string {
  const value: string | undefined = env[AUTH_URL_ENV_KEY];
  return value !== undefined && value.trim() !== ""
    ? stripTrailingSlashes(value.trim())
    : DEFAULT_AUTH_URL;
}

/**
 * The global property a server-rendered document publishes {@link resolveAuthUrl}'s
 * answer on. Only the BROWSER ever holds it — the server renders it as text and
 * never assigns it, because a server global is shared by every concurrent
 * request.
 */
export const AUTH_URL_GLOBAL_KEY = "__WALLOW_AUTH_URL__";

/** `<` as a JavaScript string escape — the one character an inline script must not carry. */
const LT_ESCAPE = String.raw`\u003c`;

/**
 * The source of the inline `<script>` that publishes one deployment's auth
 * origin, rendered in `<head>` so it runs before hydration. The returned
 * source contains no `<`: React does not escape a text child of `<script>`,
 * so a URL containing `</script` would otherwise end the element early.
 */
export function authUrlScript(url: string): string {
  const payload: string = JSON.stringify(url).replaceAll("<", LT_ESCAPE);
  return `window[${JSON.stringify(AUTH_URL_GLOBAL_KEY)}]=${payload};`;
}

/**
 * The origin {@link authUrlScript} published, read back off a scope —
 * `globalThis` in a browser — or `undefined` when nothing published one.
 * `undefined` is the answer for anything that is not a non-blank string: the
 * caller's fallback is always usable, so a malformed global costs the
 * deployment's override rather than the href.
 */
export function readInjectedAuthUrl(scope: unknown): string | undefined {
  if (typeof scope !== "object" || scope === null) {
    return undefined;
  }
  const injected: unknown = (scope as Record<string, unknown>)[AUTH_URL_GLOBAL_KEY];
  return typeof injected === "string" && injected.trim() !== "" ? injected : undefined;
}
