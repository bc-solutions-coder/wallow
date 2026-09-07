/** Read the browser BFF CSRF cookie and echo it on state-changing API requests. */

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

/** Return whether the method is GET, HEAD, or OPTIONS, ignoring case. */
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
 * Add a request interceptor that echoes the CSRF cookie in `x-csrf-token`.
 *
 * Applies to methods other than GET, HEAD, and OPTIONS when a token is readable
 * from `document.cookie`. It does nothing during SSR. `createWallowSdk`
 * installs this by default; call it once when configuring a client manually.
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
