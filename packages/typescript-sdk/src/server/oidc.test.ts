import { afterEach, describe, expect, it, vi } from "vitest";

import type { BffConfig } from "./config";
import {
  buildAuthorizeUrl,
  discover,
  exchangeCode,
  fetchUserInfo,
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
  refreshTokenGrantMock,
  fetchUserInfoMock,
  skipSubjectCheckSentinel,
  makeConfiguration,
} = vi.hoisted(() => {
  const discoveryMock: ReturnType<typeof vi.fn> = vi.fn();
  const allowInsecureRequestsMock: ReturnType<typeof vi.fn> = vi.fn();
  const buildAuthorizationUrlMock: ReturnType<typeof vi.fn> = vi.fn();
  const authorizationCodeGrantMock: ReturnType<typeof vi.fn> = vi.fn();
  const refreshTokenGrantMock: ReturnType<typeof vi.fn> = vi.fn();
  const fetchUserInfoMock: ReturnType<typeof vi.fn> = vi.fn();
  // Sentinel standing in for openid-client's `skipSubjectCheck` symbol, so the
  // test can assert the wrapper forwards it when the subject is not yet known.
  const skipSubjectCheckSentinel: symbol = Symbol("skipSubjectCheck");
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
    refreshTokenGrantMock,
    fetchUserInfoMock,
    skipSubjectCheckSentinel,
    makeConfiguration,
  };
});

