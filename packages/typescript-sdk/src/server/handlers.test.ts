import {
  createApp,
  defineEventHandler,
  toWebHandler,
  type App,
  type H3Event,
} from "h3";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { BffConfig } from "./config";
import {
  createBffHandlers,
  readSession,
  writeSession,
  type BffHandlers,
} from "./handlers";
import type { DiscoveryDoc } from "./oidc";
import { sealSession, type BffSession } from "./session";
import { CookieSessionStore } from "./store/cookie";
import type { SessionStore } from "./store/types";
import { sealTx, type LoginTx } from "./txstate";

/**
 * Hermetic mock of openid-client: `discover()` now resolves endpoints through
 * openid-client's `discovery()` rather than the native `fetch`. The stub
 * reconstructs the same endpoint shape as {@link makeDoc} from the requested
 * metadata URL's origin, so these integration tests exercise real handler logic
 * without live network I/O. The token/userinfo grant helpers remain native-fetch
 * and keep using the per-test `fetch` stubs.
 */
const { authorizationCodeGrantMock } = vi.hoisted(() => ({
  authorizationCodeGrantMock: vi.fn(),
}));

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
  // Mirrors openid-client's buildAuthorizationUrl: reads the authorization
  // endpoint from the resolved Configuration's serverMetadata() and appends the
  // supplied query params, returning a URL.
  buildAuthorizationUrl: vi.fn(
    (
      configuration: { serverMetadata: () => Record<string, unknown> },
      params: Record<string, string>,
    ): URL => {
      const endpoint: string = configuration.serverMetadata()
        .authorization_endpoint as string;
      const url: URL = new URL(endpoint);
      for (const [key, value] of Object.entries(params)) {
        url.searchParams.set(key, value);
      }
      return url;
    },
  ),
  // Code exchange is delegated to openid-client so the callback gains id_token
  // signature/iss/aud/exp validation plus state + nonce checks. Configured
  // per-test via authorizationCodeGrantMock.mockResolvedValue(...).
  authorizationCodeGrant: authorizationCodeGrantMock,
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

/**
 * Build a config. Each test passes a unique issuer so the module-level
 * discovery cache in oidc.ts never leaks a stubbed doc across tests.
 */
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

/** Register all four handlers on a fresh app and return a web fetch handler. */
function makeHandle(handlers: BffHandlers): (request: Request) => Promise<Response> {
  const app: App = createApp();
  app.use("/bff/login", handlers.login);
  app.use("/bff/callback", handlers.callback);
  app.use("/bff/user", handlers.user);
  app.use("/bff/logout", handlers.logout);
  return toWebHandler(app);
}

/** A minimal, unsigned JWT with the given payload (BFF trusts the TLS channel). */
function makeIdToken(payload: Record<string, unknown>): string {
  const encoded: string = Buffer.from(JSON.stringify(payload)).toString(
    "base64url",
  );
  return `header.${encoded}.signature`;
}

function makeSession(overrides: Partial<BffSession> = {}): BffSession {
  return {
    sessionId: "sess-fixture-000",
    accessToken: "access-token-abc",
    refreshToken: "refresh-token-def",
    idToken: makeIdToken({ sub: "user-123" }),
    expiresAt: Date.now() + 3_600_000,
    user: { sub: "user-123", email: "user@example.com", name: "Test User" },
    version: 1,
    ...overrides,
  };
}

describe("login handler", () => {
  it("302s to the authorize URL with S256 PKCE and sets the tx cookie", async () => {
    const config: BffConfig = makeConfig("https://login-test.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async (): Promise<DiscoveryDoc> => doc,
      }),
    );
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/login?returnTo=/dashboard"),
    );

    expect(res.status).toBe(302);
    const location: string = res.headers.get("location") ?? "";
    expect(location.startsWith(doc.authorization_endpoint)).toBe(true);
    const url: URL = new URL(location);
    expect(url.searchParams.get("code_challenge_method")).toBe("S256");
    expect(url.searchParams.get("code_challenge")).toBeTruthy();
    expect(url.searchParams.get("state")).toBeTruthy();
    expect(url.searchParams.get("nonce")).toBeTruthy();
    expect(res.headers.get("set-cookie") ?? "").toContain("wallow_bff_tx");
  });
});

