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
const {
  discoveryMock,
  allowInsecureRequestsMock,
  buildAuthorizationUrlMock,
  authorizationCodeGrantMock,
  makeConfiguration,
} = vi.hoisted(() => {
  const discoveryMock: ReturnType<typeof vi.fn> = vi.fn();
  const allowInsecureRequestsMock: ReturnType<typeof vi.fn> = vi.fn();
  const buildAuthorizationUrlMock: ReturnType<typeof vi.fn> = vi.fn();
  const authorizationCodeGrantMock: ReturnType<typeof vi.fn> = vi.fn();
  const makeConfiguration = (
    metadata: Record<string, unknown>,
  ): { serverMetadata: () => Record<string, unknown> } => ({
    serverMetadata: (): Record<string, unknown> => metadata,
  });
  return {
    discoveryMock,
    allowInsecureRequestsMock,
    buildAuthorizationUrlMock,
    authorizationCodeGrantMock,
    makeConfiguration,
  };
});

vi.mock("openid-client", () => ({
  discovery: discoveryMock,
  allowInsecureRequests: allowInsecureRequestsMock,
  buildAuthorizationUrl: buildAuthorizationUrlMock,
  authorizationCodeGrant: authorizationCodeGrantMock,
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
  /**
   * Build a DiscoveryDoc carrying a resolved openid-client Configuration handle,
   * as {@link discover} would populate it. The Configuration is opaque to
   * buildAuthorizeUrl — it is passed straight through to
   * openid-client's buildAuthorizationUrl — so a stub object suffices.
   */
  function makeAuthorizeDoc(): DiscoveryDoc {
    const configuration = makeConfiguration({
      issuer: "https://auth.example.com",
      authorization_endpoint: doc.authorization_endpoint,
    });
    return {
      ...doc,
      configuration: configuration as unknown as DiscoveryDoc["configuration"],
    };
  }

  it("delegates to openid-client buildAuthorizationUrl with the resolved Configuration", () => {
    const config: BffConfig = makeConfig();
    const authorizeDoc: DiscoveryDoc = makeAuthorizeDoc();
    const built: URL = new URL(
      `${doc.authorization_endpoint}?response_type=code&client_id=web-bff`,
    );
    buildAuthorizationUrlMock.mockReturnValue(built);

    const url: string = buildAuthorizeUrl(config, authorizeDoc, {
      state: "state-123",
      codeChallenge: "challenge-abc",
      nonce: "nonce-xyz",
    });

    // Returns the string form of the openid-client-built URL.
    expect(url).toBe(built.toString());

    // Delegates once, passing the resolved Configuration handle from the doc.
    expect(buildAuthorizationUrlMock).toHaveBeenCalledTimes(1);
    const [passedConfig] = buildAuthorizationUrlMock.mock.calls[0] as [
      unknown,
      Record<string, string>,
    ];
    expect(passedConfig).toBe(authorizeDoc.configuration);
  });

  it("passes PKCE (S256), state, nonce, and scopes as authorization params", () => {
    const config: BffConfig = makeConfig();
    const authorizeDoc: DiscoveryDoc = makeAuthorizeDoc();
    buildAuthorizationUrlMock.mockReturnValue(
      new URL(doc.authorization_endpoint),
    );

    buildAuthorizeUrl(config, authorizeDoc, {
      state: "state-123",
      codeChallenge: "challenge-abc",
      nonce: "nonce-xyz",
    });

    const [, params] = buildAuthorizationUrlMock.mock.calls[0] as [
      unknown,
      Record<string, string>,
    ];
    expect(params.response_type).toBe("code");
    expect(params.client_id).toBe(config.clientId);
    expect(params.redirect_uri).toBe(config.redirectUri);
    expect(params.scope).toBe("openid profile email offline_access");
    expect(params.state).toBe("state-123");
    expect(params.code_challenge).toBe("challenge-abc");
    expect(params.code_challenge_method).toBe("S256");
    expect(params.nonce).toBe("nonce-xyz");
  });
});

describe("exchangeCode", () => {
  /**
   * Build a DiscoveryDoc carrying a resolved openid-client Configuration handle,
   * as {@link discover} would populate it. The Configuration is opaque to
   * exchangeCode — it is passed straight through to openid-client's
   * authorizationCodeGrant — so a stub object suffices.
   */
  function makeExchangeDoc(): DiscoveryDoc {
    const configuration = makeConfiguration({
      issuer: "https://auth.example.com",
      token_endpoint: doc.token_endpoint,
    });
    return {
      ...doc,
      configuration: configuration as unknown as DiscoveryDoc["configuration"],
    };
  }

  const callbackUrl: URL = new URL(
    "https://app.example.com/bff/callback?code=auth-code&state=state-123",
  );

  it("delegates to openid-client authorizationCodeGrant with the Configuration, callback URL, and state/nonce/PKCE checks", async () => {
    const config: BffConfig = makeConfig();
    const exchangeDoc: DiscoveryDoc = makeExchangeDoc();
    authorizationCodeGrantMock.mockResolvedValue({
      access_token: "at",
      refresh_token: "rt",
      id_token: "it",
      expires_in: 3600,
      token_type: "Bearer",
    });

    await exchangeCode(config, exchangeDoc, {
      code: "auth-code",
      codeVerifier: "verifier-123",
      state: "state-123",
      nonce: "nonce-xyz",
      currentUrl: callbackUrl,
    });

    expect(authorizationCodeGrantMock).toHaveBeenCalledTimes(1);
    const [passedConfig, passedUrl, checks] =
      authorizationCodeGrantMock.mock.calls[0] as [
        unknown,
        URL,
        {
          expectedState: string;
          expectedNonce: string;
          pkceCodeVerifier: string;
        },
      ];
    // The opaque Configuration handle carried on the doc is forwarded as-is.
    expect(passedConfig).toBe(exchangeDoc.configuration);
    // The full callback URL is handed to openid-client for code/state extraction.
    expect(passedUrl).toBe(callbackUrl);
    // openid-client validates state + nonce and binds the PKCE verifier — this
    // is the id_token/state/nonce protection the native fetch flow lacked.
    expect(checks.expectedState).toBe("state-123");
    expect(checks.expectedNonce).toBe("nonce-xyz");
    expect(checks.pkceCodeVerifier).toBe("verifier-123");
  });

  it("maps the openid-client token response to the TokenResponse shape", async () => {
    const config: BffConfig = makeConfig();
    const exchangeDoc: DiscoveryDoc = makeExchangeDoc();
    // openid-client returns TokenEndpointResponse & helpers; exchangeCode must
    // project only the token fields (dropping helper methods like claims()).
    authorizationCodeGrantMock.mockResolvedValue({
      access_token: "access-abc",
      refresh_token: "refresh-def",
      id_token: "id-ghi",
      expires_in: 1800,
      token_type: "Bearer",
      claims: (): Record<string, unknown> => ({ sub: "user-1" }),
    });

    const result: TokenResponse = await exchangeCode(config, exchangeDoc, {
      code: "auth-code",
      codeVerifier: "verifier-123",
      state: "state-123",
      nonce: "nonce-xyz",
      currentUrl: callbackUrl,
    });

    expect(result).toEqual({
      access_token: "access-abc",
      refresh_token: "refresh-def",
      id_token: "id-ghi",
      expires_in: 1800,
      token_type: "Bearer",
    });
  });

  it("propagates id_token / state / nonce validation errors thrown by openid-client", async () => {
    const config: BffConfig = makeConfig();
    const exchangeDoc: DiscoveryDoc = makeExchangeDoc();
    authorizationCodeGrantMock.mockRejectedValue(
      new Error("unexpected ID Token nonce claim value"),
    );

    await expect(
      exchangeCode(config, exchangeDoc, {
        code: "auth-code",
        codeVerifier: "verifier-123",
        state: "state-123",
        nonce: "WRONG",
        currentUrl: callbackUrl,
      }),
    ).rejects.toThrow("unexpected ID Token nonce claim value");
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
