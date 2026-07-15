/**
 * CSRF interceptor module.
 *
 * The BFF rejects any state-changing request (POST/PUT/PATCH/DELETE) that does
 * not echo the session's CSRF token in the `x-csrf-token` header. This module
 * owns the in-memory token store and the request interceptor that stamps the
 * header, so both `src/app.ts` and `getWallowSdk()` can wire it onto the shared
 * `@hey-api` client without hand-rolling the logic.
 */

/** HTTP methods the BFF does not gate on CSRF, per RFC 9110 safe methods. */
const safeMethods: ReadonlySet<string> = new Set(["GET", "HEAD", "OPTIONS"]);

/** True when the method is CSRF-exempt (safe), matched case-insensitively. */
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
 * state-changing requests. Called with the token from `/bff/user` after login
 * and with `null` on logout / anonymous state.
 */
export function setCsrfToken(token: string | null): void {
  csrfToken = token;
}

/**
 * Register the CSRF request interceptor on the given client.
 *
 * The interceptor stamps the current CSRF token into the `x-csrf-token` header
 * on state-changing requests and leaves safe methods (and anonymous requests,
 * where the token is `null`) untouched. Without it the proxy answers 403
 * `CSRF_INVALID` and the request never reaches the API. The request instance is
 * returned unchanged so it can be chained with other interceptors.
 */
export function wireCsrfInterceptor(client: CsrfInterceptorClient): void {
  client.interceptors.request.use((request: Request): Request => {
    if (csrfToken !== null && !isSafeMethod(request.method)) {
      request.headers.set("x-csrf-token", csrfToken);
    }
    return request;
  });
}
