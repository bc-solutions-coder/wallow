import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterEach, describe, expect, it } from "vitest";

import { createWallowSdk, type CreateWallowSdkOptions, type WallowSdk } from "./create-sdk";
import { setCsrfToken } from "./csrf";
// The generator's own module-global instance. `src/client.ts` used to re-export it
// as the package's public `client`; that re-export is deleted (Wallow-pu6a.5.5),
// but the generated singleton still exists, so "the factory never touches it"
// remains the assertion — it just has to be reached at its generated path now.
import { client as generatedSingleton } from "./generated/client.gen";
import { usersGetCurrentUser } from "./generated/sdk.gen";

/**
 * Spec (Wallow-pu6a.3.5): `createWallowSdk()` is the per-request replacement for
 * the module-global client singleton.
 *
 * The singleton model (`src/client.ts` + `configureBffClient`/`configureSsrClient`)
 * is a server-side correctness bug waiting to happen: concurrent SSR renders share
 * one client, so the last render to configure it wins, its forwarded `Cookie` leaks
 * into another user's render, and every re-configure appends another interceptor to
 * the same list. This factory builds a fresh instance per request instead, and these
 * tests pin the six properties that make that safe:
 *
 *   (a) two instances do not bleed state into each other;
 *   (b) the CSRF interceptor registers exactly ONCE per instance, and never onto the
 *       module-global client;
 *   (c) an SSR instance forwards ITS OWN per-request cookie header;
 *   (d) `baseUrl` is required — no baked `/api` default sneaking in via the
 *       generated `createClientConfig()` runtime hook;
 *   (e) `internalOrigin` is applied ONLY inside the instance's `fetch`, so a server
 *       instance and a browser instance built with the same `baseUrl` are
 *       indistinguishable everywhere request identity is derived (the client's
 *       configured `baseUrl` and the URL the client builds before transport) — that
 *       is what keeps an SSR-primed cache hydration-compatible with the browser;
 *   (f) lives in `server/internal-origin.test.ts`.
 */

const repoRelativeSrc: string = dirname(fileURLToPath(import.meta.url));

const BROWSER_BASE_URL = "https://app.test/api";
const OTHER_BASE_URL = "https://other.test/api";
const INTERNAL_ORIGIN = "http://localhost:3000";

/** A `fetch` that records the requests it is handed and answers with 200 JSON. */
interface FetchRecorder {
  readonly fetch: typeof globalThis.fetch;
  /** The requests the client handed to transport, in order. */
  readonly requests: Request[];
  /** The single request the client sent; throws unless exactly one was sent. */
  only: () => Request;
}

function recordingFetch(): FetchRecorder {
  const requests: Request[] = [];
  const fetchImpl: typeof globalThis.fetch = (input: RequestInfo | URL): Promise<Response> => {
    if (!(input instanceof Request)) {
      throw new TypeError("the generated client must call fetch with a Request instance");
    }
    requests.push(input);
    return Promise.resolve(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { "content-type": "application/json" },
      }),
    );
  };
  return {
    fetch: fetchImpl,
    requests,
    only: (): Request => {
      if (requests.length !== 1) {
        throw new Error(`expected exactly one request, got ${requests.length}`);
      }
      return requests[0] as Request;
    },
  };
}

/** Drive a GET through the instance so the transport-visible request can be read. */
async function sendGet(sdk: WallowSdk, url: string = "/v1/identity/users/me"): Promise<void> {
  await sdk.client.get({ url });
}

afterEach(() => {
  // The CSRF token lives in module scope (src/csrf.ts) and is deliberately NOT
  // per-instance, so reset it between tests. The factory throws in the red phase;
  // this teardown must not mask that.
  setCsrfToken(null);
});

describe("(a) instances do not share state", () => {
  it("keeps each instance's baseUrl to itself", () => {
    const first: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    const second: WallowSdk = createWallowSdk({ baseUrl: OTHER_BASE_URL });

    expect(first.client.getConfig().baseUrl).toBe(BROWSER_BASE_URL);
    expect(second.client.getConfig().baseUrl).toBe(OTHER_BASE_URL);
  });

  it("does not hand two instances the same underlying client object", () => {
    const first: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    const second: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });

    expect(first.client).not.toBe(second.client);
    expect(first.client).not.toBe(generatedSingleton);
  });

  it("leaves a sibling untouched when one instance is reconfigured", () => {
    const first: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    const second: WallowSdk = createWallowSdk({ baseUrl: OTHER_BASE_URL });

    first.client.setConfig({ baseUrl: "https://reconfigured.test/api" });

    expect(second.client.getConfig().baseUrl).toBe(OTHER_BASE_URL);
  });

  it("never mutates the module-global generated client", () => {
    const before: string | undefined = generatedSingleton.getConfig().baseUrl;

    createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    createWallowSdk({ baseUrl: OTHER_BASE_URL });

    expect(generatedSingleton.getConfig().baseUrl).toBe(before);
  });

  it("sends each instance's requests to its own baseUrl", async () => {
    const firstTransport: FetchRecorder = recordingFetch();
    const secondTransport: FetchRecorder = recordingFetch();
    const first: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      fetch: firstTransport.fetch,
    });
    const second: WallowSdk = createWallowSdk({
      baseUrl: OTHER_BASE_URL,
      fetch: secondTransport.fetch,
    });

    await sendGet(first);
    await sendGet(second);

    expect(firstTransport.only().url).toBe("https://app.test/api/v1/identity/users/me");
    expect(secondTransport.only().url).toBe("https://other.test/api/v1/identity/users/me");
  });

  it("binds a generated operation to the instance passed as the { client } call option", async () => {
    const transport: FetchRecorder = recordingFetch();
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      fetch: transport.fetch,
    });

    await usersGetCurrentUser({ client: sdk.client });

    expect(transport.only().url).toBe("https://app.test/api/v1/identity/users/me");
  });
});

