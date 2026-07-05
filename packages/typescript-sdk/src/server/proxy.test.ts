import { createApp, toWebHandler, type App } from "h3";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { BffConfig } from "./config";
import { discover, refreshTokens } from "./oidc";
import type { DiscoveryDoc, TokenResponse } from "./oidc";
import { createApiProxy, ensureFreshSession } from "./proxy";
import { sealSession, type BffSession } from "./session";
import type { SessionStore } from "./store/types";

// Wrap the real discovery/refresh functions in spies so individual tests can
// override them (refresh unit tests) while the proxy integration tests fall
// back to the real fetch-driven implementation. `vi.restoreAllMocks()` in
// `afterEach` restores each spy to its `vi.fn(actual.*)` implementation.
vi.mock("./oidc", async (importOriginal) => {
  const actual: typeof import("./oidc") = await importOriginal();
  return {
    ...actual,
    discover: vi.fn(actual.discover),
    refreshTokens: vi.fn(actual.refreshTokens),
  };
});

/**
 * Hermetic mock of openid-client: the real `discover()` (used by the proxy
 * integration tests that fall back to `actual.discover`) resolves endpoints via
 * openid-client's `discovery()` rather than the native `fetch`. The stub
 * reconstructs the same endpoint shape as {@link makeDoc} from the requested
 * metadata URL's origin. The refresh grant now runs through openid-client's
 * `refreshTokenGrant`, so the integration test that falls back to
 * `actual.refreshTokens` gets its rotated token set from the stub below rather
 * than a native token-endpoint POST.
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
  refreshTokenGrant: vi.fn(() =>
    Promise.resolve({
      access_token: "refreshed-access",
      refresh_token: "rotated-refresh",
      expires_in: 3600,
      token_type: "Bearer",
    }),
  ),
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

/** Options for {@link makeFakeStore}. */
interface FakeStoreOptions {
  /** When true, `withRefreshLock` returns `undefined` (lock held by a peer). */
  lockHeld?: boolean;
  /** Invoked immediately before the refresh `fn` runs inside the lock. */
  onLockEnter?: () => void;
  /** Invoked immediately after the refresh `fn` settles inside the lock. */
  onLockExit?: () => void;
}

/** A recording fake {@link SessionStore} plus the calls it observed. */
interface FakeStore {
  store: SessionStore;
  calls: {
    read: number;
    write: BffSession[];
    destroy: string[];
    withRefreshLock: number;
  };
}

/**
 * Build an in-memory recording {@link SessionStore}. `stored` is what `read`
 * resolves to (the session a concurrent request may have already refreshed).
 */
function makeFakeStore(
  stored: BffSession | null,
  options: FakeStoreOptions = {},
): FakeStore {
  const calls: FakeStore["calls"] = {
    read: 0,
    write: [],
    destroy: [],
    withRefreshLock: 0,
  };
  let current: BffSession | null = stored;
  const store: SessionStore = {
    async read(): Promise<BffSession | null> {
      calls.read += 1;
      return current;
    },
    async write(session: BffSession): Promise<string> {
      calls.write.push(session);
      current = session;
      return "fake-ref";
    },
    async destroy(ref: string): Promise<void> {
      calls.destroy.push(ref);
    },
    async withRefreshLock<T>(
      _ref: string,
      fn: () => Promise<T>,
    ): Promise<T | undefined> {
      calls.withRefreshLock += 1;
      if (options.lockHeld === true) {
        return undefined;
      }
      options.onLockEnter?.();
      try {
        return await fn();
      } finally {
        options.onLockExit?.();
      }
    },
  };
  return { store, calls };
}

/**
 * Build a config. Each test passes a unique issuer so the module-level
 * discovery cache in oidc.ts never leaks a stubbed doc across tests.
 */
function makeConfig(
  issuer: string,
  overrides: Partial<BffConfig> = {},
): BffConfig {
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
    ...overrides,
  };
}

/** Discovery doc whose endpoints are rooted at the given issuer. */
function makeDoc(issuer: string): DiscoveryDoc {
  return {
    authorization_endpoint: `${issuer}/connect/authorize`,
    token_endpoint: `${issuer}/connect/token`,
    end_session_endpoint: `${issuer}/connect/logout`,
  };
}

function makeSession(overrides: Partial<BffSession> = {}): BffSession {
  return {
    sessionId: "sess-fixture-000",
    accessToken: "access-token-abc",
    refreshToken: "refresh-token-def",
    idToken: "header.payload.signature",
    expiresAt: Date.now() + 3_600_000,
    user: { sub: "user-123", email: "user@example.com", name: "Test User" },
    version: 1,
    ...overrides,
  };
}

