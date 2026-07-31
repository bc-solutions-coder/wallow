import { readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { afterEach, describe, expect, it, vi } from "vitest";

import type { BffConfig } from "./config";
import {
  buildAuthorizeUrl,
  buildLogoutUrl,
  discover,
  exchangeCode,
  fetchUserInfo,
  refreshTokens,
  shouldAllowInsecureRequests,
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
  buildEndSessionUrlMock,
  skipSubjectCheckSentinel,
  makeConfiguration,
} = vi.hoisted(() => {
  const discoveryMock: ReturnType<typeof vi.fn> = vi.fn();
  const allowInsecureRequestsMock: ReturnType<typeof vi.fn> = vi.fn();
  const buildAuthorizationUrlMock: ReturnType<typeof vi.fn> = vi.fn();
  const authorizationCodeGrantMock: ReturnType<typeof vi.fn> = vi.fn();
  const refreshTokenGrantMock: ReturnType<typeof vi.fn> = vi.fn();
  const fetchUserInfoMock: ReturnType<typeof vi.fn> = vi.fn();
  const buildEndSessionUrlMock: ReturnType<typeof vi.fn> = vi.fn();
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
    buildEndSessionUrlMock,
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
  buildEndSessionUrl: buildEndSessionUrlMock,
  skipSubjectCheck: skipSubjectCheckSentinel,
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
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
    sessionTtlSeconds: 86400,
    cookieSecure: true,
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
      authorization_endpoint: "https://discover-basic.example.com/connect/authorize",
      token_endpoint: "https://discover-basic.example.com/connect/token",
      end_session_endpoint: "https://discover-basic.example.com/connect/logout",
      userinfo_endpoint: "https://discover-basic.example.com/connect/userinfo",
    });
    discoveryMock.mockResolvedValue(configuration);

    const result: DiscoveryDoc = await discover(config);

    // openid-client discovery() is invoked with a URL, the client id, and secret.
    expect(discoveryMock).toHaveBeenCalledTimes(1);
    const [url, clientId, clientSecret] = discoveryMock.mock.calls[0] as [URL, string, string];
    expect(url).toBeInstanceOf(URL);
    expect(url.href).toBe("https://discover-basic.example.com/.well-known/openid-configuration");
    expect(clientId).toBe(config.clientId);
    expect(clientSecret).toBe(config.clientSecret);

    expect(result.authorization_endpoint).toBe(
      "https://discover-basic.example.com/connect/authorize",
    );
    expect(result.token_endpoint).toBe("https://discover-basic.example.com/connect/token");
    expect(result.end_session_endpoint).toBe("https://discover-basic.example.com/connect/logout");
    expect(result.userinfo_endpoint).toBe("https://discover-basic.example.com/connect/userinfo");
    // The adapter carries a handle to the openid-client Configuration.
    expect(result.configuration).toBe(configuration);
  });

  it("caches by metadata URL — a second call does not re-run discovery", async () => {
    const config: BffConfig = makeConfig({
      issuer: "https://discover-cache.example.com",
    });
    const configuration = makeConfiguration({
      issuer: "https://discover-cache.example.com",
      authorization_endpoint: "https://discover-cache.example.com/connect/authorize",
      token_endpoint: "https://discover-cache.example.com/connect/token",
      end_session_endpoint: "https://discover-cache.example.com/connect/logout",
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
      metadataUrl: "https://internal.svc.local/.well-known/openid-configuration",
    });
    const configuration = makeConfiguration({
      issuer: "https://internal.svc.local",
      authorization_endpoint: "https://internal.svc.local/connect/authorize",
      token_endpoint: "https://internal.svc.local/connect/token",
      end_session_endpoint: "https://internal.svc.local/connect/logout",
      userinfo_endpoint: "https://internal.svc.local/connect/userinfo",
    });
    discoveryMock.mockResolvedValue(configuration);

    const result: DiscoveryDoc = await discover(config);

    // discovery() is called with the configured metadata URL.
    const [url] = discoveryMock.mock.calls[0] as [URL, string, string];
    expect(url.href).toBe("https://internal.svc.local/.well-known/openid-configuration");

    // Browser-facing endpoints are re-pinned to the public issuer origin.
    expect(result.authorization_endpoint).toBe("https://public.example.com/connect/authorize");
    expect(result.end_session_endpoint).toBe("https://public.example.com/connect/logout");
    // Backchannel endpoints stay exactly as advertised (server-reachable).
    expect(result.token_endpoint).toBe("https://internal.svc.local/connect/token");
    expect(result.userinfo_endpoint).toBe("https://internal.svc.local/connect/userinfo");
  });

  it("uses endpoints as advertised when metadataUrl is not set", async () => {
    const config: BffConfig = makeConfig({
      issuer: "https://discover-nopin.example.com",
    });
    const configuration = makeConfiguration({
      issuer: "https://discover-nopin.example.com",
      authorization_endpoint: "https://discover-nopin.example.com/connect/authorize",
      token_endpoint: "https://discover-nopin.example.com/connect/token",
      end_session_endpoint: "https://discover-nopin.example.com/connect/logout",
      userinfo_endpoint: "https://discover-nopin.example.com/connect/userinfo",
    });
    discoveryMock.mockResolvedValue(configuration);

    const result: DiscoveryDoc = await discover(config);

    expect(result.authorization_endpoint).toBe(
      "https://discover-nopin.example.com/connect/authorize",
    );
    expect(result.end_session_endpoint).toBe("https://discover-nopin.example.com/connect/logout");
  });
});

