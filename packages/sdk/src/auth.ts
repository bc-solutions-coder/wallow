/**
 * Browser auth helpers that talk to the same-origin BFF tunnel.
 */

import { getCsrfToken } from "./csrf";

/** HTTP 401 Unauthorized — the BFF returns this when no session is active. */
const HTTP_UNAUTHORIZED: number = 401;

/** First status the BFF uses to refuse a request outright (403 CSRF, 405 method). */
const HTTP_FIRST_ERROR: number = 400;

/**
 * Header the BFF's CSRF gate reads. Duplicated from the server entry's
 * `CSRF_HEADER` on purpose: importing `./server/proxy` here would drag the Node
 * BFF into the browser bundle.
 */
const CSRF_HEADER: string = "x-csrf-token";

/**
 * Suffix of the non-HttpOnly double-submit cookie the BFF writes alongside the
 * session. Its full name is `${cookieName}-csrf`, and `cookieName` is a server
 * setting the browser never learns (it also gains a `__Host-` prefix whenever
 * the session cookie is Secure), so the suffix is the only stable handle.
 */
const CSRF_COOKIE_SUFFIX: string = "-csrf";

/**
 * A user identity resolved from the BFF `/bff/user` endpoint.
 *
 * `sub` is always present; other standard claims are optional, and arbitrary
 * additional claims are permitted via the index signature.
 */
export interface WallowUser {
  sub: string;
  email?: string;
  name?: string;
  [claim: string]: unknown;
}

/**
 * Fail with an actionable message when a browser-only navigation helper is
 * called outside the browser.
 *
 * Both helpers navigate by assigning to the global `location`, which Node has
 * no equivalent for; a partially-polyfilled SSR runtime can also expose it as
 * `undefined`. `typeof` covers both shapes. Importing this module stays safe
 * either way — the check lives inside the function bodies, never at module
 * scope — so the browser entry can be pulled into an SSR bundle unchanged.
 *
 * @param caller Name of the calling helper, e.g. `login()`, used in the message.
 */
function assertBrowserNavigation(caller: string): void {
  const canNavigate: boolean = typeof location !== "undefined";

  if (!canNavigate) {
    throw new Error(
      `${caller} can only run in the browser: it navigates by assigning to the global \`location\`, which is unavailable during server-side rendering. Call it from a client-side event handler or effect.`,
    );
  }
}

/**
 * Navigate the browser to the BFF login endpoint, preserving where the user
 * should land afterwards.
 *
 * @param returnTo Path to return to after a successful login. Defaults to "/".
 * @throws Error when called outside a browser context.
 */
export function login(returnTo: string = "/"): void {
  assertBrowserNavigation("login()");

  location.href = `/bff/login?returnTo=${encodeURIComponent(returnTo)}`;
}

/**
 * Options for {@link logout}.
 */
export interface LogoutOptions {
  /**
   * CSRF token to echo in the `x-csrf-token` header. Defaults to the token the
   * SDK learned from `/bff/user` (see `setCsrfToken`), then to the BFF's
   * non-HttpOnly double-submit cookie.
   */
  csrfToken?: string;
}

/**
 * End the BFF session and send the browser on to the IdP's end-session URL.
 *
 * `/bff/logout` is a state-changing endpoint: it answers `405` to anything but
 * a `POST` carrying a valid `x-csrf-token`, so this cannot be a plain
 * navigation. The session-clearing request is issued with `fetch`, and the
 * browser is navigated afterwards on the redirect the handler answers with.
 *
 * @param options Optional {@link LogoutOptions}.
 * @throws Error synchronously when called outside a browser context; rejects
 *         when the BFF refuses the logout.
 */
export function logout(options?: LogoutOptions): Promise<void> {
  // Not an `async function`: the SSR guard must throw synchronously (see
  // Wallow-pu6a.3.6), and `async` would turn that throw into a rejection.
  assertBrowserNavigation("logout()");

  return endSession(options);
}