describe("(b) the CSRF interceptor registers exactly once per instance", () => {
  // CSRF is the ONLY thing the factory wires as an interceptor. The two other
  // per-request concerns — `cookieHeader` and `internalOrigin` — belong inside the
  // instance's `fetch`, since `internalOrigin` MUST NOT change the request the
  // interceptor chain sees (test (e)) and keeping both on the same seam is what
  // makes this count a stable assertion.
  it("registers exactly one request interceptor on a fresh instance", () => {
    const sdk: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });

    const registered = sdk.client.interceptors.request.fns.filter(
      (fn: unknown): boolean => fn !== null,
    );

    expect(registered).toHaveLength(1);
  });

  it("registers exactly one request interceptor on an SSR instance too", () => {
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      cookieHeader: "wallow_bff=sess",
      internalOrigin: INTERNAL_ORIGIN,
    });

    const registered = sdk.client.interceptors.request.fns.filter(
      (fn: unknown): boolean => fn !== null,
    );

    expect(registered).toHaveLength(1);
  });

  it("is the CSRF interceptor — it stamps x-csrf-token on a state-changing request", async () => {
    const transport: FetchRecorder = recordingFetch();
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      fetch: transport.fetch,
    });
    setCsrfToken("token-abc");

    await sdk.client.post({ url: "/v1/identity/users/me" });

    expect(transport.only().headers.get("x-csrf-token")).toBe("token-abc");
  });

  it("does not accumulate interceptors as instances are created", () => {
    const first: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    let latest: WallowSdk = first;
    for (let index = 0; index < 20; index += 1) {
      latest = createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    }

    expect(latest.client.interceptors.request.fns).toHaveLength(
      first.client.interceptors.request.fns.length,
    );
  });

  it("registers nothing on the module-global generated client", () => {
    const before: number = generatedSingleton.interceptors.request.fns.length;

    createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    createWallowSdk({ baseUrl: BROWSER_BASE_URL, cookieHeader: "wallow_bff=sess" });

    expect(generatedSingleton.interceptors.request.fns).toHaveLength(before);
  });
});

describe("(c) an SSR instance forwards its per-request cookie header", () => {
  it("stamps the configured cookie header on outgoing requests", async () => {
    const transport: FetchRecorder = recordingFetch();
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      cookieHeader: "wallow_bff=sess-one",
      fetch: transport.fetch,
    });

    await sendGet(sdk);

    expect(transport.only().headers.get("cookie")).toBe("wallow_bff=sess-one");
  });

  it("sends no cookie header when the instance was given none", async () => {
    const transport: FetchRecorder = recordingFetch();
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      fetch: transport.fetch,
    });

    await sendGet(sdk);

    expect(transport.only().headers.get("cookie")).toBeNull();
  });

  it("keeps two concurrent renders' cookies apart", async () => {
    const firstTransport: FetchRecorder = recordingFetch();
    const secondTransport: FetchRecorder = recordingFetch();
    const first: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      cookieHeader: "wallow_bff=alice",
      fetch: firstTransport.fetch,
    });
    const second: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      cookieHeader: "wallow_bff=bob",
      fetch: secondTransport.fetch,
    });

    await Promise.all([sendGet(first), sendGet(second)]);

    expect(firstTransport.only().headers.get("cookie")).toBe("wallow_bff=alice");
    expect(secondTransport.only().headers.get("cookie")).toBe("wallow_bff=bob");
  });
});