describe("callback handler", () => {
  it("returns 400 when there is no tx cookie", async () => {
    const config: BffConfig = makeConfig("https://cb-no-tx.example.com");
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/callback?code=abc&state=xyz"),
    );

    expect(res.status).toBe(400);
  });

  it("returns 400 when the state does not match the tx cookie", async () => {
    const config: BffConfig = makeConfig("https://cb-bad-state.example.com");
    const tx: LoginTx = {
      state: "expected-state",
      nonce: "nonce-1",
      verifier: "verifier-1",
      returnTo: "/home",
    };
    const sealed: string = await sealTx(tx, config.cookiePassword);
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/callback?code=abc&state=WRONG", {
        headers: { cookie: `wallow_bff_tx=${sealed}` },
      }),
    );

    expect(res.status).toBe(400);
  });

  it("exchanges the code via openid-client and 302s to returnTo on a valid callback", async () => {
    const config: BffConfig = makeConfig("https://cb-ok.example.com");
    const tx: LoginTx = {
      state: "st-1",
      nonce: "no-1",
      verifier: "ver-1",
      returnTo: "/welcome",
    };
    const sealed: string = await sealTx(tx, config.cookiePassword);

    // The exchange is delegated to openid-client's authorizationCodeGrant, not
    // a hand-rolled token POST — so no token-endpoint fetch is expected.
    authorizationCodeGrantMock.mockResolvedValue({
      access_token: "at",
      refresh_token: "rt",
      id_token: makeIdToken({ sub: "user-123", email: "u@e.com" }),
      expires_in: 3600,
      token_type: "Bearer",
    });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")),
    );
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/callback?code=code-123&state=st-1", {
        headers: { cookie: `wallow_bff_tx=${sealed}` },
      }),
    );

    expect(res.status).toBe(302);
    expect(res.headers.get("location")).toBe("/welcome");
    expect(res.headers.get("set-cookie") ?? "").toContain("wallow_bff=");

    // The callback delegates to openid-client, passing the full callback URL
    // (for code/state extraction) and the tx-bound state/nonce/PKCE checks.
    expect(authorizationCodeGrantMock).toHaveBeenCalledTimes(1);
    const [, currentUrl, checks] = authorizationCodeGrantMock.mock.calls[0] as [
      unknown,
      URL,
      { expectedState: string; expectedNonce: string; pkceCodeVerifier: string },
    ];
    expect(currentUrl).toBeInstanceOf(URL);
    expect(String(currentUrl)).toContain("code=code-123");
    expect(String(currentUrl)).toContain("state=st-1");
    expect(checks.expectedState).toBe("st-1");
    expect(checks.expectedNonce).toBe("no-1");
    expect(checks.pkceCodeVerifier).toBe("ver-1");
  });
});

describe("user handler", () => {
  it("returns 401 when there is no session cookie", async () => {
    const config: BffConfig = makeConfig("https://user-401.example.com");
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(new Request("http://localhost/bff/user"));

    expect(res.status).toBe(401);
  });

  it("returns 200 with the user identity when a session cookie is present", async () => {
    const config: BffConfig = makeConfig("https://user-200.example.com");
    const session: BffSession = makeSession();
    const sealed: string = await sealSession(session, config.cookiePassword);
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    const body: string = await res.text();
    expect(body).toContain(session.user.sub);
  });
});

/**
 * A {@link SessionStore} that delegates to a real {@link CookieSessionStore}
 * (so `read`/`write`/`withRefreshLock` behave normally) but records every `ref`
 * handed to `destroy`, letting a test assert that logout tears the session down.
 */
function makeRecordingStore(password: string): {
  store: SessionStore;
  destroyed: string[];
} {
  const delegate: CookieSessionStore = new CookieSessionStore({ password });
  const destroyed: string[] = [];
  const store: SessionStore = {
    read: (ref: string): Promise<BffSession | null> => delegate.read(ref),
    write: (session: BffSession): Promise<string> => delegate.write(session),
    destroy: async (ref: string): Promise<void> => {
      destroyed.push(ref);
      await delegate.destroy(ref);
    },
    withRefreshLock: <T>(
      ref: string,
      fn: () => Promise<T>,
    ): Promise<T | undefined> => delegate.withRefreshLock(ref, fn),
  };
  return { store, destroyed };
}

