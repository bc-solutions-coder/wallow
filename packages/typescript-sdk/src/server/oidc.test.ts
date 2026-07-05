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

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
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
  it("fetches the issuer's well-known OpenID configuration", async () => {
    // Unique issuer avoids the module-level discovery cache leaking across tests.
    const config: BffConfig = makeConfig({
      issuer: "https://discover-test.example.com",
    });
    const fetchMock: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async (): Promise<DiscoveryDoc> => doc,
    });
    vi.stubGlobal("fetch", fetchMock);

    const result: DiscoveryDoc = await discover(config);

    expect(fetchMock).toHaveBeenCalledWith(
      "https://discover-test.example.com/.well-known/openid-configuration",
    );
    expect(result).toEqual(doc);
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