/**
 * Path-preserving endpoint pinning (Wallow-vufu.2.1).
 *
 * OpenIddict derives its advertised endpoint URIs from the base of the request
 * that fetched them, so a discovery document fetched over the internal network
 * advertises the internal host and — behind a path-based reverse proxy — omits
 * the public path prefix entirely. Pinning must therefore rebase the
 * browser-facing endpoints onto the FULL public issuer URL (origin *and* path),
 * not just its origin: with issuer `https://wallow.dev/api`, the browser has to
 * be sent to `https://wallow.dev/api/connect/authorize`, never
 * `https://wallow.dev/connect/authorize`.
 */
interface PinningCase {
  /** Deployment topology this row stands for. */
  readonly topology: string;
  /** Public issuer the browser reaches, as configured on the BFF. */
  readonly issuer: string;
  /** Server-reachable discovery URL (`BffConfig.metadataUrl`). */
  readonly metadataUrl: string;
  /** `authorization_endpoint` the metadata advertises. */
  readonly advertisedAuthorize: string;
  /** `end_session_endpoint` the metadata advertises. */
  readonly advertisedEndSession: string;
  /** `token_endpoint` the metadata advertises (backchannel). */
  readonly advertisedToken: string;
  /** `userinfo_endpoint` the metadata advertises (backchannel). */
  readonly advertisedUserinfo: string;
  /** Browser-facing authorize URL the pinning must produce. */
  readonly expectedAuthorize: string;
  /** Browser-facing end-session URL the pinning must produce. */
  readonly expectedEndSession: string;
}

const PINNING_CASES: readonly PinningCase[] = [
  {
    topology: "dev (Aspire: auth app on 3002, API on 5001, no path prefix)",
    issuer: "http://localhost:3002",
    metadataUrl: "http://localhost:5001/.well-known/openid-configuration",
    advertisedAuthorize: "http://localhost:5001/connect/authorize",
    advertisedEndSession: "http://localhost:5001/connect/logout",
    advertisedToken: "http://localhost:5001/connect/token",
    advertisedUserinfo: "http://localhost:5001/connect/userinfo",
    expectedAuthorize: "http://localhost:3002/connect/authorize",
    expectedEndSession: "http://localhost:3002/connect/logout",
  },
  {
    topology: "e2e (containerised stack reached through host.docker.internal)",
    issuer: "http://localhost:5050",
    metadataUrl: "http://host.docker.internal:5050/.well-known/openid-configuration",
    advertisedAuthorize: "http://host.docker.internal:5050/connect/authorize",
    advertisedEndSession: "http://host.docker.internal:5050/connect/logout",
    advertisedToken: "http://host.docker.internal:5050/connect/token",
    advertisedUserinfo: "http://host.docker.internal:5050/connect/userinfo",
    expectedAuthorize: "http://localhost:5050/connect/authorize",
    expectedEndSession: "http://localhost:5050/connect/logout",
  },
  {
    topology: "prod path-based (issuer /api, metadata advertises no prefix)",
    issuer: "https://wallow.dev/api",
    metadataUrl: "http://wallow-api:8080/.well-known/openid-configuration",
    advertisedAuthorize: "http://wallow-api:8080/connect/authorize",
    advertisedEndSession: "http://wallow-api:8080/connect/logout",
    advertisedToken: "http://wallow-api:8080/connect/token",
    advertisedUserinfo: "http://wallow-api:8080/connect/userinfo",
    expectedAuthorize: "https://wallow.dev/api/connect/authorize",
    expectedEndSession: "https://wallow.dev/api/connect/logout",
  },
  {
    topology: "prod path-based (API runs with PathBase, metadata already carries /api)",
    issuer: "https://wallow.dev/api",
    metadataUrl: "http://wallow-api:8080/api/.well-known/openid-configuration",
    advertisedAuthorize: "http://wallow-api:8080/api/connect/authorize",
    advertisedEndSession: "http://wallow-api:8080/api/connect/logout",
    advertisedToken: "http://wallow-api:8080/api/connect/token",
    advertisedUserinfo: "http://wallow-api:8080/api/connect/userinfo",
    expectedAuthorize: "https://wallow.dev/api/connect/authorize",
    expectedEndSession: "https://wallow.dev/api/connect/logout",
  },
];