describe("createBffHandlers store injection", () => {
  it("defaults to a CookieSessionStore when no store is provided (back-compat)", async () => {
    const config: BffConfig = makeConfig("https://store-default.example.com");
    const session: BffSession = makeSession();
    const sealed: string = await sealSession(session, config.cookiePassword);
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    const body: string = await res.text();
    expect(body).toContain(session.user.sub);
  });

  it("threads an injected store through readSession in the user handler", async () => {
    const config: BffConfig = makeConfig("https://store-injected.example.com");
    const { store, destroyed } = makeRecordingStore(config.cookiePassword);
    const session: BffSession = makeSession();
    const sealed: string = await sealSession(session, config.cookiePassword);
    const handle = makeHandle(createBffHandlers(config, store));

    const res: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    const body: string = await res.text();
    expect(body).toContain(session.user.sub);
    // The user handler only reads; it must not destroy the session.
    expect(destroyed).toEqual([]);
  });
});

describe("logout handler", () => {
  it("destroys the current session ref in the injected store", async () => {
    const config: BffConfig = makeConfig("https://logout-destroy.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const { store, destroyed } = makeRecordingStore(config.cookiePassword);
    const session: BffSession = makeSession();
    // For a single-chunk cookie the sealed value is exactly the store ref.
    const sealed: string = await sealSession(session, config.cookiePassword);
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async (): Promise<DiscoveryDoc> => doc,
      }),
    );
    const handle = makeHandle(createBffHandlers(config, store));

    const res: Response = await handle(
      new Request("http://localhost/bff/logout", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(302);
    expect(destroyed).toEqual([sealed]);
  });

  it("clears the session cookie and 302s to the end-session endpoint", async () => {
    const config: BffConfig = makeConfig("https://logout-test.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const session: BffSession = makeSession();
    const sealed: string = await sealSession(session, config.cookiePassword);
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async (): Promise<DiscoveryDoc> => doc,
      }),
    );
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/logout", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(302);
    const location: string = res.headers.get("location") ?? "";
    expect(location.startsWith(doc.end_session_endpoint ?? "")).toBe(true);
    const url: URL = new URL(location);
    expect(url.searchParams.get("post_logout_redirect_uri")).toBe(
      config.postLogoutRedirectUri,
    );
    expect(url.searchParams.get("id_token_hint")).toBe(session.idToken);
    expect(res.headers.get("set-cookie") ?? "").toContain("wallow_bff=");
  });
});

/**
 * Rebuild a request `cookie` header from a response's `Set-Cookie` list, keeping
 * only cookies that carry a non-empty value (i.e. dropping the expired chunk
 * cookies that `writeSession` emits to clear a previously larger session).
 */
function cookieHeaderFrom(res: Response): string {
  return res.headers
    .getSetCookie()
    .map((cookie: string): string => cookie.split(";", 1)[0] ?? "")
    .filter((pair: string): boolean => {
      const eq: number = pair.indexOf("=");
      return eq > 0 && pair.slice(eq + 1) !== "";
    })
    .join("; ");
}

describe("readSession/writeSession store threading", () => {
  it("round-trips a session through an injected CookieSessionStore", async () => {
    const config: BffConfig = makeConfig("https://store-roundtrip.example.com");
    const store: SessionStore = new CookieSessionStore({
      password: config.cookiePassword,
    });
    const session: BffSession = makeSession();

    const app: App = createApp();
    app.use(
      "/write",
      defineEventHandler(async (event: H3Event): Promise<void> => {
        await writeSession(event, config, store, session);
      }),
    );
    app.use(
      "/read",
      defineEventHandler(
        async (event: H3Event): Promise<BffSession | null> =>
          readSession(event, config, store),
      ),
    );
    const handle: (request: Request) => Promise<Response> = toWebHandler(app);

    const writeRes: Response = await handle(
      new Request("http://localhost/write"),
    );
    const readRes: Response = await handle(
      new Request("http://localhost/read", {
        headers: { cookie: cookieHeaderFrom(writeRes) },
      }),
    );

    const restored: BffSession = (await readRes.json()) as BffSession;
    expect(restored).toEqual(session);
  });
});
