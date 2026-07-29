/**
 * CSRF interceptor module.
 *
 * The BFF rejects any state-changing request (POST/PUT/PATCH/DELETE) that does
 * not echo the session's CSRF token in the `x-csrf-token` header. This module
 * owns the in-memory token store and the request interceptor that stamps the
 * header, so app-level wiring can reuse it against the shared `@hey-api` client
 * without hand-rolling the logic.
 */

/** HTTP methods the BFF does not gate on CSRF, per RFC 9110 safe methods. */
const safeMethods: ReadonlySet<string> = new Set(["GET", "HEAD", "OPTIONS"]);

/**
 * Suffix of the non-HttpOnly double-submit cookie the BFF writes alongside the
 * session. Its full name is `${cookieName}-csrf`, and `cookieName` is a server
 * setting the browser never learns (it also gains a `__Host-` prefix whenever
 * the session cookie is Secure), so the suffix is the only stable handle.
 */
export const CSRF_COOKIE_SUFFIX: string = "-csrf";

/**
 * Cookie-name prefix the browser only honours for cookies set by this exact
 * origin over HTTPS with no `Domain` attribute. A `__Host-` match therefore
 * outranks a bare-named one when a jar holds both.
 */
const HOST_COOKIE_PREFIX: string = "__Host-";

/**
 * Read the double-submit CSRF cookie the BFF wrote next to the session.
 *
 * Matched by the `-csrf` suffix rather than a full name, since the browser does
 * not know the configured cookie name or whether it carries a `__Host-` prefix.
 * When the jar holds more than one match — a stale bare-named cookie left over
 * from a plain-HTTP run next to the `__Host-` one a Secure session writes — the
 * `__Host-`-prefixed cookie wins, because only that one is guaranteed to have
 * been set by this exact origin over HTTPS.
 *
 * @returns The token, or `null` when there is no readable cookie (including
 *          runtimes with no `document` at all).
 */
export function readCsrfCookie(): string | null {
  const hasDocument: boolean = typeof document !== "undefined";

  if (!hasDocument) {
    return null;
  }

  let fallback: string | null = null;

  for (const entry of (document.cookie ?? "").split(";")) {
    // Re-joined on "=", since a cookie value may itself contain padding.
    const [rawName = "", ...value] = entry.split("=");
    const name: string = rawName.trim();

    if (name.endsWith(CSRF_COOKIE_SUFFIX)) {
      const token: string = decodeURIComponent(value.join("=").trim());

      if (name.startsWith(HOST_COOKIE_PREFIX)) {
        return token;
      }

      fallback ??= token;
    }
  }

  return fallback;
}

/**
 * Resolve the token the interceptor should stamp, most specific source first.
 *
 * Returns `null` outside the browser: the module store is process-global there,
 * shared by every concurrently rendered request, so one user's token could be
 * stamped onto another's. Only a real cookie jar makes the store per-tab and
 * safe to trust.
 */
function resolveToken(): string | null {
  const inBrowser: boolean = typeof document !== "undefined";

  if (!inBrowser) {
    return null;
  }

  return csrfToken ?? readCsrfCookie();
}

/** True when the method is CSRF-exempt (safe per RFC 9110), case-insensitively. */
export function isSafeMethod(method: string): boolean {
  return safeMethods.has(method.toUpperCase());
}

/**
 * The subset of the generated `@hey-api` client this module wires an interceptor
 * onto. Kept structural so the real SDK `client` is assignable without importing
 * its concrete type here.
 */
export interface CsrfInterceptorClient {
  interceptors: {
    request: {
      use: (interceptor: (request: Request) => Request) => void;
    };
  };
}

/**
 * The session's CSRF token, learned from `/bff/user`.
 *
 * The BFF mints it at login, seals it inside the session, and hands the browser
 * a copy in the `/bff/user` body. Holding it in module scope keeps it out of the
 * DOM and lets the interceptor read it live, so a token set after wiring still
 * applies.
 */
let csrfToken: string | null = null;

/**
 * Set (or clear, with `null`) the CSRF token the interceptor echoes on
 * state-changing requests.
 */
export function setCsrfToken(token: string | null): void {
  csrfToken = token;
}

/**
 * The CSRF token currently held in module scope, or `null` when none is set.
 *
 * Read by other browser helpers that must stamp `x-csrf-token` on a
 * state-changing request they issue outside the generated client's interceptor
 * chain — `logout()` in particular, which POSTs to `/bff/logout` directly.
 */
export function getCsrfToken(): string | null {
  return csrfToken;
}

/**
 * Register the CSRF request interceptor on the given client. The interceptor
 * stamps the current token into `x-csrf-token` on state-changing requests,
 * leaves safe methods (and the anonymous, token-less state) untouched, and
 * returns the request instance unchanged so it chains with other interceptors.
 *
 * The token comes from the module store when one was set, and otherwise from
 * the BFF's double-submit cookie — `setCsrfToken()` is not on every app's login
 * path, so the interceptor cannot depend on it having been called.
 */
export function wireCsrfInterceptor(client: CsrfInterceptorClient): void {
  client.interceptors.request.use((request: Request): Request => {
    const token: string | null = resolveToken();

    if (token !== null && !isSafeMethod(request.method)) {
      request.headers.set("x-csrf-token", token);
    }
    return request;
  });
}
