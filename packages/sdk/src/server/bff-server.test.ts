import { afterEach, describe, expect, it, vi } from "vitest";

import {
  createWallowBffServer,
  WALLOW_API_MOUNT,
  WALLOW_BFF_MOUNT,
  type WallowBffServer,
} from "./bff-server";
import { type BffConfig } from "./config";
import { CSRF_HEADER } from "./csrf";
import { type BffSession } from "./session";
import { CookieSessionStore } from "./store/cookie";
import { type NodeRedisClient } from "./store/redis-adapter";
import { type SessionStore } from "./store/types";
import { ValkeySessionStore } from "./store/valkey";

/**
 * Spec (Wallow-pu6a.3.7): `createWallowBffServer` is the preset that absorbs the
 * host wiring every fork copies today — env config load, session-store
 * selection, and handler + proxy construction over ONE shared store — behind
 * three web-standard entry points.
 *
 * Hermetic mock of openid-client, mirroring `handlers.test.ts`: discovery
 * reconstructs endpoints from the requested metadata URL's origin, so the login
 * redirect is exercised with no live network I/O. Every test uses a unique
 * issuer because the discovery cache in `oidc.ts` is keyed by metadata URL.
 */
vi.mock("openid-client", () => ({
  discovery: vi.fn((server: URL) => {
    const origin: string = new URL(server).origin;
    return Promise.resolve({
      serverMetadata: (): Record<string, unknown> => ({
        issuer: origin,
        authorization_endpoint: `${origin}/connect/authorize`,
        token_endpoint: `${origin}/connect/token`,
        end_session_endpoint: `${origin}/connect/logout`,
      }),
    });
  }),
  allowInsecureRequests: vi.fn(),
  buildAuthorizationUrl: vi.fn(
    (
      configuration: { serverMetadata: () => Record<string, unknown> },
      params: Record<string, string>,
    ): URL => {
      const endpoint: string = configuration.serverMetadata().authorization_endpoint as string;
      const url: URL = new URL(endpoint);
      for (const [key, value] of Object.entries(params)) {
        url.searchParams.set(key, value);
      }
      return url;
    },
  ),
  authorizationCodeGrant: vi.fn(),
  fetchUserInfo: vi.fn(),
  skipSubjectCheck: Symbol("skipSubjectCheck"),
  buildEndSessionUrl: vi.fn(
    (
      configuration: { serverMetadata: () => Record<string, unknown> },
      params: Record<string, string>,
    ): URL => {
      const endpoint: string = configuration.serverMetadata().end_session_endpoint as string;
      const url: URL = new URL(endpoint);
      for (const [key, value] of Object.entries(params)) {
        url.searchParams.set(key, value);
      }
      return url;
    },
  ),
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

/** The seven variables `loadBffConfigFromEnv` requires, plus the dev-http opt-outs. */
function requiredEnv(overrides: Record<string, string> = {}): NodeJS.ProcessEnv {
  return {
    OIDC_ISSUER: "https://auth.example.com",
    OIDC_CLIENT_ID: "wallow-bff",
    OIDC_CLIENT_SECRET: "s3cret",
    OIDC_REDIRECT_URI: "https://app.example.com/bff/callback",
    OIDC_POST_LOGOUT_REDIRECT_URI: "https://app.example.com/",
    BFF_API_BASE_URL: "https://api.example.com",
    COOKIE_PASSWORD: "0".repeat(32),
    ...overrides,
  } as NodeJS.ProcessEnv;
}

/** A config with a plain cookie name, so the test cookie header stays readable. */
function makeConfig(issuer: string, overrides: Partial<BffConfig> = {}): BffConfig {
  return {
    issuer,
    clientId: "web-bff",
    clientSecret: "s3cret",
    redirectUri: "https://app.example.com/bff/callback",
    postLogoutRedirectUri: "https://app.example.com/",
    scopes: ["openid", "profile", "email", "offline_access"],
    apiBaseUrl: "https://api.example.com",
    cookieName: "wallow_bff",
    cookiePassword: "x".repeat(32),
    sessionTtlSeconds: 86400,
    cookieSecure: true,
    ...overrides,
  };
}

/** A store that records the references it is asked to resolve and never finds one. */
function recordingStore(): { store: SessionStore; reads: string[] } {
  const reads: string[] = [];
  const store: SessionStore = {
    read: (ref: string): Promise<BffSession | null> => {
      reads.push(ref);
      return Promise.resolve(null);
    },
    write: (): Promise<string> => Promise.resolve("ref-written"),
    destroy: (): Promise<void> => Promise.resolve(),
    withRefreshLock: <T>(_ref: string, fn: () => Promise<T>): Promise<T | undefined> => fn(),
  };
  return { store, reads };
}

/** An in-memory stand-in for a connected node-redis client. */
function fakeRedisClient(): NodeRedisClient {
  const data: Map<string, string> = new Map<string, string>();
  return {
    get: (key: string): Promise<string | null> => Promise.resolve(data.get(key) ?? null),
    set: (key: string, value: string): Promise<string | null> => {
      data.set(key, value);
      return Promise.resolve("OK");
    },
    del: (key: string): Promise<number> => Promise.resolve(data.delete(key) ? 1 : 0),
  };
}

describe("createWallowBffServer — mount points", () => {
  it("exports the mount prefixes as constants so hosts and the SDK cannot drift", () => {
    expect(WALLOW_API_MOUNT).toBe("/api");
    expect(WALLOW_BFF_MOUNT).toBe("/bff");
  });
});

describe("createWallowBffServer — configuration", () => {
  it("loads the config from the environment when none is supplied", () => {
    const server: WallowBffServer = createWallowBffServer({ env: requiredEnv() });

    expect(server.config.issuer).toBe("https://auth.example.com");
    expect(server.config.clientId).toBe("wallow-bff");
    expect(server.config.apiBaseUrl).toBe("https://api.example.com");
  });

  it("propagates the env contract's fail-fast: a missing variable throws at construction", () => {
    const env: NodeJS.ProcessEnv = requiredEnv();
    delete env.COOKIE_PASSWORD;

    expect(() => createWallowBffServer({ env })).toThrow(/COOKIE_PASSWORD/u);
  });

  it("uses an explicitly supplied config instead of reading the environment", () => {
    const config: BffConfig = makeConfig("https://issuer-explicit.example.com");

    const server: WallowBffServer = createWallowBffServer({ config, env: {} as NodeJS.ProcessEnv });

    expect(server.config).toEqual(config);
  });
});

/**
 * Store selection is the one knob swapped between environments, and both the
 * tunnel handlers and the proxy MUST resolve sessions through the SAME
 * instance — the proxy has to read the sessions the login callback wrote. The
 * SDK never imports `redis`, so a Valkey-backed host hands in its own connected
 * client; asking for `REDIS_URL` without one is a misconfiguration that has to
 * fail at boot rather than silently degrade to stateless cookie sessions.
 */
describe("createWallowBffServer — session-store selection", () => {
  it("defaults to the stateless cookie store when REDIS_URL is unset", () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-cookie.test"),
    });

    expect(server.store).toBeInstanceOf(CookieSessionStore);
  });

  it("builds a Valkey store when REDIS_URL is set and a client is supplied", () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-valkey.test"),
      env: { REDIS_URL: "redis://valkey:6379" } as NodeJS.ProcessEnv,
      redisClient: fakeRedisClient(),
    });

    expect(server.store).toBeInstanceOf(ValkeySessionStore);
  });

  it("throws at construction when REDIS_URL is set but no client was supplied", () => {
    expect(() =>
      createWallowBffServer({
        config: makeConfig("https://issuer-noclient.test"),
        env: { REDIS_URL: "redis://valkey:6379" } as NodeJS.ProcessEnv,
      }),
    ).toThrow(/REDIS_URL/u);
  });

  it("treats an empty REDIS_URL as unset", () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-emptyredis.test"),
      env: { REDIS_URL: "" } as NodeJS.ProcessEnv,
    });

    expect(server.store).toBeInstanceOf(CookieSessionStore);
  });

  it("lets an explicit store win over every selection rule", () => {
    const { store } = recordingStore();

    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-explicitstore.test"),
      env: { REDIS_URL: "redis://valkey:6379" } as NodeJS.ProcessEnv,
      store,
    });

    expect(server.store).toBe(store);
  });

  it("resolves sessions for BOTH the tunnel handlers and the proxy through the same store", async () => {
    const { store, reads } = recordingStore();
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-sharedstore.test"),
      store,
    });

    await server.handleBff(
      new Request(`http://app.example.com${WALLOW_BFF_MOUNT}/user`, {
        headers: { cookie: "wallow_bff=ref-shared-123" },
      }),
    );
    await server.handleApi(
      new Request(`http://app.example.com${WALLOW_API_MOUNT}/v1/identity/users/me`, {
        headers: { cookie: "wallow_bff=ref-shared-123" },
      }),
    );

    expect(reads).toEqual(["ref-shared-123", "ref-shared-123"]);
  });
});