describe("discover pins browser-facing endpoints to the full public issuer", () => {
  it.each(PINNING_CASES)(
    "$topology: rebases authorize and end-session onto the issuer, leaving backchannel endpoints alone",
    async (testCase: PinningCase) => {
      const config: BffConfig = makeConfig({
        issuer: testCase.issuer,
        metadataUrl: testCase.metadataUrl,
      });
      discoveryMock.mockResolvedValue(
        makeConfiguration({
          issuer: new URL(testCase.metadataUrl).origin,
          authorization_endpoint: testCase.advertisedAuthorize,
          token_endpoint: testCase.advertisedToken,
          end_session_endpoint: testCase.advertisedEndSession,
          userinfo_endpoint: testCase.advertisedUserinfo,
        }),
      );

      const result: DiscoveryDoc = await discover(config);

      expect(result.authorization_endpoint).toBe(testCase.expectedAuthorize);
      expect(result.end_session_endpoint).toBe(testCase.expectedEndSession);
      // Backchannel endpoints are server-reachable by construction; rebasing
      // them onto the public issuer would route the token exchange back out
      // through the proxy.
      expect(result.token_endpoint).toBe(testCase.advertisedToken);
      expect(result.userinfo_endpoint).toBe(testCase.advertisedUserinfo);
    },
  );

  it("tolerates a trailing slash on the issuer without doubling it into the path", async () => {
    const config: BffConfig = makeConfig({
      issuer: "https://wallow.dev/api/",
      metadataUrl: "http://wallow-api-slash:8080/.well-known/openid-configuration",
    });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: "http://wallow-api-slash:8080",
        authorization_endpoint: "http://wallow-api-slash:8080/connect/authorize",
        token_endpoint: "http://wallow-api-slash:8080/connect/token",
        end_session_endpoint: "http://wallow-api-slash:8080/connect/logout",
        userinfo_endpoint: "http://wallow-api-slash:8080/connect/userinfo",
      }),
    );

    const result: DiscoveryDoc = await discover(config);

    expect(result.authorization_endpoint).toBe("https://wallow.dev/api/connect/authorize");
    expect(result.end_session_endpoint).toBe("https://wallow.dev/api/connect/logout");
  });

  it("preserves a query string carried on an advertised endpoint", async () => {
    const config: BffConfig = makeConfig({
      issuer: "https://wallow.dev/api",
      metadataUrl: "http://wallow-api-query:8080/.well-known/openid-configuration",
    });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: "http://wallow-api-query:8080",
        authorization_endpoint: "http://wallow-api-query:8080/connect/authorize?tenant=acme",
        token_endpoint: "http://wallow-api-query:8080/connect/token",
        end_session_endpoint: "http://wallow-api-query:8080/connect/logout",
      }),
    );

    const result: DiscoveryDoc = await discover(config);

    expect(result.authorization_endpoint).toBe(
      "https://wallow.dev/api/connect/authorize?tenant=acme",
    );
  });

  it("leaves endpoints untouched when metadataUrl is unset even if the issuer carries a path", async () => {
    // Without a split horizon the metadata was fetched through the public URL,
    // so its endpoints already carry the prefix and must not be rebased again.
    const config: BffConfig = makeConfig({ issuer: "https://wallow-nopin.dev/api" });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: "https://wallow-nopin.dev/api",
        authorization_endpoint: "https://wallow-nopin.dev/api/connect/authorize",
        token_endpoint: "https://wallow-nopin.dev/api/connect/token",
        end_session_endpoint: "https://wallow-nopin.dev/api/connect/logout",
        userinfo_endpoint: "https://wallow-nopin.dev/api/connect/userinfo",
      }),
    );

    const result: DiscoveryDoc = await discover(config);

    expect(result.authorization_endpoint).toBe("https://wallow-nopin.dev/api/connect/authorize");
    expect(result.end_session_endpoint).toBe("https://wallow-nopin.dev/api/connect/logout");
  });
});