describe("(d) baseUrl is a required option with no baked default", () => {
  it("pins baseUrl as a REQUIRED property of the options type", () => {
    type RequiredKeysOf<T> = {
      [K in keyof T]-?: Record<never, never> extends Pick<T, K> ? never : K;
    }[keyof T];

    // Compile-time assertion: this line stops typechecking the moment `baseUrl`
    // becomes optional, which is how a `/api` default would creep back in.
    const baseUrlIsRequired: "baseUrl" extends RequiredKeysOf<CreateWallowSdkOptions>
      ? true
      : false = true;

    expect(baseUrlIsRequired).toBe(true);
  });

  it("uses the caller's baseUrl verbatim rather than the generated /api default", () => {
    const sdk: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });

    // The generated `client.gen.ts` singleton is built through
    // `createClientConfig()` (src/runtime-config.ts), which HARD-CODES
    // `baseUrl: "/api"`. Routing the factory through that hook instead of a plain
    // `createConfig({ baseUrl })` would silently overwrite the caller's value.
    expect(sdk.client.getConfig().baseUrl).toBe(BROWSER_BASE_URL);
    expect(sdk.client.getConfig().baseUrl).not.toBe("/api");
  });

  it("rejects an empty baseUrl instead of falling back to a default", () => {
    expect(() => createWallowSdk({ baseUrl: "" })).toThrow();
    expect(() => createWallowSdk({ baseUrl: "   " })).toThrow();
  });
});

describe("(e) internalOrigin applies only inside the instance's fetch", () => {
  it("leaves the configured baseUrl byte-identical to a browser instance's", () => {
    const browser: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    const server: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      internalOrigin: INTERNAL_ORIGIN,
    });

    expect(server.client.getConfig().baseUrl).toBe(browser.client.getConfig().baseUrl);
    expect(JSON.stringify(server.client.getConfig().baseUrl)).toBe(
      JSON.stringify(browser.client.getConfig().baseUrl),
    );
  });

  it("builds a byte-identical request URL on both instances before transport", async () => {
    const browserTransport: FetchRecorder = recordingFetch();
    const serverTransport: FetchRecorder = recordingFetch();
    const browser: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      fetch: browserTransport.fetch,
    });
    const server: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      internalOrigin: INTERNAL_ORIGIN,
      fetch: serverTransport.fetch,
    });

    // Interceptors run AFTER the client builds the Request and BEFORE transport,
    // so this observes the request identity the SDK derives from `baseUrl` — the
    // thing an SSR-primed cache and the browser must agree on.
    const built: string[] = [];
    for (const sdk of [browser, server]) {
      sdk.client.interceptors.request.use((request: Request): Request => {
        built.push(request.url);
        return request;
      });
    }

    await sendGet(browser, "/v1/identity/users/me");
    await sendGet(server, "/v1/identity/users/me");

    expect(built).toHaveLength(2);
    expect(built[1]).toBe(built[0]);
  });

  it("retargets the outgoing request at the internal origin", async () => {
    const transport: FetchRecorder = recordingFetch();
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      internalOrigin: INTERNAL_ORIGIN,
      fetch: transport.fetch,
    });

    await sendGet(sdk);

    expect(transport.only().url).toBe("http://localhost:3000/api/v1/identity/users/me");
  });

  it("keeps the path, query and method intact while retargeting", async () => {
    const transport: FetchRecorder = recordingFetch();
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      internalOrigin: INTERNAL_ORIGIN,
      fetch: transport.fetch,
    });

    await sdk.client.get({ url: "/v1/identity/users", query: { page: 2 } });

    const sent: Request = transport.only();
    expect(sent.method).toBe("GET");
    expect(new URL(sent.url).pathname).toBe("/api/v1/identity/users");
    expect(new URL(sent.url).searchParams.get("page")).toBe("2");
  });

  it("preserves a request body while retargeting", async () => {
    const transport: FetchRecorder = recordingFetch();
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      internalOrigin: INTERNAL_ORIGIN,
      fetch: transport.fetch,
    });

    await sdk.client.post({ url: "/v1/identity/users", body: { email: "a@b.test" } });

    const sent: Request = transport.only();
    expect(sent.method).toBe("POST");
    expect(await sent.text()).toBe(JSON.stringify({ email: "a@b.test" }));
  });

  it("targets the browser-facing baseUrl when no internal origin is given", async () => {
    const transport: FetchRecorder = recordingFetch();
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      fetch: transport.fetch,
    });

    await sendGet(sdk);

    expect(transport.only().url).toBe("https://app.test/api/v1/identity/users/me");
  });

  it("does not leak the internal origin into the client's configuration", () => {
    const sdk: WallowSdk = createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      internalOrigin: INTERNAL_ORIGIN,
    });

    expect(JSON.stringify(sdk.client.getConfig())).not.toContain(INTERNAL_ORIGIN);
  });
});

describe("the singleton-configuration modules are gone", () => {
  // The factory does not merely supersede the module-global configure-once model —
  // Wallow-pu6a.5.5 deleted it. These modules carried a `@deprecated` marker during
  // the transition; now their absence is the assertion, so nothing can quietly
  // reintroduce a second, process-wide way to configure the SDK.
  it.each(["client.ts", "ssr.ts", "facade.ts", "mfa-client.ts"])(
    "no longer ships src/%s",
    (file: string) => {
      expect(existsSync(resolve(repoRelativeSrc, file))).toBe(false);
    },
  );
});
