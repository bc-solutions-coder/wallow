import { createApp, toWebHandler, type App } from "h3";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { BffConfig } from "./config";
import { WallowError } from "./errors";
import { discover, refreshTokens } from "./oidc";
import type { DiscoveryDoc, TokenResponse } from "./oidc";
import {
  createApiProxy,
  ensureFreshSession,
  forceRefreshSession,
  forwardWithResilience,
  FORWARD_TIMEOUT_MS,
  MAX_RETRY_AFTER_MS,
  NETWORK_ERROR_CODE,
  NETWORK_TIMEOUT_CODE,
  type ForwardRequest,
  type ForwardResult,
} from "./proxy";
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
  vi.useRealTimers();
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
    const config: BffConfig = makeConfig(
      "https://fresh-no-refresh.example.com",
    );
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
function makeHandle(
  config: BffConfig,
  store?: SessionStore,
): (request: Request) => Promise<Response> {
  const app: App = createApp();
  app.use(
    store === undefined
      ? createApiProxy(config)
      : createApiProxy(config, store),
  );
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

/** A single scripted upstream attempt. */
type FetchAttempt = (input: unknown, init: RequestInit) => Promise<Response>;

/**
 * Stub `fetch` with a script: attempt N is served by `attempts[N]`. A call past
 * the end of the script rejects, so "retried more times than allowed" fails the
 * test instead of hanging.
 */
function stubFetchScript(
  attempts: readonly FetchAttempt[],
): ReturnType<typeof vi.fn> {
  let attempt: number = 0;
  const fetchMock: ReturnType<typeof vi.fn> = vi.fn(
    (input: unknown, init: RequestInit): Promise<Response> => {
      const handler: FetchAttempt | undefined = attempts[attempt];
      attempt += 1;
      if (handler === undefined) {
        return Promise.reject(
          new Error(`unexpected upstream attempt #${attempt}`),
        );
      }
      return handler(input, init);
    },
  );
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

/** An attempt that always answers with the same response. */
function respond(factory: () => Response): FetchAttempt {
  return (): Promise<Response> => Promise.resolve(factory());
}

/** RFC 7807 body as the .NET API emits it (machine code under `extensions`). */
function problem(
  status: number,
  body: Record<string, unknown>,
  headers: Record<string, string> = {},
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/problem+json", ...headers },
  });
}

/** The auth-cookie redirect the .NET API emits when the bearer is rejected. */
function loginRedirect(): Response {
  return new Response(null, {
    status: 302,
    headers: {
      location: "https://api.example.com/Account/Login?ReturnUrl=%2Fusers",
    },
  });
}

/** The `authorization` header a scripted attempt was called with. */
function bearerOf(
  fetchMock: ReturnType<typeof vi.fn>,
  attempt: number,
): string | null {
  const init = fetchMock.mock.calls[attempt]?.[1] as RequestInit | undefined;
  return new Headers(init?.headers).get("authorization");
}

/** The `redirect` mode a scripted attempt was called with. */
function redirectModeOf(
  fetchMock: ReturnType<typeof vi.fn>,
  attempt: number,
): RequestRedirect | undefined {
  const init = fetchMock.mock.calls[attempt]?.[1] as RequestInit | undefined;
  return init?.redirect;
}

/** The request forwardWithResilience replays across attempts. */
function makeForwardRequest(
  overrides: Partial<ForwardRequest> = {},
): ForwardRequest {
  return {
    target: "https://api.example.com/users",
    method: "GET",
    headers: new Headers({ accept: "application/json" }),
    ...overrides,
  };
}

/** Rotated tokens returned by the forced refresh in the reactive-401 tests. */
const ROTATED_TOKENS: TokenResponse = {
  access_token: "reactive-access",
  refresh_token: "reactive-refresh",
  expires_in: 3600,
  token_type: "Bearer",
};

/** Wire up `discover`/`refreshTokens` so a forced refresh rotates the tokens. */
function stubRefreshGrant(config: BffConfig): void {
  vi.mocked(discover).mockResolvedValue(makeDoc(config.issuer));
  vi.mocked(refreshTokens).mockResolvedValue(ROTATED_TOKENS);
}