describe("createWallowBffServer — health", () => {
  it("answers 200 with a JSON liveness body", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-health.test"),
    });

    const res: Response = server.handleHealth();

    expect(res.status).toBe(200);
    expect(res.headers.get("content-type")).toContain("application/json");
    await expect(res.json()).resolves.toEqual({ status: "ok" });
  });
});

/**
 * The dispatcher replaces the h3 router each host wired by hand. It routes on
 * path ONLY: method policy belongs to the handlers themselves (a bare
 * `GET /bff/logout` must reach the logout handler so it can answer 405 with
 * `Allow: POST`, rather than being swallowed as a 404 by the router).
 */
describe("createWallowBffServer — BFF dispatch", () => {
  it("routes /bff/login to the login handler, which redirects to the authorize endpoint", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-login.test"),
    });

    const res: Response = await server.handleBff(
      new Request(`http://app.example.com${WALLOW_BFF_MOUNT}/login`),
    );

    expect(res.status).toBe(302);
    const location: URL = new URL(res.headers.get("location") ?? "");
    expect(location.origin).toBe("https://issuer-login.test");
    expect(location.pathname).toBe("/connect/authorize");
    // The login transaction cookie proves the real handler ran, not a stub.
    expect(res.headers.getSetCookie().join(";")).toContain("wallow_bff_tx=");
  });

  it("routes /bff/user to the user handler, which answers 401 without a session", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-user.test"),
    });

    const res: Response = await server.handleBff(
      new Request(`http://app.example.com${WALLOW_BFF_MOUNT}/user`),
    );

    expect(res.status).toBe(401);
  });

  it("routes a bare GET /bff/logout to the logout handler, which rejects it with 405", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-logoutget.test"),
    });

    const res: Response = await server.handleBff(
      new Request(`http://app.example.com${WALLOW_BFF_MOUNT}/logout`),
    );

    expect(res.status).toBe(405);
    expect(res.headers.get("allow")).toBe("POST");
  });

  /**
   * This request carries NO Cookie header, so it is the genuinely anonymous
   * case: the logout handler answers 204 and clears cookies without consulting
   * the CSRF token at all (Wallow-vufu.5.1). It pins DISPATCH — that POST
   * reaches the logout handler rather than the 405 branch above. The CSRF gate
   * itself is exercised in `handlers.test.ts` against a real sealed session,
   * which is the only shape that can reach it.
   */
  it("routes POST /bff/logout to the logout handler, which answers an anonymous POST with 204", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-logoutpost.test"),
    });

    const res: Response = await server.handleBff(
      new Request(`http://app.example.com${WALLOW_BFF_MOUNT}/logout`, {
        method: "POST",
        headers: { [CSRF_HEADER]: "not-the-session-token" },
      }),
    );

    // Even a stale CSRF token cannot turn an anonymous logout into a 403.
    expect(res.status).toBe(204);
  });

  it("routes /bff/callback to the callback handler, which rejects a request with no state", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-callback.test"),
    });

    const res: Response = await server.handleBff(
      new Request(`http://app.example.com${WALLOW_BFF_MOUNT}/callback`),
    );

    // No transaction cookie and no code/state: the handler refuses it. What
    // matters here is that it is the handler answering, not the router's 404.
    expect(res.status).toBe(400);
  });

  it("answers 404 for an unknown path under the BFF mount", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-bffunknown.test"),
    });

    const res: Response = await server.handleBff(
      new Request(`http://app.example.com${WALLOW_BFF_MOUNT}/not-a-handler`),
    );

    expect(res.status).toBe(404);
  });

  it("answers 404 for a path outside the BFF mount, including a lookalike prefix", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-bffoutside.test"),
    });

    await expect(
      server.handleBff(new Request("http://app.example.com/dashboard")).then((r) => r.status),
    ).resolves.toBe(404);
    await expect(
      server.handleBff(new Request("http://app.example.com/bffoo/user")).then((r) => r.status),
    ).resolves.toBe(404);
  });
});

