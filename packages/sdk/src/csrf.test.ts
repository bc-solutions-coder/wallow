import { afterEach, describe, expect, it, vi } from "vitest";

import {
  CSRF_COOKIE_SUFFIX,
  type CsrfInterceptorClient,
  isSafeMethod,
  readCsrfCookie,
  wireCsrfInterceptor,
} from "./csrf";

/**
 * CSRF interceptor module (Wallow-0q2s.7.1). This is the SDK-owned home of the
 * CSRF helper, consolidated from the byte-near-identical
 * `apps/wallow-auth/src/lib/csrf.test.ts` and
 * `apps/wallow-web/src/lib/csrf.test.ts` copies. The interceptor echoes the
 * session CSRF token in the `x-csrf-token` header on state-changing requests and
 * leaves safe methods (GET/HEAD/OPTIONS) untouched.
 *
 * The ONE token source is the BFF's non-HttpOnly double-submit cookie
 * (Wallow-j7qk). The module-scope token store that used to sit in front of it
 * (`setCsrfToken`/`getCsrfToken`) is deleted: at module scope the token was
 * process-global during SSR — the exact cross-user hazard `create-sdk.ts`
 * exists to prevent — and in the browser it could only ever equal or trail the
 * cookie, which the BFF rewrites on every login.
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
 * browser-path test opts in explicitly. The returned handle mutates the jar in
 * place, the way a login/logout response's `Set-Cookie` does.
 */
function stubCookieJar(cookie: string): { set: (next: string) => void } {
  const jar: { cookie: string } = { cookie };
  vi.stubGlobal("document", jar);
  return {
    set(next: string): void {
      jar.cookie = next;
    },
  };
}

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
  it("registers exactly one request interceptor on the client", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    expect(client.useCount()).toBe(1);
  });

  it("stamps the double-submit cookie's token on state-changing requests", () => {
    stubCookieJar("wallow_bff-csrf=cookie-token");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("cookie-token");
  });

  it("does not attach the header on safe methods even when a cookie is readable", () => {
    stubCookieJar("wallow_bff-csrf=cookie-token");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/users/me", {
      method: "GET",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBeNull();
  });

  it("does not attach the header while the jar holds no CSRF cookie (anonymous)", () => {
    stubCookieJar("theme=dark");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBeNull();
  });

  it("ignores unrelated cookies in the jar", () => {
    stubCookieJar("theme=dark; __Host-wallow_bff=sealed-session; wallow_bff-csrf=cookie-token");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("cookie-token");
  });

  it("reads the jar live, so a login after wiring still applies", () => {
    // The BFF writes the cookie at the OIDC callback, long after the app wired
    // its client — the interceptor must read at request time, not wire time.
    const jar = stubCookieJar("");
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    jar.set("wallow_bff-csrf=tok-late");

    const request = new Request("https://example.test/api/v1/identity/auth/mfa/verify", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("tok-late");
  });

  it("stops carrying the token once logout clears the cookie", () => {
    const jar = stubCookieJar("wallow_bff-csrf=tok-123");
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    // The logout handler answers with a Max-Age=0 `Set-Cookie`; from the jar's
    // point of view the cookie is simply gone.
    jar.set("");

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "DELETE",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBeNull();
  });

  it("picks up the rewritten cookie after a re-login", () => {
    // Every login mints a fresh token and overwrites the cookie. A cached copy
    // would present the STALE token here and 403 every mutation — the class of
    // bug the deleted module store invited.
    const jar = stubCookieJar("wallow_bff-csrf=first-session");
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    jar.set("wallow_bff-csrf=second-session");

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("second-session");
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

  it("returns the same request instance it was given", () => {
    stubCookieJar("wallow_bff-csrf=tok-123");
    const client = createFakeClient();
    wireCsrfInterceptor(client);

    const request = new Request("https://example.test/api/v1/identity/organizations", {
      method: "POST",
    });
    expect(client.run(request)).toBe(request);
  });

  it("is inert outside the browser, where no cookie jar exists", () => {
    // During SSR there is no per-user jar to read — anything else the
    // interceptor could reach at module scope would be process-global and
    // shared by every concurrently rendered request.
    vi.stubGlobal("document", undefined);
    const client = createFakeClient();
    wireCsrfInterceptor(client);

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

describe("existing callers still resolve", () => {
  it("keeps exporting the helper trio as named values", () => {
    // `logout()` and the apps' logger wiring import
    // isSafeMethod/readCsrfCookie/wireCsrfInterceptor from the SDK's browser
    // entry; these named value exports are the contract.
    expect(vi.isMockFunction(isSafeMethod)).toBe(false);
    expect(typeof isSafeMethod).toBe("function");
    expect(typeof readCsrfCookie).toBe("function");
    expect(typeof wireCsrfInterceptor).toBe("function");
  });
});
