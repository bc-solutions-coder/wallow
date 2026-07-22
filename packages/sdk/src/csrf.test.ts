import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  type CsrfInterceptorClient,
  isSafeMethod,
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
