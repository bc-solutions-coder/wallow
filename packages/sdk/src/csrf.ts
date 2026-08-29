/**
 * CSRF interceptor module.
 *
 * The BFF rejects any state-changing request (POST/PUT/PATCH/DELETE) that does
 * not echo the session's CSRF token in the `x-csrf-token` header. This module
 * owns the cookie reader and the request interceptor that stamps the header, so
 * app-level wiring can reuse it against the generated `@hey-api` client without
 * hand-rolling the logic.
 *
 * The ONLY token source is the BFF's non-HttpOnly double-submit cookie. There
 * is deliberately no module-scope token store (Wallow-j7qk): the SDK's doctrine
 * is that nothing request-scoped lives at module scope (`create-sdk.ts`), and a
 * process-global token shared by concurrent SSR renders is exactly the
 * cross-user leak that doctrine exists to prevent. The cookie jar is already
 * per-tab, per-user state, and the BFF rewrites the cookie on every login, so
 * it is always the live synchronizer token — a copy held in a variable could
 * only ever be equal or stale.
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
 * Register the CSRF request interceptor on the given client. The interceptor
 * stamps the live double-submit cookie into `x-csrf-token` on state-changing
 * requests, leaves safe methods (and the anonymous, cookie-less state)
 * untouched, and returns the request instance unchanged so it chains with other
 * interceptors.
 *
 * The cookie is the interceptor's only source: it is rewritten by the BFF on
 * every login and unreadable outside the browser, so the interceptor is
 * automatically live after a re-login and automatically inert during SSR —
 * `readCsrfCookie()` returns `null` where `document` does not exist.
 */
export function wireCsrfInterceptor(client: CsrfInterceptorClient): void {
  client.interceptors.request.use((request: Request): Request => {
    const token: string | null = readCsrfCookie();

    if (token !== null && !isSafeMethod(request.method)) {
      request.headers.set("x-csrf-token", token);
    }
    return request;
  });
}
