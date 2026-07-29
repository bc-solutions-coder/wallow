import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  CSRF_COOKIE_SUFFIX,
  type CsrfInterceptorClient,
  getCsrfToken,
  isSafeMethod,
  readCsrfCookie,
  setCsrfToken,
  wireCsrfInterceptor,
} from "./csrf";

/**
 * CSRF interceptor module (Wallow-0q2s.7.1). This is the SDK-owned home of the
 * CSRF helper, consolidated from the byte-near-identical
 * `apps/wallow-auth/src/lib/csrf.test.ts` and
 * `apps/wallow-web/src/lib/csrf.test.ts` copies. The interceptor echoes the
 * session CSRF token in the `x-csrf-token` header on state-changing requests and
 * leaves safe methods (GET/HEAD/OPTIONS) untouched. Both apps' facades
 * (`getWallowSdk()` / `getWallowAuthSdk()`) wire it onto the shared `@hey-api`
 * client.
 */

/**
 * Minimal fake of the generated `@hey-api` client interceptor surface. Captures
 * the interceptor `wireCsrfInterceptor` registers so a test can run a request
 * through it and inspect the resulting headers.
 */
function createFakeClient(): CsrfInterceptorClient & {
  run: (request: Request) => Request;
  useCount: () => number;
} {
  let registered: ((request: Request) => Request) | null = null;
  let uses = 0;
  return {
    interceptors: {
      request: {
        use(interceptor: (request: Request) => Request): void {
          registered = interceptor;
          uses += 1;
        },
      },
    },
    run(request: Request): Request {
      if (registered === null) {
        throw new Error("no request interceptor was registered");
      }
      return registered(request);
    },
    useCount(): number {
      return uses;
    },
  };
}

/**
 * Stub a browser cookie jar. The node vitest project has no `document` at all,
 * which is exactly the SSR shape the interceptor must stay inert under, so every
 * browser-path test opts in explicitly.
 */
function stubCookieJar(cookie: string): void {
  vi.stubGlobal("document", { cookie });
}

beforeEach(() => {
  // Reset module-scope token state so tests do not leak into one another. The
  // stub throws in the red phase, so swallow it: the assertions below are what
  // must drive the implementation, not this teardown.
  try {
    setCsrfToken(null);
  } catch {
    /* red phase: setCsrfToken is not implemented yet */
  }
});

afterEach(() => {
  // Drop any stubbed `document` so the next test starts from the bare node
  // global again — otherwise a leaked jar would mask the SSR-isolation case.
  vi.unstubAllGlobals();
});

describe("isSafeMethod", () => {
  it("treats RFC 9110 safe methods as safe, case-insensitively", () => {
    expect(isSafeMethod("GET")).toBe(true);
    expect(isSafeMethod("head")).toBe(true);
    expect(isSafeMethod("Options")).toBe(true);
  });

  it("treats state-changing methods as unsafe", () => {
    expect(isSafeMethod("POST")).toBe(false);
    expect(isSafeMethod("PUT")).toBe(false);
    expect(isSafeMethod("PATCH")).toBe(false);
    expect(isSafeMethod("DELETE")).toBe(false);
  });
});

describe("wireCsrfInterceptor", () => {
  // These cases all exercise the browser path, where the module token store is
  // per-tab and safe to read. An empty jar keeps the double-submit cookie out of
  // the picture so each case still isolates the store it is about.
  beforeEach(() => {
    stubCookieJar("");
  });

  it("registers exactly one request interceptor on the client", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    expect(client.useCount()).toBe(1);
  });

  it("attaches the CSRF token header on state-changing requests once a token is set", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("tok-123");

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });
    const result = client.run(request);

    expect(result.headers.get("x-csrf-token")).toBe("tok-123");
  });

  it("does not attach the header on safe methods even when a token is set", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("tok-123");

    const request = new Request("https://example.test/api/v1/identity/users/me", {
      method: "GET",
    });
    const result = client.run(request);

    expect(result.headers.get("x-csrf-token")).toBeNull();
  });

  it("does not attach the header on state-changing requests while no token is set", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    // No setCsrfToken call; the token remains null (anonymous / pre-login).

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });
    const result = client.run(request);

    expect(result.headers.get("x-csrf-token")).toBeNull();
  });

  it("clears the token so later mutations stop carrying it after logout", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("tok-123");
    setCsrfToken(null);

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "DELETE",
    });
    const result = client.run(request);

    expect(result.headers.get("x-csrf-token")).toBeNull();
  });

  it("reads the token live, so a token set after wiring still applies", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("tok-late");

    const request = new Request("https://example.test/api/v1/identity/auth/mfa/verify", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("tok-late");
  });

  it("returns the same request instance it was given", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("tok-123");

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });
    expect(client.run(request)).toBe(request);
  });
});

