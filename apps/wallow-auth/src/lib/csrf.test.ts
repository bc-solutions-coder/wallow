import { beforeEach, describe, expect, it } from "vitest";

import {
  type CsrfInterceptorClient,
  isSafeMethod,
  setCsrfToken,
  wireCsrfInterceptor,
} from "./csrf";

/**
 * CSRF interceptor module for wallow-auth (Wallow-vec7.2.3), a 1:1 mirror of
 * `apps/wallow-web/src/lib/csrf.test.ts`. The interceptor echoes the session
 * CSRF token in the `x-csrf-token` header on state-changing requests and leaves
 * safe methods (GET/HEAD/OPTIONS) untouched. `getWallowAuthSdk()` wires it onto
 * the shared `@hey-api` client.
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

    const request = new Request("https://example.test/v1/identity/auth/login", {
      method: "POST",
    });
    const result = client.run(request);

    expect(result.headers.get("x-csrf-token")).toBe("tok-123");
  });

  it("does not attach the header on safe methods even when a token is set", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("tok-123");

    const request = new Request("https://example.test/v1/identity/auth/external-providers", {
      method: "GET",
    });
    const result = client.run(request);

    expect(result.headers.get("x-csrf-token")).toBeNull();
  });

  it("does not attach the header on state-changing requests while no token is set", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    // No setCsrfToken call; the token remains null (anonymous / pre-login) —
    // the common case for wallow-auth, whose whole job is the pre-login flow.

    const request = new Request("https://example.test/v1/identity/auth/login", {
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

    const request = new Request("https://example.test/v1/identity/auth/login", {
      method: "DELETE",
    });
    const result = client.run(request);

    expect(result.headers.get("x-csrf-token")).toBeNull();
  });

  it("reads the token live, so a token set after wiring still applies", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("tok-late");

    const request = new Request("https://example.test/v1/identity/auth/mfa/verify", {
      method: "POST",
    });

    expect(client.run(request).headers.get("x-csrf-token")).toBe("tok-late");
  });

  it("returns the same request instance it was given", () => {
    const client = createFakeClient();
    wireCsrfInterceptor(client);
    setCsrfToken("tok-123");

    const request = new Request("https://example.test/v1/identity/auth/login", {
      method: "POST",
    });
    expect(client.run(request)).toBe(request);
  });
});