describe("forceRefreshSession", () => {
  it("refreshes under the lock even when the access token is not near expiry", async () => {
    const config: BffConfig = makeConfig("https://force-refresh.example.com");
    const session: BffSession = makeSession({
      accessToken: "believed-fresh",
      refreshToken: "the-refresh-token",
      expiresAt: Date.now() + 3_600_000,
      version: 7,
    });
    const { store, calls }: FakeStore = makeFakeStore(session);
    stubRefreshGrant(config);

    const result: BffSession = await forceRefreshSession(
      session,
      config,
      store,
      "ref-force",
    );

    // The local expiry says "fresh"; the reactive path refreshes anyway.
    expect(refreshTokens).toHaveBeenCalledTimes(1);
    expect(calls.withRefreshLock).toBe(1);
    expect(result.accessToken).toBe("reactive-access");
    expect(result.refreshToken).toBe("reactive-refresh");
    expect(result.version).toBe(8);
    expect(calls.write.length).toBe(1);
    expect(calls.write[0].accessToken).toBe("reactive-access");
  });

  it("throws when there is no refresh token to force a refresh with", async () => {
    const config: BffConfig = makeConfig(
      "https://force-no-refresh.example.com",
    );
    const session: BffSession = makeSession({ refreshToken: undefined });
    const { store, calls }: FakeStore = makeFakeStore(session);

    await expect(
      forceRefreshSession(session, config, store, "ref-force-none"),
    ).rejects.toThrow(/refresh token/i);
    // Fails before ever reaching the lock.
    expect(calls.withRefreshLock).toBe(0);
  });
});