/**
 * Double-submit cookie fallback (Wallow-vufu.1.1).
 *
 * `setCsrfToken()` is called from exactly one route in the whole workspace, so
 * every other mutation reached the BFF with no `x-csrf-token` header and came
 * back `403 CSRF_INVALID`. The interceptor therefore cannot depend on that call
 * having happened: it falls back to the non-HttpOnly `${cookieName}-csrf` cookie
 * the BFF writes at the OIDC callback, which is rewritten on every login and so
 * is always the live synchronizer token — including after a re-login that would
 * leave the module store stale.
 */
describe("wireCsrfInterceptor CSRF token resolution", () => {
  it("falls back to the double-submit cookie when no token was set", () => {
    stubCookieJar("wallow_bff-csrf=cookie-token");
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    // No setCsrfToken call: this is every dashboard mutation's real state.

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("cookie-token");
  });

  it("ignores unrelated cookies in the jar when falling back", () => {
    stubCookieJar("theme=dark; __Host-wallow_bff=sealed-session; wallow_bff-csrf=cookie-token");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("cookie-token");
  });

  it("prefers an explicitly set token over the cookie", () => {
    stubCookieJar("wallow_bff-csrf=cookie-token");
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("explicit-token");

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("explicit-token");
  });

  it("prefers a __Host--prefixed cookie over a bare-named one when both are present", () => {
    // A localhost jar accumulates both across runs: the Aspire (plain HTTP)
    // stack writes the bare name, the compose stack writes the `__Host-` one.
    // First-match-wins parsing would hand back whichever the browser lists
    // first, which is the stale one here.
    stubCookieJar("wallow_bff-csrf=stale-token; __Host-wallow_bff-csrf=host-token");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("host-token");
  });

  it("sends no header on a safe method even when a cookie is readable", () => {
    stubCookieJar("wallow_bff-csrf=cookie-token");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/users/me", {
      method: "GET",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBeNull();
  });

  it("sends no header when neither a token nor a cookie exists", () => {
    stubCookieJar("theme=dark");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBeNull();
  });

  it("ignores the module token store outside the browser", () => {
    // The store is module scope, which during SSR is process-global and shared
    // by every concurrently rendered request — one user's token must never be
    // stamped onto another's. With no `document` there is no browser to trust.
    vi.stubGlobal("document", undefined);
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("other-users-token");

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBeNull();
  });
});

/**
 * The cookie reader itself, moved here from `auth.ts` so the interceptor and
 * `logout()` resolve the token through one implementation instead of two.
 */
describe("readCsrfCookie", () => {
  it("exposes the suffix the BFF's cookie name ends with", () => {
    expect(CSRF_COOKIE_SUFFIX).toBe("-csrf");
  });

  it("returns null outside the browser, where there is no cookie jar", () => {
    vi.stubGlobal("document", undefined);

    expect(readCsrfCookie()).toBeNull();
  });

  it("returns null when the jar holds no CSRF cookie", () => {
    stubCookieJar("theme=dark; __Host-wallow_bff=sealed-session");

    expect(readCsrfCookie()).toBeNull();
  });

  it("returns the value of the suffixed cookie", () => {
    stubCookieJar("theme=dark; wallow_bff-csrf=cookie-token");

    expect(readCsrfCookie()).toBe("cookie-token");
  });

  it("percent-decodes the cookie value", () => {
    // The token is base64url in practice, but the BFF writes it encoded, and a
    // value carrying `+` or `=` padding must survive the round trip intact.
    stubCookieJar("wallow_bff-csrf=tok%2Ba%3D%3D");

    expect(readCsrfCookie()).toBe("tok+a==");
  });

  it("prefers the __Host--prefixed cookie when the jar holds both", () => {
    stubCookieJar("wallow_bff-csrf=stale-token; __Host-wallow_bff-csrf=host-token");

    expect(readCsrfCookie()).toBe("host-token");
  });
});

/**
 * The token is also read outside the interceptor chain: `logout()` POSTs to
 * `/bff/logout` itself and must stamp the same `x-csrf-token` header
 * (Wallow-pu6a.3.9), so the module token needs a reader as well as a writer.
 */
describe("getCsrfToken", () => {
  it("returns null before any token is set", () => {
    expect(getCsrfToken()).toBeNull();
  });

  it("returns the token most recently set, and null again once cleared", () => {
    setCsrfToken("tok-123");
    expect(getCsrfToken()).toBe("tok-123");

    setCsrfToken(null);
    expect(getCsrfToken()).toBeNull();
  });
});

describe("existing callers still resolve", () => {
  it("keeps exporting isSafeMethod for both app facades", () => {
    // The app facades import isSafeMethod/setCsrfToken/wireCsrfInterceptor from
    // the SDK's browser entry; the relocation must preserve these named value
    // exports so those imports continue to resolve.
    expect(vi.isMockFunction(isSafeMethod)).toBe(false);
    expect(typeof isSafeMethod).toBe("function");
    expect(typeof setCsrfToken).toBe("function");
    expect(typeof wireCsrfInterceptor).toBe("function");
  });
});
