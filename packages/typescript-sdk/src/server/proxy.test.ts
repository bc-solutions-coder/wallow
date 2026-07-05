import { createApp, toWebHandler, type App } from "h3";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { BffConfig } from "./config";
import type { DiscoveryDoc, TokenResponse } from "./oidc";
import { createApiProxy, ensureFreshSession } from "./proxy";
import { sealSession, type BffSession } from "./session";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

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
    accessToken: "access-token-abc",
    refreshToken: "refresh-token-def",
    idToken: "header.payload.signature",
    expiresAt: Date.now() + 3_600_000,
    user: { sub: "user-123", email: "user@example.com", name: "Test User" },
    ...overrides,
  };
}

describe("ensureFreshSession", () => {
  it("returns the same session unchanged when the access token is still valid", async () => {
    const config: BffConfig = makeConfig("https://fresh-valid.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const session: BffSession = makeSession({
      expiresAt: Date.now() + 3_600_000,
    });
    // Fail loudly if a refresh network call is attempted.
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    const result = await ensureFreshSession(config, doc, session);

    expect(result.refreshed).toBe(false);
    expect(result.session).toEqual(session);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("refreshes the tokens when the access token has expired", async () => {
    const config: BffConfig = makeConfig("https://fresh-expired.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const session: BffSession = makeSession({
      accessToken: "old-access",
      refreshToken: "old-refresh",
      expiresAt: Date.now() - 1_000,
    });
    const tokens: TokenResponse = {
      access_token: "new-access",
      refresh_token: "new-refresh",
      id_token: "new-id",
      expires_in: 3600,
      token_type: "Bearer",
    };
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<TokenResponse> => tokens,
    });
    vi.stubGlobal("fetch", fetchMock);

    const result = await ensureFreshSession(config, doc, session);

    expect(result.refreshed).toBe(true);
    expect(result.session.accessToken).toBe("new-access");
    expect(result.session.refreshToken).toBe("new-refresh");
    expect(result.session.idToken).toBe("new-id");
    expect(result.session.expiresAt).toBeGreaterThan(Date.now());
    // User identity is preserved across refresh.
    expect(result.session.user).toEqual(session.user);
  });

  it("throws when the access token is expired and there is no refresh token", async () => {
    const config: BffConfig = makeConfig("https://fresh-no-refresh.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const session: BffSession = makeSession({
      expiresAt: Date.now() - 1_000,
      refreshToken: undefined,
    });

    await expect(ensureFreshSession(config, doc, session)).rejects.toThrow();
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