vi.mock("openid-client", () => ({
  discovery: discoveryMock,
  allowInsecureRequests: allowInsecureRequestsMock,
  buildAuthorizationUrl: buildAuthorizationUrlMock,
  authorizationCodeGrant: authorizationCodeGrantMock,
  refreshTokenGrant: refreshTokenGrantMock,
  fetchUserInfo: fetchUserInfoMock,
  skipSubjectCheck: skipSubjectCheckSentinel,
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
  /**
   * Build a DiscoveryDoc carrying a resolved openid-client Configuration handle,
   * as {@link discover} would populate it. The Configuration is opaque to
   * refreshTokens — it is passed straight through to openid-client's
   * refreshTokenGrant — so a stub object suffices.
   */
  function makeRefreshDoc(): DiscoveryDoc {
    const configuration = makeConfiguration({
      issuer: "https://auth.example.com",
      token_endpoint: doc.token_endpoint,
    });
    return {
      ...doc,
      configuration: configuration as unknown as DiscoveryDoc["configuration"],
    };
  }

  /**
   * Reject any native `fetch` so a lingering token-endpoint POST would fail the
   * test loudly: after migration the refresh grant must go through
   * openid-client's {@link refreshTokenGrant}, never a hand-rolled fetch.
   */
  function stubFetchAsForbidden(): ReturnType<typeof vi.fn> {
    const fetchMock: ReturnType<typeof vi.fn> = vi
      .fn()
      .mockRejectedValue(new Error("native fetch must not be used"));
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
  }

  it("delegates to openid-client refreshTokenGrant with the Configuration and refresh token", async () => {
    const config: BffConfig = makeConfig();
    const refreshDoc: DiscoveryDoc = makeRefreshDoc();
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchAsForbidden();
    refreshTokenGrantMock.mockResolvedValue({
      access_token: "at2",
      refresh_token: "rt2",
      expires_in: 3600,
      token_type: "Bearer",
    });

    await refreshTokens(config, refreshDoc, "refresh-123");

    expect(refreshTokenGrantMock).toHaveBeenCalledTimes(1);
    const [passedConfig, passedRefreshToken] =
      refreshTokenGrantMock.mock.calls[0] as [unknown, string];
    // The opaque Configuration handle carried on the doc is forwarded as-is.
    expect(passedConfig).toBe(refreshDoc.configuration);
    // The current refresh token is exchanged for a fresh token set.
    expect(passedRefreshToken).toBe("refresh-123");
    // The native token-endpoint POST is gone.
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("surfaces the rotated refresh_token and maps to the TokenResponse shape", async () => {
    const config: BffConfig = makeConfig();
    const refreshDoc: DiscoveryDoc = makeRefreshDoc();
    stubFetchAsForbidden();
    // openid-client returns TokenEndpointResponse & helpers; refreshTokens must
    // project only the token fields (dropping helper methods like claims()) and
    // must surface the rotated refresh_token returned by the grant.
    refreshTokenGrantMock.mockResolvedValue({
      access_token: "access-rotated",
      refresh_token: "refresh-rotated",
      id_token: "id-rotated",
      expires_in: 1200,
      token_type: "Bearer",
      claims: (): Record<string, unknown> => ({ sub: "user-1" }),
    });

    const result: TokenResponse = await refreshTokens(
      config,
      refreshDoc,
      "old-refresh",
    );

    expect(result).toEqual({
      access_token: "access-rotated",
      refresh_token: "refresh-rotated",
      id_token: "id-rotated",
      expires_in: 1200,
      token_type: "Bearer",
    });
  });

  it("propagates errors thrown by openid-client refreshTokenGrant", async () => {
    const config: BffConfig = makeConfig();
    const refreshDoc: DiscoveryDoc = makeRefreshDoc();
    stubFetchAsForbidden();
    refreshTokenGrantMock.mockRejectedValue(new Error("invalid_grant"));

    await expect(
      refreshTokens(config, refreshDoc, "expired-refresh"),
    ).rejects.toThrow("invalid_grant");
  });
});

describe("fetchUserInfo", () => {
  /**
   * Build a DiscoveryDoc carrying a resolved openid-client Configuration handle
   * and an advertised userinfo endpoint. The Configuration is opaque to
   * fetchUserInfo — it is passed straight through to openid-client's
   * fetchUserInfo — so a stub object suffices.
   */
  function makeUserInfoDoc(): DiscoveryDoc {
    const configuration = makeConfiguration({
      issuer: "https://auth.example.com",
      userinfo_endpoint: "https://auth.example.com/connect/userinfo",
    });
    return {
      ...doc,
      userinfo_endpoint: "https://auth.example.com/connect/userinfo",
      configuration: configuration as unknown as DiscoveryDoc["configuration"],
    };
  }

  /**
   * Reject any native `fetch` so a lingering hand-rolled Bearer request would
   * fail loudly: after migration the userinfo call must go through
   * openid-client's {@link fetchUserInfo}, never a native fetch.
   */
  function stubFetchAsForbidden(): ReturnType<typeof vi.fn> {
    const fetchMock: ReturnType<typeof vi.fn> = vi
      .fn()
      .mockRejectedValue(new Error("native fetch must not be used"));
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
  }

  it("delegates to openid-client fetchUserInfo with the Configuration, access token, and skipSubjectCheck", async () => {
    const userInfoDoc: DiscoveryDoc = makeUserInfoDoc();
    const fetchMock: ReturnType<typeof vi.fn> = stubFetchAsForbidden();
    fetchUserInfoMock.mockResolvedValue({ sub: "user-1", email: "u@e.com" });

    await fetchUserInfo(userInfoDoc, "access-abc");

    expect(fetchUserInfoMock).toHaveBeenCalledTimes(1);
    const [passedConfig, passedAccessToken, expectedSubject] =
      fetchUserInfoMock.mock.calls[0] as [unknown, string, unknown];
    // The opaque Configuration handle carried on the doc is forwarded as-is.
    expect(passedConfig).toBe(userInfoDoc.configuration);
    expect(passedAccessToken).toBe("access-abc");
    // The subject is not yet known at the userinfo call, so the wrapper forwards
    // openid-client's skipSubjectCheck sentinel rather than an expected subject.
    expect(expectedSubject).toBe(skipSubjectCheckSentinel);
    // The native Bearer fetch is gone.
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("returns the claims object resolved by openid-client fetchUserInfo", async () => {
    const userInfoDoc: DiscoveryDoc = makeUserInfoDoc();
    stubFetchAsForbidden();
    const claims: Record<string, unknown> = {
      sub: "user-1",
      email: "user@example.com",
      roles: ["admin"],
    };
    fetchUserInfoMock.mockResolvedValue(claims);

    const result: Record<string, unknown> | null = await fetchUserInfo(
      userInfoDoc,
      "access-abc",
    );

    expect(result).toEqual(claims);
  });

  it("returns null and skips the call when no userinfo endpoint is advertised", async () => {
    const noUserInfoDoc: DiscoveryDoc = {
      ...doc,
      userinfo_endpoint: undefined,
    };
    stubFetchAsForbidden();

    const result: Record<string, unknown> | null = await fetchUserInfo(
      noUserInfoDoc,
      "access-abc",
    );

    expect(result).toBeNull();
    expect(fetchUserInfoMock).not.toHaveBeenCalled();
  });
});