/**
 * Read the double-submit CSRF cookie the BFF wrote next to the session.
 *
 * Matched by the `-csrf` suffix rather than a full name, since the browser does
 * not know the configured cookie name or whether it carries a `__Host-` prefix.
 *
 * @returns The token, or `null` when there is no readable cookie (including
 *          runtimes with no `document` at all).
 */
function readCsrfCookie(): string | null {
  const hasDocument: boolean = typeof document !== "undefined";

  if (!hasDocument) {
    return null;
  }

  for (const entry of (document.cookie ?? "").split(";")) {
    // Re-joined on "=", since a cookie value may itself contain padding.
    const [name = "", ...value] = entry.split("=");

    if (name.trim().endsWith(CSRF_COOKIE_SUFFIX)) {
      return decodeURIComponent(value.join("=").trim());
    }
  }

  return null;
}

/**
 * Resolve the CSRF token to present, most specific source first: an explicit
 * option, then the token the SDK learned from `/bff/user`, then the readable
 * double-submit cookie.
 *
 * @returns The token, or `null` when the browser holds none — in which case no
 *          header is sent at all and the BFF stays the authority, answering 403.
 */
function resolveCsrfToken(options?: LogoutOptions): string | null {
  return options?.csrfToken ?? getCsrfToken() ?? readCsrfCookie();
}

/**
 * Issue the gated `POST /bff/logout` and navigate on whatever the handler
 * answers with.
 *
 * @throws Error when the BFF refuses the logout, leaving the browser where it is.
 */
async function endSession(options?: LogoutOptions): Promise<void> {
  const token: string | null = resolveCsrfToken(options);
  const headers: Record<string, string> = {};

  if (token !== null) {
    headers[CSRF_HEADER] = token;
  }

  const response: Response = await fetch("/bff/logout", {
    method: "POST",
    credentials: "include",
    // Letting fetch follow the 302 itself would put the IdP's end-session
    // endpoint behind CORS and fail the logout with an opaque TypeError. The
    // browser must make that hop as a navigation instead.
    redirect: "manual",
    headers,
  });

  if (response.status >= HTTP_FIRST_ERROR) {
    throw new Error(
      `Logout failed: the BFF answered ${response.status}. The session may still be active.`,
    );
  }

  // A cross-origin `redirect: "manual"` response is opaque in a real browser —
  // status 0 and no readable headers — even though its `Set-Cookie` headers
  // were applied and the session IS cleared. Only the target is invisible, so
  // fall back to the app root.
  location.href = response.headers.get("location") ?? "/";
}

/**
 * Options for {@link getUser}.
 */
export interface GetUserOptions {
  /**
   * Absolute origin (e.g. `http://localhost:3000`) to resolve the `/bff/user`
   * request against. Required during SSR, where the global (Node/undici) `fetch`
   * cannot parse a relative URL and throws `Failed to parse URL from /bff/user`.
   * Omit it in the browser to keep the same-origin relative request.
   */
  baseUrl?: string;
  /**
   * Extra request headers to attach. Used during SSR to forward the incoming
   * session `Cookie` header, since the Node `fetch` has no cookie jar and
   * `credentials: "include"` alone would send an anonymous request. Omit it in
   * the browser, where the cookie rides along automatically.
   */
  headers?: Record<string, string>;
}

/**
 * Fetch the current user from the BFF `/bff/user` endpoint.
 *
 * @param options Optional {@link GetUserOptions}; pass `baseUrl` during SSR so the
 *                request target is an absolute URL the Node fetch can resolve.
 * @returns The parsed user on 200, or `null` when unauthenticated (401).
 *          Throws on any other non-ok response.
 */
export async function getUser(options?: GetUserOptions): Promise<WallowUser | null> {
  const target: string = options?.baseUrl ? `${options.baseUrl}/bff/user` : "/bff/user";

  const init: RequestInit = { credentials: "include" };
  if (options?.headers) {
    init.headers = options.headers;
  }

  const response: Response = await fetch(target, init);

  if (response.status === HTTP_UNAUTHORIZED) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to fetch user: ${response.status}`);
  }

  return (await response.json()) as WallowUser;
}
