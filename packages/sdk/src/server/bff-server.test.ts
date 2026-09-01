import { afterEach, describe, expect, it, vi } from "vitest";

import {
  createWallowBffServer,
  WALLOW_API_MOUNT,
  WALLOW_BFF_MOUNT,
  type WallowBffServer,
  type WallowBffServerOptions,
} from "./bff-server";
import { type BffConfig } from "./config";
import { type PeerRequest } from "./forwarded";
import { CSRF_HEADER } from "./csrf";
import { sealSession, type BffSession } from "./session";
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
const { discoveryMetadataByOrigin, discoveryFailures } = vi.hoisted(() => ({
  /** Extra serverMetadata a test wants its issuer's discovery stub to advertise. */
  discoveryMetadataByOrigin: new Map<string, Record<string, unknown>>(),
  /** Issuer origins whose discovery should fail, for the boot-probe specs. */
  discoveryFailures: new Set<string>(),
}));

vi.mock("openid-client", () => ({
  discovery: vi.fn((server: URL) => {
    const origin: string = new URL(server).origin;
    if (discoveryFailures.has(origin)) {
      return Promise.reject(new Error(`discovery unreachable for ${origin}`));
    }
    return Promise.resolve({
      serverMetadata: (): Record<string, unknown> => ({
        issuer: origin,
        authorization_endpoint: `${origin}/connect/authorize`,
        token_endpoint: `${origin}/connect/token`,
        end_session_endpoint: `${origin}/connect/logout`,
        ...discoveryMetadataByOrigin.get(origin),
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

const { redisCreateClientMock, redisConnectMock, redisData, redisSets } = vi.hoisted(() => {
  const redisConnectMock: ReturnType<typeof vi.fn> = vi.fn(() => Promise.resolve());
  const data: Map<string, string> = new Map<string, string>();
  const sets: Map<string, Set<string>> = new Map<string, Set<string>>();
  const redisCreateClientMock: ReturnType<typeof vi.fn> = vi.fn(() => ({
    on: vi.fn(),
    connect: redisConnectMock,
    get: (key: string): Promise<string | null> => Promise.resolve(data.get(key) ?? null),
    set: (key: string, value: string): Promise<string | null> => {
      data.set(key, value);
      return Promise.resolve("OK");
    },
    del: (key: string): Promise<number> => Promise.resolve(data.delete(key) ? 1 : 0),
    sAdd: (key: string, member: string): Promise<number> => {
      const members: Set<string> = sets.get(key) ?? new Set<string>();
      const added: boolean = !members.has(member);
      members.add(member);
      sets.set(key, members);
      return Promise.resolve(added ? 1 : 0);
    },
    sRem: (key: string, member: string): Promise<number> =>
      Promise.resolve(sets.get(key)?.delete(member) === true ? 1 : 0),
    sMembers: (key: string): Promise<string[]> => Promise.resolve([...(sets.get(key) ?? [])]),
    expire: (): Promise<boolean> => Promise.resolve(true),
  }));
  return { redisCreateClientMock, redisConnectMock, redisData: data, redisSets: sets };
});

/** `redis` is an OPTIONAL peer the preset imports lazily; stand it in here. */
vi.mock("redis", () => ({ createClient: redisCreateClientMock }));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.clearAllMocks();
  // The stand-in's maps are module-scoped; keep boot-time namespace claims and
  // session writes from leaking between tests.
  redisData.clear();
  redisSets.clear();
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
  const sets: Map<string, Set<string>> = new Map<string, Set<string>>();
  return {
    get: (key: string): Promise<string | null> => Promise.resolve(data.get(key) ?? null),
    set: (key: string, value: string): Promise<string | null> => {
      data.set(key, value);
      return Promise.resolve("OK");
    },
    del: (key: string): Promise<number> => Promise.resolve(data.delete(key) ? 1 : 0),
    sAdd: (key: string, member: string): Promise<number> => {
      const members: Set<string> = sets.get(key) ?? new Set<string>();
      const added: boolean = !members.has(member);
      members.add(member);
      sets.set(key, members);
      return Promise.resolve(added ? 1 : 0);
    },
    sRem: (key: string, member: string): Promise<number> =>
      Promise.resolve(sets.get(key)?.delete(member) === true ? 1 : 0),
    sMembers: (key: string): Promise<string[]> => Promise.resolve([...(sets.get(key) ?? [])]),
    expire: (): Promise<boolean> => Promise.resolve(true),
  };
}

/**
 * A client shaped like a real node-redis `createClient()` result: replies are
 * typed as broadly as node-redis types them (`string | Buffer | null` for
 * `GET`/`SET`), which is what makes the port's `unknown` replies necessary.
 */
function nodeRedisShapedClient(): {
  get: (key: string) => Promise<string | Buffer | null>;
  set: (
    key: string,
    value: string,
    options?: { EX?: number; NX?: true },
  ) => Promise<string | Buffer | null>;
  del: (key: string) => Promise<number>;
  sAdd: (key: string, member: string) => Promise<number>;
  sRem: (key: string, member: string) => Promise<number>;
  sMembers: (key: string) => Promise<string[]>;
  expire: (key: string, seconds: number) => Promise<boolean>;
} {
  const data: Map<string, string> = new Map<string, string>();
  const sets: Map<string, Set<string>> = new Map<string, Set<string>>();
  return {
    get: (key: string): Promise<string | Buffer | null> => Promise.resolve(data.get(key) ?? null),
    set: (key: string, value: string, options?: { NX?: true }): Promise<string | Buffer | null> => {
      if (options?.NX === true && data.has(key)) {
        return Promise.resolve(null);
      }
      data.set(key, value);
      return Promise.resolve("OK");
    },
    del: (key: string): Promise<number> => Promise.resolve(data.delete(key) ? 1 : 0),
    sAdd: (key: string, member: string): Promise<number> => {
      const members: Set<string> = sets.get(key) ?? new Set<string>();
      const added: boolean = !members.has(member);
      members.add(member);
      sets.set(key, members);
      return Promise.resolve(added ? 1 : 0);
    },
    sRem: (key: string, member: string): Promise<number> =>
      Promise.resolve(sets.get(key)?.delete(member) === true ? 1 : 0),
    sMembers: (key: string): Promise<string[]> => Promise.resolve([...(sets.get(key) ?? [])]),
    expire: (): Promise<boolean> => Promise.resolve(true),
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
 * instance — the proxy has to read the sessions the login callback wrote. A
 * Valkey-backed host either hands in its own connected node-redis client or
 * just sets `REDIS_URL` and lets the preset connect (through the optional
 * `redis` peer); `REDIS_URL` never silently degrades to cookie sessions.
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

  it("accepts a node-redis client as-is, without a hand-written bridge", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-raw-node-redis.test"),
      env: { REDIS_URL: "redis://valkey:6379" } as NodeJS.ProcessEnv,
      redisClient: nodeRedisShapedClient(),
    });

    expect(server.store).toBeInstanceOf(ValkeySessionStore);
    const ref: string = await server.store.write({
      sessionId: "s-raw",
      accessToken: "a",
      refreshToken: "r",
      idToken: "i",
      expiresAt: Date.now() + 60_000,
      user: { sub: "u-raw" },
      version: 1,
    });
    const session = await server.store.read(ref);
    expect(session?.sessionId).toBe("s-raw");
    // The supplied client wins: the preset does not also open its own.
    expect(redisCreateClientMock).not.toHaveBeenCalled();
  });

  it("connects to REDIS_URL itself when no client is supplied, on first use of the store", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-selfconnect.test"),
      env: { REDIS_URL: "redis://valkey:6379" } as NodeJS.ProcessEnv,
    });

    expect(server.store).toBeInstanceOf(ValkeySessionStore);
    // Construction is synchronous and touches no network: the connection is
    // made lazily, once, when the store first needs it.
    expect(redisCreateClientMock).not.toHaveBeenCalled();

    const ref: string = await server.store.write({
      sessionId: "s-1",
      accessToken: "a",
      refreshToken: "r",
      idToken: "i",
      expiresAt: Date.now() + 60_000,
      user: { sub: "u-1" },
      version: 1,
    });
    await server.store.read(ref);

    expect(redisCreateClientMock).toHaveBeenCalledTimes(1);
    expect(redisCreateClientMock).toHaveBeenCalledWith({ url: "redis://valkey:6379" });
    expect(redisConnectMock).toHaveBeenCalledTimes(1);
    const session = await server.store.read(ref);
    expect(session?.sessionId).toBe("s-1");
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

describe("createWallowBffServer — Valkey namespacing (BFF_APP_ID)", () => {
  /** Let the fire-and-forget namespace claim settle. */
  function claimSettled(): Promise<void> {
    return new Promise((resolve) => {
      setTimeout(resolve, 0);
    });
  }

  function sampleSession(sub: string, sid: string): BffSession {
    return {
      sessionId: "",
      accessToken: "a",
      refreshToken: "r",
      idToken: "i",
      expiresAt: Date.now() + 60_000,
      user: { sub },
      sid,
      version: 1,
    };
  }

  it("separates sessions by appId when two BFFs share one Valkey", async () => {
    const issuer: string = "https://issuer-ns-split.test";
    const client: ReturnType<typeof nodeRedisShapedClient> = nodeRedisShapedClient();
    const onWarning: ReturnType<typeof vi.fn<(message: string) => void>> = vi.fn();

    const web: WallowBffServer = createWallowBffServer({
      config: makeConfig(issuer, { appId: "web" }),
      redisClient: client,
      onWarning,
    });
    const sibling: WallowBffServer = createWallowBffServer({
      config: makeConfig(issuer, { appId: "example" }),
      redisClient: client,
      onWarning,
    });

    const ref: string = await web.store.write(sampleSession("u-ns", "op-sid-ns"));

    // The sibling shares the Valkey AND the cookie password, yet cannot
    // resolve the reference: its keys live under its own prefix.
    expect(await sibling.store.read(ref)).toBeNull();

    // A restart of the SAME BFF still finds the session.
    const webAgain: WallowBffServer = createWallowBffServer({
      config: makeConfig(issuer, { appId: "web" }),
      redisClient: client,
      onWarning,
    });
    const session: BffSession | null = await webAgain.store.read(ref);
    expect(session?.user.sub).toBe("u-ns");

    // Disjoint prefixes and matching identities: nothing to warn about.
    await claimSettled();
    expect(onWarning).not.toHaveBeenCalled();
  });

  it("derives the key prefix from BFF_APP_ID for the self-connected REDIS_URL store", async () => {
    const server: WallowBffServer = createWallowBffServer({
      env: requiredEnv({
        OIDC_ISSUER: "https://issuer-ns-env.test",
        REDIS_URL: "redis://valkey:6379",
        BFF_APP_ID: "example",
      }),
    });

    await server.store.write(sampleSession("u-env", "op-sid-env"));

    const keys: string[] = [...redisData.keys()];
    expect(keys.some((key: string) => key.startsWith("wallow:example:session:"))).toBe(true);
    // The sid index moves with the namespace — it is what back-channel logout
    // resolves through, so a shared index is the cross-RP teardown footgun.
    expect(keys).toContain("wallow:example:sid:op-sid-env");
    expect(keys.some((key: string) => key.startsWith("wallow:session:"))).toBe(false);
  });

  it("keeps the bare wallow prefix when BFF_APP_ID is unset", async () => {
    const server: WallowBffServer = createWallowBffServer({
      env: requiredEnv({
        OIDC_ISSUER: "https://issuer-ns-bare.test",
        REDIS_URL: "redis://valkey:6379",
      }),
    });

    await server.store.write(sampleSession("u-bare", "op-sid-bare"));

    const keys: string[] = [...redisData.keys()];
    expect(keys.some((key: string) => key.startsWith("wallow:session:"))).toBe(true);
  });

  it("warns at boot when a different BFF identity already claimed the namespace", async () => {
    const client: ReturnType<typeof nodeRedisShapedClient> = nodeRedisShapedClient();
    await client.set("wallow:owner", "https://other-op.test other-bff");
    const onWarning: ReturnType<typeof vi.fn<(message: string) => void>> = vi.fn();

    createWallowBffServer({
      config: makeConfig("https://issuer-ns-conflict.test"),
      redisClient: client,
      onWarning,
    });

    await vi.waitFor(() => {
      expect(onWarning).toHaveBeenCalledTimes(1);
    });
    // The warning names the standing owner and the fix.
    expect(onWarning).toHaveBeenCalledWith(
      expect.stringContaining("https://other-op.test other-bff"),
    );
    expect(onWarning).toHaveBeenCalledWith(expect.stringContaining("BFF_APP_ID"));
  });

  it("stays silent when the same BFF identity reclaims its namespace", async () => {
    const issuer: string = "https://issuer-ns-reclaim.test";
    const client: ReturnType<typeof nodeRedisShapedClient> = nodeRedisShapedClient();
    const onWarning: ReturnType<typeof vi.fn<(message: string) => void>> = vi.fn();

    createWallowBffServer({ config: makeConfig(issuer), redisClient: client, onWarning });
    createWallowBffServer({ config: makeConfig(issuer), redisClient: client, onWarning });

    await claimSettled();
    expect(onWarning).not.toHaveBeenCalled();
  });

  it("swallows a namespace claim the store cannot answer at boot", async () => {
    const down = (): Promise<never> => Promise.reject(new Error("valkey down"));
    const unreachable: NodeRedisClient = {
      get: down,
      set: down,
      del: down,
      sAdd: down,
      sRem: down,
      sMembers: down,
      expire: down,
    };
    const onWarning: ReturnType<typeof vi.fn<(message: string) => void>> = vi.fn();

    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-ns-down.test"),
      redisClient: unreachable,
      onWarning,
    });

    await claimSettled();
    expect(onWarning).not.toHaveBeenCalled();
    // Construction itself survives the dead store.
    expect(server.store).toBeInstanceOf(ValkeySessionStore);
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

  /**
   * The front-channel logout notification is a cross-site GET the OP's logout
   * page fires from a hidden iframe — it must be dispatched to its handler
   * (which answers 200 unconditionally), not swallowed as a router 404.
   */
  it("routes /bff/frontchannel-logout to the frontchannel handler, which answers 200", async () => {
    const server: WallowBffServer = createWallowBffServer({
      config: makeConfig("https://issuer-frontchannel.test"),
    });

    const res: Response = await server.handleBff(
      new Request(
        `http://app.example.com${WALLOW_BFF_MOUNT}/frontchannel-logout?iss=https%3A%2F%2Fissuer-frontchannel.test&sid=sid-1`,
      ),
    );

    expect(res.status).toBe(200);
    expect(res.headers.get("cache-control")).toBe("no-store");
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
 * The BFF proxy resolves the caller's address ITSELF — from the peer srvx
 * exposes on `request.ip` and the trusted-proxy list — and appends it to the
 * upstream `X-Forwarded-For`, where the API's per-IP limiter reads the
 * rightmost entry. Nothing a host stamps onto the request is consulted, so an
 * own-domain consumer needs no helper of its own beyond the SDK.
 */
describe("createWallowBffServer — client address", () => {
  /** A fresh session, sealed as the cookie the default store reads back. */
  async function sessionCookie(config: BffConfig): Promise<string> {
    const session: BffSession = {
      sessionId: "sess-fixture-000",
      accessToken: "access-token-abc",
      refreshToken: "refresh-token-def",
      idToken: "header.payload.signature",
      expiresAt: Date.now() + 3_600_000,
      user: { sub: "user-123", email: "user@example.com", name: "Test User" },
      version: 1,
      csrfToken: "csrf-fixture-token",
    };
    return `${config.cookieName}=${await sealSession(session, config.cookiePassword)}`;
  }

  /** Answers discovery with a minimal document and everything else with `{}`. */
  function upstreamFetch(issuer: string): ReturnType<typeof vi.fn> {
    const doc = {
      issuer,
      authorization_endpoint: `${issuer}/connect/authorize`,
      token_endpoint: `${issuer}/connect/token`,
      jwks_uri: `${issuer}/.well-known/jwks`,
    };
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn((input: unknown): Promise<Response> => {
      const body: string = String(input).includes(".well-known") ? JSON.stringify(doc) : "{}";
      return Promise.resolve(
        new Response(body, { status: 200, headers: { "content-type": "application/json" } }),
      );
    });
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
  }

  /** The `X-Forwarded-For` the API received for one proxied request from `ip`. */
  async function forwardedForFrom(
    issuer: string,
    options: Omit<WallowBffServerOptions, "config">,
    ip: string | undefined,
    inboundHeaders: Record<string, string> = {},
  ): Promise<string | null> {
    const config: BffConfig = makeConfig(issuer);
    const fetchMock = upstreamFetch(issuer);
    const server: WallowBffServer = createWallowBffServer({ config, env: {}, ...options });

    const request: PeerRequest = Object.assign(
      new Request(`http://app.example.com${WALLOW_API_MOUNT}/v1/identity/users/me`, {
        headers: { cookie: await sessionCookie(config), ...inboundHeaders },
      }),
      { ip },
    );
    const res: Response = await server.handleApi(request);
    expect(res.status).toBe(200);

    const upstreamCall = fetchMock.mock.calls.find(
      (call): boolean => !String(call[0]).includes(".well-known"),
    );
    expect(upstreamCall).toBeDefined();
    const init = upstreamCall?.[1] as RequestInit;
    return new Headers(init.headers).get("x-forwarded-for");
  }

  it("stamps the peer address when no proxies are trusted", async () => {
    const forwardedFor: string | null = await forwardedForFrom(
      "https://issuer-ip-peer.test",
      {},
      "203.0.113.7",
    );

    expect(forwardedFor).toBe("203.0.113.7");
  });

  it("forwards the caller a trusted proxy reported, appended to the chain it wrote", async () => {
    const forwardedFor: string | null = await forwardedForFrom(
      "https://issuer-ip-trusted.test",
      { trustedProxies: "10.0.0.0/8" },
      "10.0.0.5",
      { "x-forwarded-for": "198.51.100.9" },
    );

    // Rightmost is what the API pops, so the resolved caller goes last.
    expect(forwardedFor).toBe("198.51.100.9, 198.51.100.9");
  });

  it("ignores a chain an untrusted peer sent and stamps the peer itself", async () => {
    const forwardedFor: string | null = await forwardedForFrom(
      "https://issuer-ip-spoof.test",
      { trustedProxies: "10.0.0.0/8" },
      "203.0.113.5",
      { "x-forwarded-for": "198.51.100.4" },
    );

    expect(forwardedFor).toBe("198.51.100.4, 203.0.113.5");
  });

  it("reads WALLOW_TRUSTED_PROXIES from the supplied environment", async () => {
    const forwardedFor: string | null = await forwardedForFrom(
      "https://issuer-ip-env.test",
      { env: { WALLOW_TRUSTED_PROXIES: "10.0.0.0/8" } as NodeJS.ProcessEnv },
      "10.0.0.5",
      { "x-forwarded-for": "198.51.100.9" },
    );

    expect(forwardedFor).toBe("198.51.100.9, 198.51.100.9");
  });

  it("writes no X-Forwarded-For when the host exposes no peer address", async () => {
    const forwardedFor: string | null = await forwardedForFrom(
      "https://issuer-ip-none.test",
      {},
      undefined,
    );

    expect(forwardedFor).toBeNull();
  });

  it("never believes a client-IP header the caller sent in place of the socket", async () => {
    const forwardedFor: string | null = await forwardedForFrom(
      "https://issuer-ip-header.test",
      {},
      undefined,
      { "x-wallow-client-ip": "203.0.113.7" },
    );

    expect(forwardedFor).toBeNull();
  });
});

/**
 * The preset and its mount constants are part of the published `./server`
 * surface — a fork imports them from `@bc-solutions-coder/sdk/server`, so the
 * barrel has to re-export them.
 */
describe("createWallowBffServer — back-channel boot warning", () => {
  afterEach(() => {
    discoveryMetadataByOrigin.clear();
    discoveryFailures.clear();
  });

  /** Let the fire-and-forget discovery probe settle. */
  function probeSettled(): Promise<void> {
    return new Promise((resolve) => {
      setTimeout(resolve, 0);
    });
  }

  it("warns when the issuer advertises back-channel logout but the store cannot revoke", async () => {
    const issuer: string = "https://op-warn-1.example.com";
    discoveryMetadataByOrigin.set(issuer, { backchannel_logout_supported: true });
    const onWarning: ReturnType<typeof vi.fn<(message: string) => void>> = vi.fn();

    createWallowBffServer({ env: requiredEnv({ OIDC_ISSUER: issuer }), onWarning });

    await vi.waitFor(() => {
      expect(onWarning).toHaveBeenCalledTimes(1);
    });
    expect(onWarning).toHaveBeenCalledWith(expect.stringContaining("back-channel logout"));
  });

  it("stays silent when the store can revoke — a Valkey-backed session store", async () => {
    const issuer: string = "https://op-warn-2.example.com";
    discoveryMetadataByOrigin.set(issuer, { backchannel_logout_supported: true });
    const onWarning: ReturnType<typeof vi.fn<(message: string) => void>> = vi.fn();

    createWallowBffServer({
      env: requiredEnv({ OIDC_ISSUER: issuer }),
      redisClient: fakeRedisClient(),
      onWarning,
    });

    await probeSettled();
    expect(onWarning).not.toHaveBeenCalled();
  });

  it("stays silent when the issuer does not advertise back-channel logout", async () => {
    const issuer: string = "https://op-warn-3.example.com";
    const onWarning: ReturnType<typeof vi.fn<(message: string) => void>> = vi.fn();

    createWallowBffServer({ env: requiredEnv({ OIDC_ISSUER: issuer }), onWarning });

    await probeSettled();
    expect(onWarning).not.toHaveBeenCalled();
  });

  it("swallows a failed boot probe — the OP may simply not be up yet", async () => {
    const issuer: string = "https://op-warn-4.example.com";
    discoveryFailures.add(issuer);
    const onWarning: ReturnType<typeof vi.fn<(message: string) => void>> = vi.fn();

    const server: WallowBffServer = createWallowBffServer({
      env: requiredEnv({ OIDC_ISSUER: issuer }),
      onWarning,
    });

    await probeSettled();
    expect(onWarning).not.toHaveBeenCalled();
    // Construction itself survives the dead OP.
    expect(server.config.issuer).toBe(issuer);
  });
});

describe("server entry exports", () => {
  it("re-exports the preset and both mount constants from the server barrel", async () => {
    const barrel = await import("./index");

    expect(typeof barrel.createWallowBffServer).toBe("function");
    expect(barrel.WALLOW_API_MOUNT).toBe("/api");
    expect(barrel.WALLOW_BFF_MOUNT).toBe("/bff");
  });
});
