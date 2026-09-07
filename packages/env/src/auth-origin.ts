/**
 * Resolve the public sign-in app URL from a supplied environment and publish it to the browser
 * before hydration. The URL can include a deployment base path.
 */

import { publishedGlobalScript, readPublishedGlobal } from "./published-global";

/** The environment variable {@link resolveAuthUrl} reads, by name. */
export const AUTH_URL_ENV_KEY = "WALLOW_AUTH_URL";

/** The auth app's local dev listener — the backend's `ServiceUrls:AuthUrl` default. */
export const DEFAULT_AUTH_URL = "http://localhost:3002";

/** Drop trailing slashes so callers never build a `//login` href. */
function stripTrailingSlashes(value: string): string {
  return value.replace(/\/+$/u, "");
}

/**
 * Return the trimmed WALLOW_AUTH_URL without trailing slashes. Missing or whitespace-only values
 * use http://localhost:3002. Accepts a URL with a base path and does not validate its scheme or
 * host.
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

/**
 * Create inline script source that publishes the auth app URL on the browser global. Render it
 * before hydration, then use readInjectedAuthUrl in the browser. Escapes less-than characters
 * through publishedGlobalScript; does not execute the script.
 */
export function authUrlScript(url: string): string {
  return publishedGlobalScript(AUTH_URL_GLOBAL_KEY, url);
}

/**
 * Read the auth URL from a supplied browser scope, usually globalThis. Returns undefined when the
 * published value is missing or not a nonblank string. Does not validate the URL or trim the
 * returned value.
 */
export function readInjectedAuthUrl(scope: unknown): string | undefined {
  const injected: unknown = readPublishedGlobal(AUTH_URL_GLOBAL_KEY, scope);
  return typeof injected === "string" && injected.trim() !== "" ? injected : undefined;
}