/**
 * `handleApi` is the ported `createApiProxy` and keeps its contract: the `/api`
 * allowlist rejects anything outside the mount BEFORE a session is read, and a
 * request with no session cookie is unauthorized rather than forwarded.
 */
describe("createWallowBffServer — API dispatch", () => {
  it("answers 401 for an /api request that carries no session cookie", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-api401.test"),
    });

    const res: Response = await server.handleApi(
      new Request(`http://app.example.com${WALLOW_API_MOUNT}/v1/identity/users/me`),
    );

    expect(res.status).toBe(401);
  });

  it("answers 404 for a path outside the API mount, without reading a session", async () => {
    const { store, reads } = recordingStore();
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-api404.test"),
      store,
    });

    const res: Response = await server.handleApi(
      new Request("http://app.example.com/v1/identity/users/me", {
        headers: { cookie: "wallow_bff=ref-outside" },
      }),
    );

    expect(res.status).toBe(404);
    expect(reads).toEqual([]);
  });

  it("does not forward an /api request upstream when the session is missing", async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal("fetch", fetchSpy);
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-apinofetch.test"),
    });

    await server.handleApi(
      new Request(`http://app.example.com${WALLOW_API_MOUNT}/v1/identity/users/me`),
    );

    expect(fetchSpy).not.toHaveBeenCalled();
  });
});

/**
 * The preset and its mount constants are part of the published `./server`
 * surface — a fork imports them from `@bc-solutions-coder/sdk/server`, so the
 * barrel has to re-export them.
 */
describe("server entry exports", () => {
  it("re-exports the preset and both mount constants from the server barrel", async () => {
    const barrel = await import("./index");

    expect(typeof barrel.createWallowBffServer).toBe("function");
    expect(barrel.WALLOW_API_MOUNT).toBe("/api");
    expect(barrel.WALLOW_BFF_MOUNT).toBe("/bff");
  });
});
