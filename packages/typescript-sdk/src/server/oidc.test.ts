import { afterEach, describe, expect, it, vi } from "vitest";

import type { BffConfig } from "./config";
import {
  buildAuthorizeUrl,
  discover,
  exchangeCode,
  refreshTokens,
  type DiscoveryDoc,
  type TokenResponse,
} from "./oidc";

/**
 * Hermetic mock of openid-client v6. `discovery()` performs real network I/O in
 * production, so it is replaced with a controllable stub whose returned
 * Configuration exposes `serverMetadata()` — the shape the new
 * openid-client-backed `discover()` reads endpoints from.
 */
const { discoveryMock, allowInsecureRequestsMock, makeConfiguration } =
  vi.hoisted(() => {
    const discoveryMock: ReturnType<typeof vi.fn> = vi.fn();
    const allowInsecureRequestsMock: ReturnType<typeof vi.fn> = vi.fn();
    const makeConfiguration = (
      metadata: Record<string, unknown>,
    ): { serverMetadata: () => Record<string, unknown> } => ({
      serverMetadata: (): Record<string, unknown> => metadata,
    });
    return { discoveryMock, allowInsecureRequestsMock, makeConfiguration };
  });

vi.mock("openid-client", () => ({
  discovery: discoveryMock,
  allowInsecureRequests: allowInsecureRequestsMock,
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

function makeConfig(overrides: Partial<BffConfig> = {}): BffConfig {
  return {
    issuer: "https://auth.example.com",
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

const doc: DiscoveryDoc = {
  authorization_endpoint: "https://auth.example.com/connect/authorize",
  token_endpoint: "https://auth.example.com/connect/token",
  end_session_endpoint: "https://auth.example.com/connect/logout",
};

describe("discover", () => {
  it("resolves endpoints through openid-client discovery and exposes the Configuration handle", async () => {
    // Unique issuer avoids the module-level discovery cache leaking across tests.
    const config: BffConfig = makeConfig({
      issuer: "https://discover-basic.example.com",
    });
    const configuration = makeConfiguration({
      issuer: "https://discover-basic.example.com",
      authorization_endpoint:
        "https://discover-basic.example.com/connect/authorize",
      token_endpoint: "https://discover-basic.example.com/connect/token",
      end_session_endpoint:
        "https://discover-basic.example.com/connect/logout",
      userinfo_endpoint: "https://discover-basic.example.com/connect/userinfo",
    });
    discoveryMock.mockResolvedValue(configuration);

    const result: DiscoveryDoc = await discover(config);

    // openid-client discovery() is invoked with a URL, the client id, and secret.
    expect(discoveryMock).toHaveBeenCalledTimes(1);
    const [url, clientId, clientSecret] = discoveryMock.mock.calls[0] as [
      URL,
      string,
      string,
    ];
    expect(url).toBeInstanceOf(URL);
    expect(url.href).toBe(
      "https://discover-basic.example.com/.well-known/openid-configuration",
    );
    expect(clientId).toBe(config.clientId);
    expect(clientSecret).toBe(config.clientSecret);

    expect(result.authorization_endpoint).toBe(
      "https://discover-basic.example.com/connect/authorize",
    );
    expect(result.token_endpoint).toBe(
      "https://discover-basic.example.com/connect/token",
    );
    expect(result.end_session_endpoint).toBe(
      "https://discover-basic.example.com/connect/logout",
    );
    expect(result.userinfo_endpoint).toBe(
      "https://discover-basic.example.com/connect/userinfo",
    );
    // The adapter carries a handle to the openid-client Configuration.
    expect(result.configuration).toBe(configuration);
  });

  it("caches by metadata URL — a second call does not re-run discovery", async () => {
    const config: BffConfig = makeConfig({
      issuer: "https://discover-cache.example.com",
    });
    const configuration = makeConfiguration({
      issuer: "https://discover-cache.example.com",
      authorization_endpoint:
        "https://discover-cache.example.com/connect/authorize",
      token_endpoint: "https://discover-cache.example.com/connect/token",
      end_session_endpoint:
        "https://discover-cache.example.com/connect/logout",
      userinfo_endpoint: "https://discover-cache.example.com/connect/userinfo",
    });
    discoveryMock.mockResolvedValue(configuration);

    const first: DiscoveryDoc = await discover(config);
    const second: DiscoveryDoc = await discover(config);

    expect(discoveryMock).toHaveBeenCalledTimes(1);
    expect(second).toBe(first);
  });

  it("re-pins browser-facing endpoints to the public issuer origin when metadataUrl is set", async () => {
    // Split-horizon: server discovers via an internal host; the metadata
    // advertises every endpoint on that internal origin.
    const config: BffConfig = makeConfig({
      issuer: "https://public.example.com",
      metadataUrl:
        "https://internal.svc.local/.well-known/openid-configuration",
    });
    const configuration = makeConfiguration({
      issuer: "https://internal.svc.local",
      authorization_endpoint:
        "https://internal.svc.local/connect/authorize",
      token_endpoint: "https://internal.svc.local/connect/token",
      end_session_endpoint: "https://internal.svc.local/connect/logout",
      userinfo_endpoint: "https://internal.svc.local/connect/userinfo",
    });
    discoveryMock.mockResolvedValue(configuration);

    const result: DiscoveryDoc = await discover(config);

    // discovery() is called with the configured metadata URL.
    const [url] = discoveryMock.mock.calls[0] as [URL, string, string];
    expect(url.href).toBe(
      "https://internal.svc.local/.well-known/openid-configuration",
    );

    // Browser-facing endpoints are re-pinned to the public issuer origin.
    expect(result.authorization_endpoint).toBe(
      "https://public.example.com/connect/authorize",
    );
    expect(result.end_session_endpoint).toBe(
      "https://public.example.com/connect/logout",
    );
    // Backchannel endpoints stay exactly as advertised (server-reachable).
    expect(result.token_endpoint).toBe(
      "https://internal.svc.local/connect/token",
    );
    expect(result.userinfo_endpoint).toBe(
      "https://internal.svc.local/connect/userinfo",
    );
  });

  it("uses endpoints as advertised when metadataUrl is not set", async () => {
    const config: BffConfig = makeConfig({
      issuer: "https://discover-nopin.example.com",
    });
    const configuration = makeConfiguration({
      issuer: "https://discover-nopin.example.com",
      authorization_endpoint:
        "https://discover-nopin.example.com/connect/authorize",
      token_endpoint: "https://discover-nopin.example.com/connect/token",
      end_session_endpoint:
        "https://discover-nopin.example.com/connect/logout",
      userinfo_endpoint:
        "https://discover-nopin.example.com/connect/userinfo",
    });
    discoveryMock.mockResolvedValue(configuration);

    const result: DiscoveryDoc = await discover(config);

    expect(result.authorization_endpoint).toBe(
      "https://discover-nopin.example.com/connect/authorize",
    );
    expect(result.end_session_endpoint).toBe(
      "https://discover-nopin.example.com/connect/logout",
    );
  });
});

describe("buildAuthorizeUrl", () => {
  it("includes PKCE challenge, state, nonce, and scopes", () => {
    const config: BffConfig = makeConfig();

    const url: URL = new URL(
      buildAuthorizeUrl(config, doc, {
        state: "state-123",
        codeChallenge: "challenge-abc",
        nonce: "nonce-xyz",
      }),
    );

    expect(`${url.origin}${url.pathname}`).toBe(doc.authorization_endpoint);
    const params: URLSearchParams = url.searchParams;
    expect(params.get("response_type")).toBe("code");
    expect(params.get("client_id")).toBe(config.clientId);
    expect(params.get("redirect_uri")).toBe(config.redirectUri);
    expect(params.get("scope")).toBe("openid profile email offline_access");
    expect(params.get("state")).toBe("state-123");
    expect(params.get("code_challenge")).toBe("challenge-abc");
    expect(params.get("code_challenge_method")).toBe("S256");
    expect(params.get("nonce")).toBe("nonce-xyz");
  });
});

describe("exchangeCode", () => {
  it("posts the authorization code and PKCE verifier to the token endpoint", async () => {
    const config: BffConfig = makeConfig();
    const tokens: TokenResponse = {
      access_token: "at",
      refresh_token: "rt",
      id_token: "it",
      expires_in: 3600,
      token_type: "Bearer",
    };
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<TokenResponse> => tokens,
    });
    vi.stubGlobal("fetch", fetchMock);

    const result: TokenResponse = await exchangeCode(config, doc, {
      code: "auth-code",
      codeVerifier: "verifier-123",
    });

    expect(result).toEqual(tokens);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(doc.token_endpoint);
    expect(init.method).toBe("POST");
    const headers: Record<string, string> = init.headers as Record<
      string,
      string
    >;
    expect(headers["Content-Type"]).toBe("application/x-www-form-urlencoded");
    const body: URLSearchParams = new URLSearchParams(init.body as string);
    expect(body.get("grant_type")).toBe("authorization_code");
    expect(body.get("code")).toBe("auth-code");
    expect(body.get("code_verifier")).toBe("verifier-123");
    expect(body.get("redirect_uri")).toBe(config.redirectUri);
    expect(body.get("client_id")).toBe(config.clientId);
    expect(body.get("client_secret")).toBe(config.clientSecret);
  });

  it("throws when the token endpoint returns a non-2xx response", async () => {
    const config: BffConfig = makeConfig();
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async (): Promise<unknown> => ({ error: "invalid_grant" }),
      text: async (): Promise<string> => '{"error":"invalid_grant"}',
    });
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      exchangeCode(config, doc, { code: "bad", codeVerifier: "v" }),
    ).rejects.toThrow();
  });
});

describe("refreshTokens", () => {
  it("posts a refresh_token grant to the token endpoint", async () => {
    const config: BffConfig = makeConfig();
    const tokens: TokenResponse = {
      access_token: "at2",
      expires_in: 3600,
    };
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<TokenResponse> => tokens,
    });
    vi.stubGlobal("fetch", fetchMock);

    const result: TokenResponse = await refreshTokens(
      config,
      doc,
      "refresh-123",
    );

    expect(result).toEqual(tokens);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(doc.token_endpoint);
    expect(init.method).toBe("POST");
    const body: URLSearchParams = new URLSearchParams(init.body as string);
    expect(body.get("grant_type")).toBe("refresh_token");
    expect(body.get("refresh_token")).toBe("refresh-123");
    expect(body.get("client_id")).toBe(config.clientId);
    expect(body.get("client_secret")).toBe(config.clientSecret);
  });
});