/**
 * The plain-HTTP discovery gate (Wallow-pu6a.4.7).
 *
 * The apps are TanStack Start, so the SDK's server entry is bundled INTO each
 * app's nitro production build. Vite's bundler substitutes build-time environment
 * reads with literals there and then constant-folds the branch away, which is
 * how `process.env.NODE_ENV !== "production"` silently became `void 0` in
 * `.output/server/_ssr/bff-*.mjs` and made every containerised login return
 * `OAUTH_HTTP_REQUEST_FORBIDDEN`. The gate must therefore be decided from a
 * signal only knowable at runtime — the protocol of the URL being discovered —
 * so no bundler can pre-compute it.
 */
describe("shouldAllowInsecureRequests", () => {
  it("allows insecure requests when the discovery URL is plain http", () => {
    expect(
      shouldAllowInsecureRequests("http://localhost:5050/.well-known/openid-configuration"),
    ).toBe(true);
  });

  it("refuses insecure requests when the discovery URL is https", () => {
    expect(
      shouldAllowInsecureRequests("https://auth.example.com/.well-known/openid-configuration"),
    ).toBe(false);
  });

  it("still allows a plain-http discovery URL when NODE_ENV is production", () => {
    // The exact configuration of the containerised stack: a production build
    // pointed at a plain-http issuer. A gate keyed on NODE_ENV answers false
    // here (and a bundled one cannot answer at all).
    vi.stubEnv("NODE_ENV", "production");

    expect(
      shouldAllowInsecureRequests("http://localhost:5050/.well-known/openid-configuration"),
    ).toBe(true);
  });

  it("refuses an https discovery URL even when NODE_ENV is development", () => {
    // The gate is not a dev backdoor: an https issuer wants the gate closed
    // regardless of how the process was started.
    vi.stubEnv("NODE_ENV", "development");

    expect(
      shouldAllowInsecureRequests("https://auth.example.com/.well-known/openid-configuration"),
    ).toBe(false);
  });
});

describe("discover applies the plain-http gate", () => {
  /** The `options` (5th) argument openid-client's `discovery()` was called with. */
  function discoveryOptions(): { execute?: unknown[] } | undefined {
    const call = discoveryMock.mock.calls[0] as [URL, string, string, unknown, unknown];
    return call[4] as { execute?: unknown[] } | undefined;
  }

  it("passes allowInsecureRequests when the issuer is plain http", async () => {
    const config: BffConfig = makeConfig({ issuer: "http://gate-http.example.com" });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: "http://gate-http.example.com",
        authorization_endpoint: "http://gate-http.example.com/connect/authorize",
        token_endpoint: "http://gate-http.example.com/connect/token",
      }),
    );

    await discover(config);

    expect(discoveryOptions()?.execute).toEqual([allowInsecureRequestsMock]);
  });

  it("passes allowInsecureRequests for a plain-http issuer even under NODE_ENV=production", async () => {
    // The regression this bead fixes: in the built nitro server this branch had
    // been folded to `void 0`, so discovery refused the plain-http issuer.
    vi.stubEnv("NODE_ENV", "production");
    const config: BffConfig = makeConfig({ issuer: "http://gate-http-prod.example.com" });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: "http://gate-http-prod.example.com",
        authorization_endpoint: "http://gate-http-prod.example.com/connect/authorize",
        token_endpoint: "http://gate-http-prod.example.com/connect/token",
      }),
    );

    await discover(config);

    expect(discoveryOptions()?.execute).toEqual([allowInsecureRequestsMock]);
  });

  it("omits the option entirely for an https issuer", async () => {
    const config: BffConfig = makeConfig({ issuer: "https://gate-https.example.com" });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: "https://gate-https.example.com",
        authorization_endpoint: "https://gate-https.example.com/connect/authorize",
        token_endpoint: "https://gate-https.example.com/connect/token",
      }),
    );

    await discover(config);

    // openid-client must not be handed an execute chain it does not need — the
    // https deployment keeps the transport check on.
    expect(discoveryOptions()).toBeUndefined();
  });

  it("keys the gate on the URL discovery is actually fetched from, not the public issuer", async () => {
    // Split-horizon: the browser-facing issuer is https while the server reaches
    // the OP over a plain-http internal host. The discovery request is the http
    // one, so the gate must be open.
    const config: BffConfig = makeConfig({
      issuer: "https://gate-split.example.com",
      metadataUrl: "http://internal.gate-split.local/.well-known/openid-configuration",
    });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: "http://internal.gate-split.local",
        authorization_endpoint: "http://internal.gate-split.local/connect/authorize",
        token_endpoint: "http://internal.gate-split.local/connect/token",
      }),
    );

    await discover(config);

    expect(discoveryOptions()?.execute).toEqual([allowInsecureRequestsMock]);
  });
});

