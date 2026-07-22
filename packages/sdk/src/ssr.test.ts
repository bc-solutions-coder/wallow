import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { client } from "./client";
import type { CsrfInterceptorClient } from "./csrf";
import { getV1IdentityUsersMe } from "./generated/sdk.gen";
import {
  configureSsrClient,
  getSsrRequestContext,
  setSsrRequestContextResolver,
  wireSsrCookieInterceptor,
  type SsrRequestContext,
} from "./ssr";

/**
 * SSR request-context seam + SSR-time BFF client configuration (Wallow-0q2s.7.2).
 *
 * Relocated from `apps/wallow-web/src/lib/ssr-request-context.ts` (the browser-safe
 * resolver seam) and the SSR wiring previously inlined in
 * `apps/wallow-web/src/lib/wallow-sdk.ts` (`wireSsrCookieInterceptor` + the
 * SSR-vs-browser `ensureConfigured` branch). These symbols ship on the SDK's
 * BROWSER entry (`.`) because they carry no node import — the app's `ssr.tsx`
 * keeps the `node:async_hooks` `AsyncLocalStorage` and registers a resolver via
 * {@link setSsrRequestContextResolver}; the isomorphic facade reads the context
 * during SSR and forwards the session cookie through a live interceptor.
 */

const ORIGIN = "http://localhost:3000";

/**
 * Minimal fake of the generated `@hey-api` client interceptor surface. Captures
 * the interceptor `wireSsrCookieInterceptor` registers so a test can run a request
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

describe("setSsrRequestContextResolver / getSsrRequestContext (browser-safe seam)", () => {
  it("returns the resolved context once a resolver is registered", () => {
    const context: SsrRequestContext = { origin: ORIGIN, cookie: "wallow_bff=abc" };
    setSsrRequestContextResolver(() => context);

    expect(getSsrRequestContext()).toBe(context);
  });

  it("returns undefined when the resolver yields no active request context", () => {
    setSsrRequestContextResolver(() => undefined);

    expect(getSsrRequestContext()).toBeUndefined();
  });

  it("reads the resolver live, reflecting the in-flight request on each call", () => {
    let current: SsrRequestContext | undefined;
    setSsrRequestContextResolver(() => current);

    expect(getSsrRequestContext()).toBeUndefined();

    current = { origin: ORIGIN, cookie: "wallow_bff=one" };
    expect(getSsrRequestContext()).toBe(current);

    current = { origin: ORIGIN, cookie: "wallow_bff=two" };
    expect(getSsrRequestContext()?.cookie).toBe("wallow_bff=two");
  });

  it("lets a later resolver registration supersede an earlier one", () => {
    setSsrRequestContextResolver(() => ({ origin: "http://a.test", cookie: "x" }));
    setSsrRequestContextResolver(() => ({ origin: "http://b.test", cookie: "y" }));

    expect(getSsrRequestContext()).toEqual({ origin: "http://b.test", cookie: "y" });
  });
});

describe("wireSsrCookieInterceptor", () => {
  it("registers exactly one request interceptor on the client", () => {
    const fake = createFakeClient();
    wireSsrCookieInterceptor(fake);

    expect(fake.useCount()).toBe(1);
  });

  it("forwards the in-flight session cookie onto the outgoing request", () => {
    setSsrRequestContextResolver(() => ({ origin: ORIGIN, cookie: "wallow_bff=sess" }));
    const fake = createFakeClient();
    wireSsrCookieInterceptor(fake);

    const result = fake.run(new Request(`${ORIGIN}/api/v1/identity/apps`));

    expect(result.headers.get("cookie")).toBe("wallow_bff=sess");
  });

  it("does not set a cookie header when the context carries no cookie", () => {
    setSsrRequestContextResolver(() => ({ origin: ORIGIN, cookie: undefined }));
    const fake = createFakeClient();
    wireSsrCookieInterceptor(fake);

    const result = fake.run(new Request(`${ORIGIN}/api/v1/identity/apps`));

    expect(result.headers.get("cookie")).toBeNull();
  });

  it("does not set a cookie header when there is no active request context", () => {
    setSsrRequestContextResolver(() => undefined);
    const fake = createFakeClient();
    wireSsrCookieInterceptor(fake);

    const result = fake.run(new Request(`${ORIGIN}/api/v1/identity/apps`));

    expect(result.headers.get("cookie")).toBeNull();
  });

  it("reads the cookie live, so each request carries the then-current session", () => {
    let current: string | undefined = "wallow_bff=first";
    setSsrRequestContextResolver(() => ({ origin: ORIGIN, cookie: current }));
    const fake = createFakeClient();
    wireSsrCookieInterceptor(fake);

    const first = fake.run(new Request(`${ORIGIN}/api/v1/identity/apps`));
    current = "wallow_bff=second";
    const second = fake.run(new Request(`${ORIGIN}/api/v1/identity/apps`));

    expect(first.headers.get("cookie")).toBe("wallow_bff=first");
    expect(second.headers.get("cookie")).toBe("wallow_bff=second");
  });

  it("returns the same request instance it was given", () => {
    setSsrRequestContextResolver(() => ({ origin: ORIGIN, cookie: "wallow_bff=sess" }));
    const fake = createFakeClient();
    wireSsrCookieInterceptor(fake);

    const request = new Request(`${ORIGIN}/api/v1/identity/apps`);

    expect(fake.run(request)).toBe(request);
  });
});

describe("configureSsrClient", () => {
  beforeEach(() => {
    client.setConfig({ baseUrl: undefined, credentials: undefined });
  });

  it("points the shared client at the request origin's /api path with credentials included", () => {
    configureSsrClient({ origin: ORIGIN, cookie: undefined });

    const config = client.getConfig();

    expect(config.baseUrl).toBe(`${ORIGIN}/api`);
    expect(config.credentials).toBe("include");
  });

  it("defaults to the same-origin /api path when no request context is supplied", () => {
    configureSsrClient();

    expect(client.getConfig().baseUrl).toBe("/api");
  });

  describe("cookie forwarding through the shared client", () => {
    const fetchMock = vi.fn(
      async (_request: Request) =>
        new Response("{}", {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
    );

    beforeEach(() => {
      fetchMock.mockClear();
      vi.stubGlobal("fetch", fetchMock);
      client.setConfig({ baseUrl: undefined, credentials: undefined });
    });

    afterEach(() => {
      vi.unstubAllGlobals();
    });

    it("forwards the live session cookie on SSR-time BFF requests", async () => {
      let current = "wallow_bff=first";
      setSsrRequestContextResolver(() => ({ origin: ORIGIN, cookie: current }));
      configureSsrClient({ origin: ORIGIN, cookie: current });

      await getV1IdentityUsersMe();
      current = "wallow_bff=second";
      await getV1IdentityUsersMe();

      expect(fetchMock).toHaveBeenCalledTimes(2);

      const first: Request = fetchMock.mock.calls[0]![0];
      const second: Request = fetchMock.mock.calls[1]![0];

      expect(first.url).toBe(`${ORIGIN}/api/v1/identity/users/me`);
      expect(first.headers.get("cookie")).toBe("wallow_bff=first");
      expect(second.headers.get("cookie")).toBe("wallow_bff=second");
    });
  });
});