describe("forwardWithResilience", () => {
  it("forwards once with redirect:manual, an abort signal, and the session bearer", async () => {
    const config: BffConfig = makeConfig("https://forward-ok.example.com");
    const session: BffSession = makeSession({
      accessToken: "the-access-token",
    });
    const { store, calls }: FakeStore = makeFakeStore(session);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond(
        (): Response =>
          new Response(JSON.stringify({ ok: true }), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
      ),
    ]);

    const result: ForwardResult = await forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-ok",
    );

    expect(result.response.status).toBe(200);
    expect(result.session).toEqual(session);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(bearerOf(fetchMock, 0)).toBe("Bearer the-access-token");
    // Manual redirect is what makes a login-page 3xx observable at all.
    expect(redirectModeOf(fetchMock, 0)).toBe("manual");
    const init = fetchMock.mock.calls[0]?.[1] as RequestInit;
    expect(init.signal).toBeInstanceOf(AbortSignal);
    // A healthy forward never refreshes.
    expect(calls.withRefreshLock).toBe(0);
    expect(refreshTokens).not.toHaveBeenCalled();
  });

  it("forces a refresh under the lock and replays the request once on a reactive 401", async () => {
    const config: BffConfig = makeConfig("https://forward-401.example.com");
    const session: BffSession = makeSession({
      accessToken: "rejected-access",
      refreshToken: "the-refresh-token",
      expiresAt: Date.now() + 3_600_000,
      version: 2,
    });
    const { store, calls }: FakeStore = makeFakeStore(session);
    stubRefreshGrant(config);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response => problem(401, { title: "Unauthorized" })),
      respond(
        (): Response =>
          new Response(JSON.stringify({ ok: true }), { status: 200 }),
      ),
    ]);

    const result: ForwardResult = await forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-401",
    );

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(bearerOf(fetchMock, 0)).toBe("Bearer rejected-access");
    // The replay carries the token the forced refresh produced.
    expect(bearerOf(fetchMock, 1)).toBe("Bearer reactive-access");
    expect(calls.withRefreshLock).toBe(1);
    expect(result.response.status).toBe(200);
    // The caller needs the rotated session back to re-seal the cookie.
    expect(result.session.accessToken).toBe("reactive-access");
    expect(result.session.version).toBe(3);
  });

  it("surfaces a WallowError when the replayed request is rejected with a second 401", async () => {
    const config: BffConfig = makeConfig(
      "https://forward-401-twice.example.com",
    );
    const session: BffSession = makeSession({
      accessToken: "rejected-access",
      refreshToken: "the-refresh-token",
    });
    const { store }: FakeStore = makeFakeStore(session);
    stubRefreshGrant(config);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response => problem(401, { title: "Unauthorized" })),
      respond((): Response =>
        problem(401, {
          title: "Unauthorized",
          detail: "The access token is not valid.",
          extensions: { code: "TOKEN_REJECTED" },
        }),
      ),
    ]);

    const error: WallowError = await forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-401-twice",
    ).then(
      (): never => {
        throw new Error("expected forwardWithResilience to reject");
      },
      (thrown: unknown): WallowError => thrown as WallowError,
    );

    expect(error).toBeInstanceOf(WallowError);
    expect(error.status).toBe(401);
    expect(error.code).toBe("TOKEN_REJECTED");
    expect(error.detail).toBe("The access token is not valid.");
    // Exactly one retry: no second refresh, no third attempt.
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(refreshTokens).toHaveBeenCalledTimes(1);
  });

  it("does not retry a 401 when the session has no refresh token", async () => {
    const config: BffConfig = makeConfig(
      "https://forward-401-norefresh.example.com",
    );
    const session: BffSession = makeSession({ refreshToken: undefined });
    const { store }: FakeStore = makeFakeStore(session);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response => problem(401, { title: "Unauthorized" })),
    ]);

    await expect(
      forwardWithResilience(
        makeForwardRequest(),
        config,
        store,
        session,
        "ref-401-norefresh",
      ),
    ).rejects.toBeInstanceOf(WallowError);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(refreshTokens).not.toHaveBeenCalled();
  });

  it("treats a 3xx redirect to the login page as an auth failure and replays once", async () => {
    const config: BffConfig = makeConfig(
      "https://forward-login-redirect.example.com",
    );
    const session: BffSession = makeSession({
      accessToken: "rejected-access",
      refreshToken: "the-refresh-token",
      version: 5,
    });
    const { store, calls }: FakeStore = makeFakeStore(session);
    stubRefreshGrant(config);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond(loginRedirect),
      respond(
        (): Response =>
          new Response(JSON.stringify({ ok: true }), { status: 200 }),
      ),
    ]);

    const result: ForwardResult = await forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-redirect",
    );

    // The redirect is only visible because the forward opted out of following it.
    expect(redirectModeOf(fetchMock, 0)).toBe("manual");
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(bearerOf(fetchMock, 1)).toBe("Bearer reactive-access");
    expect(calls.withRefreshLock).toBe(1);
    expect(result.response.status).toBe(200);
    expect(result.session.version).toBe(6);
  });

  it("surfaces a WallowError when the replayed request is redirected to the login page again", async () => {
    const config: BffConfig = makeConfig(
      "https://forward-redirect-twice.example.com",
    );
    const session: BffSession = makeSession({
      refreshToken: "the-refresh-token",
    });
    const { store }: FakeStore = makeFakeStore(session);
    stubRefreshGrant(config);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond(loginRedirect),
      respond(loginRedirect),
    ]);

    const error: WallowError = await forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-redirect-twice",
    ).then(
      (): never => {
        throw new Error("expected forwardWithResilience to reject");
      },
      (thrown: unknown): WallowError => thrown as WallowError,
    );

    expect(error).toBeInstanceOf(WallowError);
    // A login redirect is an authentication failure, whatever status it wears.
    expect(error.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(refreshTokens).toHaveBeenCalledTimes(1);
  });

  it("does not treat a non-login 3xx as an auth failure", async () => {
    const config: BffConfig = makeConfig(
      "https://forward-redirect-other.example.com",
    );
    const session: BffSession = makeSession({
      refreshToken: "the-refresh-token",
    });
    const { store, calls }: FakeStore = makeFakeStore(session);
    stubRefreshGrant(config);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond(
        (): Response =>
          new Response(null, {
            status: 302,
            headers: { location: "https://api.example.com/users/42" },
          }),
      ),
    ]);

    const result: ForwardResult = await forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-redirect-other",
    );

    // A plain redirect is the API's business: hand it back, do not refresh.
    expect(result.response.status).toBe(302);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(calls.withRefreshLock).toBe(0);
    expect(refreshTokens).not.toHaveBeenCalled();
  });

  it("waits for Retry-After and replays the request once on a 429", async () => {
    vi.useFakeTimers();
    const config: BffConfig = makeConfig("https://forward-429.example.com");
    const session: BffSession = makeSession();
    const { store, calls }: FakeStore = makeFakeStore(session);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response =>
        problem(429, { title: "Too Many Requests" }, { "retry-after": "1" }),
      ),
      respond(
        (): Response =>
          new Response(JSON.stringify({ ok: true }), { status: 200 }),
      ),
    ]);

    const pending: Promise<ForwardResult> = forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-429",
    );
    // Attach a handler up front: driving the clock below must not let a failing
    // forward surface as an unhandled rejection before the `await` picks it up.
    void pending.catch((): void => undefined);

    // Still waiting out Retry-After: the replay has not gone out yet.
    await vi.advanceTimersByTimeAsync(0);
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1_000);
    const result: ForwardResult = await pending;

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(result.response.status).toBe(200);
    // Throttling is not an auth failure.
    expect(calls.withRefreshLock).toBe(0);
  });

  it("bounds the Retry-After wait to MAX_RETRY_AFTER_MS", async () => {
    vi.useFakeTimers();
    const config: BffConfig = makeConfig(
      "https://forward-429-bounded.example.com",
    );
    const session: BffSession = makeSession();
    const { store }: FakeStore = makeFakeStore(session);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response =>
        problem(429, { title: "Too Many Requests" }, { "retry-after": "3600" }),
      ),
      respond(
        (): Response =>
          new Response(JSON.stringify({ ok: true }), { status: 200 }),
      ),
    ]);

    const pending: Promise<ForwardResult> = forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-429-bounded",
    );
    void pending.catch((): void => undefined);

    // An hour-long Retry-After must not park the request for an hour.
    await vi.advanceTimersByTimeAsync(MAX_RETRY_AFTER_MS);
    const result: ForwardResult = await pending;

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(result.response.status).toBe(200);
  });

  it("surfaces a WallowError when the replayed request is throttled again", async () => {
    vi.useFakeTimers();
    const config: BffConfig = makeConfig(
      "https://forward-429-twice.example.com",
    );
    const session: BffSession = makeSession();
    const { store }: FakeStore = makeFakeStore(session);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response =>
        problem(429, { title: "Too Many Requests" }, { "retry-after": "1" }),
      ),
      respond((): Response =>
        problem(
          429,
          {
            title: "Too Many Requests",
            detail: "Rate limit exceeded.",
            extensions: { code: "RATE_LIMITED" },
          },
          { "retry-after": "1" },
        ),
      ),
    ]);

    const pending: Promise<ForwardResult> = forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-429-twice",
    );
    const settled: Promise<WallowError> = pending.then(
      (): never => {
        throw new Error("expected forwardWithResilience to reject");
      },
      (thrown: unknown): WallowError => thrown as WallowError,
    );

    await vi.advanceTimersByTimeAsync(MAX_RETRY_AFTER_MS);
    const error: WallowError = await settled;

    expect(error).toBeInstanceOf(WallowError);
    expect(error.status).toBe(429);
    expect(error.code).toBe("RATE_LIMITED");
    // Exactly one retry: the second 429 is not waited out again.
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("raises a 503 NETWORK_ERROR WallowError when the transport fails", async () => {
    const config: BffConfig = makeConfig("https://forward-network.example.com");
    const session: BffSession = makeSession();
    const { store }: FakeStore = makeFakeStore(session);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      (): Promise<Response> =>
        Promise.reject(new TypeError("fetch failed: ECONNREFUSED")),
    ]);

    const error: WallowError = await forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-network",
    ).then(
      (): never => {
        throw new Error("expected forwardWithResilience to reject");
      },
      (thrown: unknown): WallowError => thrown as WallowError,
    );

    expect(error).toBeInstanceOf(WallowError);
    expect(error.status).toBe(503);
    expect(error.code).toBe(NETWORK_ERROR_CODE);
    // A dead socket is not retried.
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("raises a 503 NETWORK_TIMEOUT WallowError when the forward exceeds FORWARD_TIMEOUT_MS", async () => {
    vi.useFakeTimers();
    const config: BffConfig = makeConfig("https://forward-timeout.example.com");
    const session: BffSession = makeSession();
    const { store }: FakeStore = makeFakeStore(session);
    // A hung upstream: the attempt only settles when its abort signal fires.
    stubFetchScript([
      (_input: unknown, init: RequestInit): Promise<Response> =>
        new Promise<Response>((_resolve, reject): void => {
          const signal: AbortSignal | null | undefined = init.signal;
          signal?.addEventListener("abort", (): void => {
            const aborted: Error = new Error("The operation was aborted.");
            aborted.name = "AbortError";
            reject(aborted);
          });
        }),
    ]);

    const settled: Promise<WallowError> = forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-timeout",
    ).then(
      (): never => {
        throw new Error("expected forwardWithResilience to reject");
      },
      (thrown: unknown): WallowError => thrown as WallowError,
    );

    await vi.advanceTimersByTimeAsync(FORWARD_TIMEOUT_MS);
    const error: WallowError = await settled;

    expect(error).toBeInstanceOf(WallowError);
    expect(error.status).toBe(503);
    // A timeout is distinguishable from any other transport failure.
    expect(error.code).toBe(NETWORK_TIMEOUT_CODE);
  });

  it("raises a WallowError carrying the upstream problem details for a non-OK response", async () => {
    const config: BffConfig = makeConfig("https://forward-problem.example.com");
    const session: BffSession = makeSession();
    const { store }: FakeStore = makeFakeStore(session);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response =>
        problem(409, {
          type: "https://httpstatuses.io/409",
          title: "Conflict",
          status: 409,
          detail: "Tenant slug already taken.",
          extensions: { code: "TENANT_SLUG_TAKEN" },
        }),
      ),
    ]);

    const error: WallowError = await forwardWithResilience(
      makeForwardRequest({ method: "POST", body: '{"slug":"acme"}' }),
      config,
      store,
      session,
      "ref-problem",
    ).then(
      (): never => {
        throw new Error("expected forwardWithResilience to reject");
      },
      (thrown: unknown): WallowError => thrown as WallowError,
    );

    expect(error).toBeInstanceOf(WallowError);
    // The upstream status survives the trip through the BFF.
    expect(error.status).toBe(409);
    expect(error.code).toBe("TENANT_SLUG_TAKEN");
    expect(error.title).toBe("Conflict");
    expect(error.detail).toBe("Tenant slug already taken.");
    // A 409 is deterministic: retrying it would just conflict again.
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("raises an UNKNOWN-coded WallowError when a non-OK response is not problem details", async () => {
    const config: BffConfig = makeConfig("https://forward-html.example.com");
    const session: BffSession = makeSession();
    const { store }: FakeStore = makeFakeStore(session);
    stubFetchScript([
      respond(
        (): Response =>
          new Response("<html><body>Server Error</body></html>", {
            status: 500,
            headers: { "content-type": "text/html" },
          }),
      ),
    ]);

    const error: WallowError = await forwardWithResilience(
      makeForwardRequest(),
      config,
      store,
      session,
      "ref-html",
    ).then(
      (): never => {
        throw new Error("expected forwardWithResilience to reject");
      },
      (thrown: unknown): WallowError => thrown as WallowError,
    );

    expect(error).toBeInstanceOf(WallowError);
    expect(error.status).toBe(500);
    expect(error.code).toBe("UNKNOWN");
  });

  it("replays the request body on a retried attempt", async () => {
    const config: BffConfig = makeConfig(
      "https://forward-replay-body.example.com",
    );
    const session: BffSession = makeSession({
      refreshToken: "the-refresh-token",
    });
    const { store }: FakeStore = makeFakeStore(session);
    stubRefreshGrant(config);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response => problem(401, { title: "Unauthorized" })),
      respond(
        (): Response =>
          new Response(JSON.stringify({ ok: true }), { status: 201 }),
      ),
    ]);

    const result: ForwardResult = await forwardWithResilience(
      makeForwardRequest({ method: "POST", body: '{"slug":"acme"}' }),
      config,
      store,
      session,
      "ref-replay-body",
    );

    expect(result.response.status).toBe(201);
    const replay = fetchMock.mock.calls[1]?.[1] as RequestInit;
    expect(replay.method).toBe("POST");
    // A retry that drops the body would silently POST nothing.
    expect(replay.body).toBe('{"slug":"acme"}');
  });
});