describe("the bundled server surface reads no bundler-foldable environment signal", () => {
  const serverDir: string = dirname(fileURLToPath(import.meta.url));

  /**
   * This spec necessarily names the very tokens it forbids (both in the pattern
   * literals and in the `vi.stubEnv` calls above), so it is the one file the
   * sweep skips.
   */
  const SELF: string = fileURLToPath(import.meta.url);

  /**
   * Signals Vite's bundler substitutes at BUILD time and then constant-folds. Any
   * these in the server entry is a decision the built nitro bundle has already
   * made before the process starts, and no runtime environment can change it.
   */
  const FOLDABLE_SIGNALS: ReadonlyArray<readonly [string, RegExp]> = [
    ["NODE_ENV", /NODE_ENV/u],
    ["import.meta.env", /import\.meta\.env/u],
  ];

  /** Every `.ts` module under the server entry, excluding specs. */
  function serverModules(dir: string): string[] {
    const found: string[] = [];
    for (const entry of readdirSync(dir)) {
      const full: string = join(dir, entry);
      if (statSync(full).isDirectory()) {
        found.push(...serverModules(full));
      } else if (full.endsWith(".ts") && !full.endsWith(".test.ts") && full !== SELF) {
        found.push(full);
      }
    }
    return found;
  }

  it.each(FOLDABLE_SIGNALS)("no module under src/server reads %s", (_name, pattern: RegExp) => {
    const offenders: string[] = serverModules(serverDir)
      .filter((file: string): boolean => pattern.test(readFileSync(file, "utf8")))
      .map((file: string): string => resolve(file));

    // Doc comments count: a comment that still describes the gate as
    // NODE_ENV-driven documents the bug rather than the fix.
    expect(offenders).toEqual([]);
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
    buildAuthorizationUrlMock.mockReturnValue(new URL(doc.authorization_endpoint));

    buildAuthorizeUrl(config, authorizeDoc, {
      state: "state-123",
      codeChallenge: "challenge-abc",
      nonce: "nonce-xyz",
    });

    const [, params] = buildAuthorizationUrlMock.mock.calls[0] as [unknown, Record<string, string>];
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
    const [passedConfig, passedUrl, checks] = authorizationCodeGrantMock.mock.calls[0] as [
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
    const [passedConfig, passedRefreshToken] = refreshTokenGrantMock.mock.calls[0] as [
      unknown,
      string,
    ];
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

    const result: TokenResponse = await refreshTokens(config, refreshDoc, "old-refresh");

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

    await expect(refreshTokens(config, refreshDoc, "expired-refresh")).rejects.toThrow(
      "invalid_grant",
    );
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
    const [passedConfig, passedAccessToken, expectedSubject] = fetchUserInfoMock.mock.calls[0] as [
      unknown,
      string,
      unknown,
    ];
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

    const result: Record<string, unknown> | null = await fetchUserInfo(userInfoDoc, "access-abc");

    expect(result).toEqual(claims);
  });

  it("returns null and skips the call when no userinfo endpoint is advertised", async () => {
    const noUserInfoDoc: DiscoveryDoc = {
      ...doc,
      userinfo_endpoint: undefined,
    };
    stubFetchAsForbidden();

    const result: Record<string, unknown> | null = await fetchUserInfo(noUserInfoDoc, "access-abc");

    expect(result).toBeNull();
    expect(fetchUserInfoMock).not.toHaveBeenCalled();
  });
});

describe("buildLogoutUrl", () => {
  /**
   * Build a DiscoveryDoc carrying a resolved openid-client Configuration handle
   * and an advertised end-session endpoint, as {@link discover} would populate
   * it. The Configuration is opaque to buildLogoutUrl — it is passed straight
   * through to openid-client's buildEndSessionUrl — so a stub object suffices.
   */
  function makeLogoutDoc(): DiscoveryDoc {
    const configuration = makeConfiguration({
      issuer: "https://auth.example.com",
      end_session_endpoint: doc.end_session_endpoint,
    });
    return {
      ...doc,
      configuration: configuration as unknown as DiscoveryDoc["configuration"],
    };
  }

  it("delegates to openid-client buildEndSessionUrl when an end_session_endpoint is advertised", () => {
    const config: BffConfig = makeConfig();
    const logoutDoc: DiscoveryDoc = makeLogoutDoc();
    const built: URL = new URL(
      `${doc.end_session_endpoint}?post_logout_redirect_uri=${encodeURIComponent(
        config.postLogoutRedirectUri,
      )}`,
    );
    buildEndSessionUrlMock.mockReturnValue(built);

    const url: string = buildLogoutUrl(config, logoutDoc, "id-token-hint-abc");

    // Returns the string form of the openid-client-built end-session URL.
    expect(url).toBe(built.toString());

    // Delegates once, forwarding the resolved Configuration handle from the doc.
    expect(buildEndSessionUrlMock).toHaveBeenCalledTimes(1);
    const [passedConfig, params] = buildEndSessionUrlMock.mock.calls[0] as [
      unknown,
      Record<string, string>,
    ];
    expect(passedConfig).toBe(logoutDoc.configuration);
    // RP-initiated logout parameters: post-logout redirect and id_token hint.
    expect(params.post_logout_redirect_uri).toBe(config.postLogoutRedirectUri);
    expect(params.id_token_hint).toBe("id-token-hint-abc");
  });

  it("omits id_token_hint when no id token hint is provided", () => {
    const config: BffConfig = makeConfig();
    const logoutDoc: DiscoveryDoc = makeLogoutDoc();
    buildEndSessionUrlMock.mockReturnValue(
      new URL(doc.end_session_endpoint ?? "https://auth.example.com/connect/logout"),
    );

    buildLogoutUrl(config, logoutDoc);

    const [, params] = buildEndSessionUrlMock.mock.calls[0] as [unknown, Record<string, string>];
    expect(params.post_logout_redirect_uri).toBe(config.postLogoutRedirectUri);
    expect("id_token_hint" in params).toBe(false);
  });

  it("falls back to <issuerOrigin>/connect/logout when no end_session_endpoint is advertised", () => {
    const config: BffConfig = makeConfig();
    const noEndSessionDoc: DiscoveryDoc = {
      authorization_endpoint: doc.authorization_endpoint,
      token_endpoint: doc.token_endpoint,
      end_session_endpoint: undefined,
      configuration: makeConfiguration({
        issuer: "https://auth.example.com",
      }) as unknown as DiscoveryDoc["configuration"],
    };

    const url: string = buildLogoutUrl(config, noEndSessionDoc, "id-token-hint-abc");

    // No end-session endpoint means openid-client is never consulted.
    expect(buildEndSessionUrlMock).not.toHaveBeenCalled();

    // The fallback targets <issuerOrigin>/connect/logout (Appendix A) carrying
    // the same post-logout redirect and id_token hint parameters.
    const parsed: URL = new URL(url);
    expect(parsed.origin).toBe(new URL(config.issuer).origin);
    expect(parsed.pathname).toBe("/connect/logout");
    expect(parsed.searchParams.get("post_logout_redirect_uri")).toBe(config.postLogoutRedirectUri);
    expect(parsed.searchParams.get("id_token_hint")).toBe("id-token-hint-abc");
  });

  it("omits id_token_hint on the fallback URL when no id token hint is provided", () => {
    const config: BffConfig = makeConfig();
    const noEndSessionDoc: DiscoveryDoc = {
      authorization_endpoint: doc.authorization_endpoint,
      token_endpoint: doc.token_endpoint,
      end_session_endpoint: undefined,
      configuration: makeConfiguration({
        issuer: "https://auth.example.com",
      }) as unknown as DiscoveryDoc["configuration"],
    };

    const url: string = buildLogoutUrl(config, noEndSessionDoc);

    const parsed: URL = new URL(url);
    expect(parsed.pathname).toBe("/connect/logout");
    expect(parsed.searchParams.get("post_logout_redirect_uri")).toBe(config.postLogoutRedirectUri);
    expect(parsed.searchParams.has("id_token_hint")).toBe(false);
  });

  it("keeps the issuer path prefix on the fallback URL behind a path-based proxy", () => {
    // OpenIddict advertises no end_session_endpoint, so the fallback is the
    // only logout URL the browser ever sees — resolving it against the issuer
    // ORIGIN drops the /api prefix and lands on the frontend's 404 page.
    const config: BffConfig = makeConfig({
      issuer: "https://wallow.dev/api",
      postLogoutRedirectUri: "https://wallow.dev/",
    });
    const noEndSessionDoc: DiscoveryDoc = {
      authorization_endpoint: "https://wallow.dev/api/connect/authorize",
      token_endpoint: "http://wallow-api:8080/connect/token",
      end_session_endpoint: undefined,
      configuration: makeConfiguration({
        issuer: "https://wallow.dev/api",
      }) as unknown as DiscoveryDoc["configuration"],
    };

    const url: string = buildLogoutUrl(config, noEndSessionDoc, "id-token-hint-abc");

    expect(buildEndSessionUrlMock).not.toHaveBeenCalled();
    const parsed: URL = new URL(url);
    expect(parsed.origin).toBe("https://wallow.dev");
    expect(parsed.pathname).toBe("/api/connect/logout");
    expect(parsed.searchParams.get("post_logout_redirect_uri")).toBe(config.postLogoutRedirectUri);
    expect(parsed.searchParams.get("id_token_hint")).toBe("id-token-hint-abc");
  });

  it("tolerates a trailing slash on the issuer when building the fallback URL", () => {
    const config: BffConfig = makeConfig({ issuer: "https://wallow.dev/api/" });
    const noEndSessionDoc: DiscoveryDoc = {
      authorization_endpoint: "https://wallow.dev/api/connect/authorize",
      token_endpoint: "http://wallow-api:8080/connect/token",
      end_session_endpoint: undefined,
      configuration: makeConfiguration({
        issuer: "https://wallow.dev/api/",
      }) as unknown as DiscoveryDoc["configuration"],
    };

    const url: string = buildLogoutUrl(config, noEndSessionDoc);

    expect(new URL(url).pathname).toBe("/api/connect/logout");
  });
});

/**
 * Browser-bound URLs must be built from the REBASED endpoints (Wallow-vufu.5.4).
 *
 * `discover` rebases `authorization_endpoint` and `end_session_endpoint` onto the
 * public issuer, but those strings were dead: `buildAuthorizeUrl` and
 * `buildLogoutUrl` delegate to openid-client, which builds from the
 * `Configuration`'s OWN `serverMetadata()` — the raw, un-rebased discovery
 * response. So under a split-horizon deployment (discovery fetched over the
 * container network, browser on the public origin) the BFF answered
 * `GET /bff/login` with a 302 to `http://wallow-api:8080/connect/authorize`: an
 * internal Docker hostname no browser can reach. The rebasing was correct and
 * simply never reached the user agent.
 *
 * The mocks below reproduce that faithfully: openid-client is made to return a
 * URL on the INTERNAL host, exactly as the real library does when its
 * Configuration carries raw metadata. The wrapper is what must pin it.
 */
describe("browser-bound URLs under a split-horizon issuer", () => {
  /** The public origin plus path prefix the browser reaches, via the ingress. */
  const PUBLIC_ISSUER: string = "http://localhost/api";

  /**
   * Resolve a split-horizon discovery document: metadata fetched from an
   * internal container host that advertises itself, issuer public.
   *
   * @param internalHost Unique internal host for this test — the discovery cache
   *   is keyed by metadata URL, so each case needs its own.
   */
  async function discoverSplitHorizon(
    internalHost: string,
  ): Promise<{ config: BffConfig; resolved: DiscoveryDoc }> {
    const internalOrigin: string = `http://${internalHost}:8080`;
    const config: BffConfig = makeConfig({
      issuer: PUBLIC_ISSUER,
      postLogoutRedirectUri: "http://localhost/",
      metadataUrl: `${internalOrigin}/.well-known/openid-configuration`,
    });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: internalOrigin,
        authorization_endpoint: `${internalOrigin}/connect/authorize`,
        token_endpoint: `${internalOrigin}/connect/token`,
        end_session_endpoint: `${internalOrigin}/connect/logout`,
        userinfo_endpoint: `${internalOrigin}/connect/userinfo`,
      }),
    );
    const resolved: DiscoveryDoc = await discover(config);
    return { config, resolved };
  }

  /**
   * Stand in for openid-client's builders, which resolve the endpoint from the
   * Configuration's raw metadata and append the caller's parameters.
   *
   * @param endpoint Absolute endpoint URL the real library would have used.
   */
  function buildLikeOpenIdClient(endpoint: string) {
    return (_configuration: unknown, params: Record<string, string>): URL => {
      const url: URL = new URL(endpoint);
      for (const [key, value] of Object.entries(params)) {
        url.searchParams.set(key, value);
      }
      return url;
    };
  }

  it("sends the browser to the public authorize endpoint, not the internal host", async () => {
    const { config, resolved } = await discoverSplitHorizon("wallow-api-authz");
    buildAuthorizationUrlMock.mockImplementation(
      buildLikeOpenIdClient("http://wallow-api-authz:8080/connect/authorize"),
    );

    const url: URL = new URL(
      buildAuthorizeUrl(config, resolved, {
        state: "state-123",
        codeChallenge: "challenge-abc",
        nonce: "nonce-xyz",
      }),
    );

    // The whole point: a reachable origin, with the ingress path prefix intact.
    expect(url.origin).toBe("http://localhost");
    expect(url.pathname).toBe("/api/connect/authorize");
    // The internal hostname must not survive anywhere in the redirect — and nor
    // must its port, which a bare `URL.host` assignment would leave behind.
    expect(url.toString()).not.toContain("wallow-api-authz");
    expect(url.port).toBe("");
  });

  it("keeps every authorization parameter openid-client produced", async () => {
    const { config, resolved } = await discoverSplitHorizon("wallow-api-params");
    buildAuthorizationUrlMock.mockImplementation(
      buildLikeOpenIdClient("http://wallow-api-params:8080/connect/authorize"),
    );

    const url: URL = new URL(
      buildAuthorizeUrl(config, resolved, {
        state: "state-123",
        codeChallenge: "challenge-abc",
        nonce: "nonce-xyz",
      }),
    );

    // Pinning relocates the endpoint; it must not disturb the query openid-client
    // encoded, or PKCE/state/nonce validation fails on the callback.
    expect(url.searchParams.get("response_type")).toBe("code");
    expect(url.searchParams.get("client_id")).toBe(config.clientId);
    expect(url.searchParams.get("redirect_uri")).toBe(config.redirectUri);
    expect(url.searchParams.get("scope")).toBe("openid profile email offline_access");
    expect(url.searchParams.get("state")).toBe("state-123");
    expect(url.searchParams.get("code_challenge")).toBe("challenge-abc");
    expect(url.searchParams.get("code_challenge_method")).toBe("S256");
    expect(url.searchParams.get("nonce")).toBe("nonce-xyz");
  });

  it("sends the browser to the public end-session endpoint, not the internal host", async () => {
    const { config, resolved } = await discoverSplitHorizon("wallow-api-logout");
    buildEndSessionUrlMock.mockImplementation(
      buildLikeOpenIdClient("http://wallow-api-logout:8080/connect/logout"),
    );

    // OpenIddict always advertises end_session_endpoint, so this — not the
    // fallback below it — is the branch production actually takes.
    const url: URL = new URL(buildLogoutUrl(config, resolved, "id-token-hint-abc"));

    expect(url.origin).toBe("http://localhost");
    expect(url.pathname).toBe("/api/connect/logout");
    expect(url.toString()).not.toContain("wallow-api-logout");
    expect(url.searchParams.get("post_logout_redirect_uri")).toBe(config.postLogoutRedirectUri);
    expect(url.searchParams.get("id_token_hint")).toBe("id-token-hint-abc");
  });

  it("preserves a query string the advertised endpoint itself carries", async () => {
    const internalOrigin: string = "http://wallow-api-tenant:8080";
    const config: BffConfig = makeConfig({
      issuer: PUBLIC_ISSUER,
      metadataUrl: `${internalOrigin}/.well-known/openid-configuration`,
    });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: internalOrigin,
        authorization_endpoint: `${internalOrigin}/connect/authorize?tenant=acme`,
        token_endpoint: `${internalOrigin}/connect/token`,
      }),
    );
    const resolved: DiscoveryDoc = await discover(config);
    buildAuthorizationUrlMock.mockImplementation(
      buildLikeOpenIdClient(`${internalOrigin}/connect/authorize?tenant=acme`),
    );

    const url: URL = new URL(
      buildAuthorizeUrl(config, resolved, {
        state: "state-123",
        codeChallenge: "challenge-abc",
        nonce: "nonce-xyz",
      }),
    );

    expect(url.origin).toBe("http://localhost");
    expect(url.pathname).toBe("/api/connect/authorize");
    // The provider's own parameter and the authorization parameters coexist.
    expect(url.searchParams.get("tenant")).toBe("acme");
    expect(url.searchParams.get("state")).toBe("state-123");
  });

  it("leaves the built URL alone when there is no split horizon", async () => {
    // Same origin on both sides: pinning must be a no-op, not a rewrite that
    // happens to land in the same place by luck.
    const config: BffConfig = makeConfig({ issuer: "https://no-split.example.com" });
    discoveryMock.mockResolvedValue(
      makeConfiguration({
        issuer: "https://no-split.example.com",
        authorization_endpoint: "https://no-split.example.com/connect/authorize",
        token_endpoint: "https://no-split.example.com/connect/token",
      }),
    );
    const resolved: DiscoveryDoc = await discover(config);
    const built: URL = new URL(
      "https://no-split.example.com/connect/authorize?response_type=code&client_id=web-bff",
    );
    buildAuthorizationUrlMock.mockReturnValue(built);

    const url: string = buildAuthorizeUrl(config, resolved, {
      state: "state-123",
      codeChallenge: "challenge-abc",
      nonce: "nonce-xyz",
    });

    expect(url).toBe(built.toString());
  });
});
