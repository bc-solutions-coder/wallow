import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  type CsrfInterceptorClient,
  isSafeMethod,
  setCsrfToken,
  wireCsrfInterceptor,
} from "./csrf";

/**
 * CSRF interceptor module (Wallow-8w1h.3.2). This file is the relocated home of
 * the CSRF helper (moved from `src/csrf.ts`) and adds `wireCsrfInterceptor`,
 * which extracts the request interceptor previously hand-wired in `src/app.ts`
 * so `getWallowSdk()` (task 2.3) can reuse it. The interceptor echoes the
 * session CSRF token in the `x-csrf-token` header on state-changing requests and
 * leaves safe methods (GET/HEAD/OPTIONS) untouched.
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
  // Reset module-scope token state so tests do not leak into one another.
  setCsrfToken(null);
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
  it("keeps exporting isSafeMethod for src/app.ts and the SDK facade", () => {
    // src/app.ts imports isSafeMethod from the csrf module; the relocation must
    // preserve that named export so its import continues to resolve.
    expect(vi.isMockFunction(isSafeMethod)).toBe(false);
    expect(typeof isSafeMethod).toBe("function");
  });
});