describe("ensureFreshSession", () => {
  it("returns the session unchanged without acquiring the refresh lock when still fresh", async () => {
    const config: BffConfig = makeConfig("https://fresh-valid.example.com");
    const session: BffSession = makeSession({
      expiresAt: Date.now() + 3_600_000,
    });
    const { store, calls }: FakeStore = makeFakeStore(session);
    // Fail loudly if a refresh network call is attempted.
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result: BffSession = await ensureFreshSession(
      session,
      config,
      store,
      "ref-fresh",
    );

    expect(result).toEqual(session);
    // No lock acquired, no write, and refresh never touched.
    expect(calls.withRefreshLock).toBe(0);
    expect(calls.write.length).toBe(0);
    expect(refreshTokens).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("performs the refresh inside store.withRefreshLock, bumps version, and writes the new session", async () => {
    const config: BffConfig = makeConfig("https://refresh-lock.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const session: BffSession = makeSession({
      accessToken: "old-access",
      refreshToken: "old-refresh",
      expiresAt: Date.now() - 1_000,
      version: 4,
    });
    const tokens: TokenResponse = {
      access_token: "new-access",
      refresh_token: "new-refresh",
      id_token: "new-id",
      expires_in: 3600,
      token_type: "Bearer",
    };

    // The refresh must run strictly within the lock's critical section.
    let insideLock: boolean = false;
    const { store, calls }: FakeStore = makeFakeStore(session, {
      onLockEnter: (): void => {
        insideLock = true;
      },
      onLockExit: (): void => {
        insideLock = false;
      },
    });
    vi.mocked(discover).mockResolvedValue(doc);
    vi.mocked(refreshTokens).mockImplementation(
      async (): Promise<TokenResponse> => {
        expect(insideLock).toBe(true);
        return tokens;
      },
    );

    const result: BffSession = await ensureFreshSession(
      session,
      config,
      store,
      "ref-1",
    );

    // Refresh was gated by exactly one lock acquisition.
    expect(calls.withRefreshLock).toBe(1);
    expect(refreshTokens).toHaveBeenCalledTimes(1);
    // New tokens applied and identity preserved.
    expect(result.accessToken).toBe("new-access");
    expect(result.refreshToken).toBe("new-refresh");
    expect(result.idToken).toBe("new-id");
    expect(result.expiresAt).toBeGreaterThan(Date.now());
    expect(result.user).toEqual(session.user);
    // Version is bumped on refresh.
    expect(result.version).toBe(5);
    // The refreshed session is persisted through the store inside the lock.
    expect(calls.write.length).toBe(1);
    expect(calls.write[0].version).toBe(5);
    expect(calls.write[0].accessToken).toBe("new-access");
  });

  it("re-reads the session from the store instead of refreshing when the lock is held by a concurrent request", async () => {
    const config: BffConfig = makeConfig(
      "https://refresh-contended.example.com",
    );
    const session: BffSession = makeSession({
      accessToken: "stale-access",
      refreshToken: "old-refresh",
      expiresAt: Date.now() - 1_000,
      version: 2,
    });
    // The session a concurrent request already refreshed and stored.
    const refreshedByPeer: BffSession = makeSession({
      accessToken: "peer-refreshed-access",
      refreshToken: "peer-refresh",
      expiresAt: Date.now() + 3_600_000,
      version: 3,
    });
    const { store, calls }: FakeStore = makeFakeStore(refreshedByPeer, {
      lockHeld: true,
    });
    vi.mocked(refreshTokens).mockRejectedValue(
      new Error("refreshTokens must not run while the lock is held"),
    );

    const result: BffSession = await ensureFreshSession(
      session,
      config,
      store,
      "ref-2",
    );

    // Lock was attempted once and returned undefined; we re-read rather than
    // refresh a second time.
    expect(calls.withRefreshLock).toBe(1);
    expect(calls.read).toBe(1);
    expect(refreshTokens).not.toHaveBeenCalled();
    expect(result).toEqual(refreshedByPeer);
  });

  it("throws when the lock is held but the store no longer has the session", async () => {
    const config: BffConfig = makeConfig("https://refresh-gone.example.com");
    const session: BffSession = makeSession({
      refreshToken: "old-refresh",
      expiresAt: Date.now() - 1_000,
    });
    const { store }: FakeStore = makeFakeStore(null, { lockHeld: true });

    await expect(
      ensureFreshSession(session, config, store, "ref-3"),
    ).rejects.toThrow();
  });

  it("throws when the access token is expired and there is no refresh token", async () => {
    const config: BffConfig = makeConfig("https://fresh-no-refresh.example.com");
    const session: BffSession = makeSession({
      expiresAt: Date.now() - 1_000,
      refreshToken: undefined,
    });
    const { store, calls }: FakeStore = makeFakeStore(session);

    await expect(
      ensureFreshSession(session, config, store, "ref-4"),
    ).rejects.toThrow();
    // Fails before ever reaching the lock.
    expect(calls.withRefreshLock).toBe(0);
  });
});

/** Register the proxy as a catch-all so it receives the full `/api/...` path. */
function makeHandle(config: BffConfig): (request: Request) => Promise<Response> {
  const app: App = createApp();
  app.use(createApiProxy(config));
  return toWebHandler(app);
}

describe("createApiProxy", () => {
  it("returns 401 when there is no session cookie", async () => {
    const config: BffConfig = makeConfig("https://proxy-401.example.com");
    const handle = makeHandle(config);

    const res: Response = await handle(
      new Request("http://localhost/api/users"),
    );

    expect(res.status).toBe(401);
  });

  it("strips the /api prefix and forwards to the API with a Bearer token", async () => {
    const config: BffConfig = makeConfig("https://proxy-forward.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const session: BffSession = makeSession({
      accessToken: "the-access-token",
      expiresAt: Date.now() + 3_600_000,
    });
    const sealed: string = await sealSession(session, config.cookiePassword);

    const fetchMock: ReturnType<typeof vi.fn> = vi.fn(
      (input: unknown): Promise<Response> => {
        const requestUrl: string = String(input);
        if (requestUrl.includes(".well-known")) {
          return Promise.resolve(
            new Response(JSON.stringify(doc), {
              status: 200,
              headers: { "content-type": "application/json" },
            }),
          );
        }
        return Promise.resolve(
          new Response(JSON.stringify({ ok: true }), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const handle = makeHandle(config);

    const res: Response = await handle(
      new Request("http://localhost/api/users", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);

    const upstreamCall = fetchMock.mock.calls.find(
      (call): boolean => !String(call[0]).includes(".well-known"),
    );
    expect(upstreamCall).toBeDefined();
    const upstreamUrl: URL = new URL(String(upstreamCall?.[0]));
    expect(upstreamUrl.origin).toBe(config.apiBaseUrl);
    expect(upstreamUrl.pathname).toBe("/users");

    const init = upstreamCall?.[1] as RequestInit;
    const headers: Headers = new Headers(init.headers);
    expect(headers.get("authorization")).toBe("Bearer the-access-token");
  });

  it("silently refreshes an expired session, re-seals the cookie, and forwards the new token", async () => {
    const config: BffConfig = makeConfig("https://proxy-refresh.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const session: BffSession = makeSession({
      accessToken: "stale-access",
      refreshToken: "the-refresh-token",
      expiresAt: Date.now() - 1_000,
    });
    const sealed: string = await sealSession(session, config.cookiePassword);

    const fetchMock: ReturnType<typeof vi.fn> = vi.fn(
      (input: unknown): Promise<Response> => {
        const requestUrl: string = String(input);
        if (requestUrl.includes(".well-known")) {
          return Promise.resolve(
            new Response(JSON.stringify(doc), {
              status: 200,
              headers: { "content-type": "application/json" },
            }),
          );
        }
        if (requestUrl.includes("/connect/token")) {
          const tokens: TokenResponse = {
            access_token: "refreshed-access",
            refresh_token: "rotated-refresh",
            expires_in: 3600,
          };
          return Promise.resolve(
            new Response(JSON.stringify(tokens), {
              status: 200,
              headers: { "content-type": "application/json" },
            }),
          );
        }
        return Promise.resolve(
          new Response(JSON.stringify({ ok: true }), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
        );
      },
    );
    vi.stubGlobal("fetch", fetchMock);
    const handle = makeHandle(config);

    const res: Response = await handle(
      new Request("http://localhost/api/users", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    // The refreshed session is re-sealed back into the cookie.
    expect(res.headers.get("set-cookie") ?? "").toContain("wallow_bff=");

    const upstreamCall = fetchMock.mock.calls.find(
      (call): boolean =>
        !String(call[0]).includes(".well-known") &&
        !String(call[0]).includes("/connect/token"),
    );
    expect(upstreamCall).toBeDefined();
    const init = upstreamCall?.[1] as RequestInit;
    const headers: Headers = new Headers(init.headers);
    expect(headers.get("authorization")).toBe("Bearer refreshed-access");
  });
});