describe("createApiProxy resilience", () => {
  it("recovers from a reactive 401 and re-seals the rotated session into the cookie", async () => {
    const config: BffConfig = makeConfig(
      "https://proxy-reactive-401.example.com",
    );
    const session: BffSession = makeSession({
      accessToken: "rejected-access",
      refreshToken: "the-refresh-token",
      expiresAt: Date.now() + 3_600_000,
    });
    const { store, calls }: FakeStore = makeFakeStore(session);
    stubRefreshGrant(config);
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchScript([
      respond((): Response => problem(401, { title: "Unauthorized" })),
      respond(
        (): Response =>
          new Response(JSON.stringify({ ok: true }), {
            status: 200,
            headers: { "content-type": "application/json" },
          }),
      ),
    ]);
    const handle = makeHandle(config, store);

    const res: Response = await handle(
      new Request("http://localhost/api/users", {
        headers: { cookie: `${config.cookieName}=fake-ref` },
      }),
    );

    expect(res.status).toBe(200);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(bearerOf(fetchMock, 1)).toBe("Bearer reactive-access");
    // The rotated session must reach the browser, or the next request 401s again.
    expect(calls.write.length).toBe(1);
    expect(res.headers.get("set-cookie") ?? "").toContain(
      `${config.cookieName}=`,
    );
  });

  it("answers 503 when the downstream API is unreachable", async () => {
    const config: BffConfig = makeConfig(
      "https://proxy-unreachable.example.com",
    );
    const session: BffSession = makeSession({
      expiresAt: Date.now() + 3_600_000,
    });
    const { store }: FakeStore = makeFakeStore(session);
    stubFetchScript([
      (): Promise<Response> =>
        Promise.reject(new TypeError("fetch failed: ECONNREFUSED")),
    ]);
    const handle = makeHandle(config, store);

    const res: Response = await handle(
      new Request("http://localhost/api/users", {
        headers: { cookie: `${config.cookieName}=fake-ref` },
      }),
    );

    // A dead API is a 503, not an unhandled 500 from the BFF.
    expect(res.status).toBe(503);
  });

  it("preserves the upstream status and problem details of a non-OK response", async () => {
    const config: BffConfig = makeConfig("https://proxy-problem.example.com");
    const session: BffSession = makeSession({
      expiresAt: Date.now() + 3_600_000,
    });
    const { store }: FakeStore = makeFakeStore(session);
    stubFetchScript([
      respond((): Response =>
        problem(409, {
          title: "Conflict",
          status: 409,
          detail: "Tenant slug already taken.",
          extensions: { code: "TENANT_SLUG_TAKEN" },
        }),
      ),
    ]);
    const handle = makeHandle(config, store);

    const res: Response = await handle(
      new Request("http://localhost/api/tenants", {
        method: "POST",
        headers: {
          cookie: `${config.cookieName}=fake-ref`,
          "content-type": "application/json",
        },
        body: '{"slug":"acme"}',
      }),
    );

    expect(res.status).toBe(409);
    expect(res.headers.get("content-type") ?? "").toContain("problem+json");
    const body = (await res.json()) as Record<string, unknown>;
    expect(body["title"]).toBe("Conflict");
    expect(body["detail"]).toBe("Tenant slug already taken.");
  });
});
