/**
 * CSRF interceptor module for wallow-auth (Wallow-vec7.2.3).
 *
 * A 1:1 mirror of `apps/wallow-web/src/lib/csrf.ts`. The API rejects any
 * state-changing request (POST/PUT/PATCH/DELETE) that does not echo the
 * session's CSRF token in the `x-csrf-token` header, so this module owns the
 * in-memory token store and the request interceptor that stamps the header.
 * `getWallowAuthSdk()` wires it onto the shared `@hey-api` client on first use.
 *
 * Per bd memory `csrf-delivery-pattern-synchronizer-token-in-session-csrftoke`:
 * synchronizer token lives in the session, a non-HttpOnly double-submit cookie
 * carries a copy to the browser, and the `x-csrf-token` header echoes it back.
 */

/** HTTP methods the API does not gate on CSRF, per RFC 9110 safe methods. */
const safeMethods: ReadonlySet<string> = new Set(["GET", "HEAD", "OPTIONS"]);

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

/** True when the method is CSRF-exempt (safe per RFC 9110), case-insensitively. */
export function isSafeMethod(method: string): boolean {
  return safeMethods.has(method.toUpperCase());
}

/**
 * The session's CSRF token.
 *
 * Holding it in module scope keeps it out of the DOM and lets the interceptor
 * read it live, so a token set after wiring still applies. wallow-auth spends
 * most of its life pre-login, where this stays `null` and mutating requests go
 * out unstamped.
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
 * Register the CSRF request interceptor on the given client. The interceptor
 * stamps the current token into `x-csrf-token` on state-changing requests,
 * leaves safe methods (and the anonymous, token-less state) untouched, and
 * returns the request instance unchanged so it chains with other interceptors.
 */
export function wireCsrfInterceptor(client: CsrfInterceptorClient): void {
  client.interceptors.request.use((request: Request): Request => {
    if (csrfToken !== null && !isSafeMethod(request.method)) {
      request.headers.set("x-csrf-token", csrfToken);
    }
    return request;
  });
}
