import {
  createApp,
  defineEventHandler,
  toWebHandler,
  type App,
  type H3Event,
} from "h3";
import { afterEach, describe, expect, it, vi } from "vitest";
import { discovery, type Configuration } from "openid-client";

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
  // Userinfo is delegated to openid-client. The discovery stub above advertises
  // no userinfo_endpoint, so the wrapper short-circuits and this is never
  // invoked in these tests — provided for import parity with oidc.ts.
  fetchUserInfo: vi.fn(),
  skipSubjectCheck: Symbol("skipSubjectCheck"),
  // RP-initiated logout is delegated to openid-client: reads the end-session
  // endpoint from the resolved Configuration's serverMetadata() and appends the
  // supplied logout params, returning a URL. Mirrors buildAuthorizationUrl.
  buildEndSessionUrl: vi.fn(
    (
      configuration: { serverMetadata: () => Record<string, unknown> },
      params: Record<string, string>,
    ): URL => {
      const endpoint: string = configuration.serverMetadata()
        .end_session_endpoint as string;
      const url: URL = new URL(endpoint);
      for (const [key, value] of Object.entries(params)) {
        url.searchParams.set(key, value);
      }
      return url;
    },
  ),
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
    sessionTtlSeconds: 86400,
    cookieSecure: true,
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

  it("maps role/tenant/scope claims into first-class session.user fields", async () => {
    const config: BffConfig = makeConfig("https://cb-claims.example.com");
    const tx: LoginTx = {
      state: "st-c",
      nonce: "no-c",
      verifier: "ver-c",
      returnTo: "/dashboard",
    };
    const sealed: string = await sealTx(tx, config.cookiePassword);

    // The id_token carries authorization + tenant claims in their raw OIDC
    // shape; the callback must normalize them into first-class user fields.
    authorizationCodeGrantMock.mockResolvedValue({
      access_token: "at",
      refresh_token: "rt",
      id_token: makeIdToken({
        sub: "user-9",
        email: "u@e.com",
        role: "admin",
        roles: ["user"],
        scope: "read write",
        tenant_id: "tenant-42",
        tenant_name: "Acme Corp",
      }),
      expires_in: 3600,
      token_type: "Bearer",
    });
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")),
    );
    const handle = makeHandle(createBffHandlers(config));

    const cbRes: Response = await handle(
      new Request("http://localhost/bff/callback?code=code-c&state=st-c", {
        headers: { cookie: `wallow_bff_tx=${sealed}` },
      }),
    );
    expect(cbRes.status).toBe(302);

    // Read the persisted identity back out through the user handler.
    const userRes: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: cookieHeaderFrom(cbRes) },
      }),
    );
    expect(userRes.status).toBe(200);
    const user: BffSession["user"] = (await userRes.json()) as BffSession["user"];

    expect(user.sub).toBe("user-9");
    // role (string) + roles (array) merge into a normalized roles array.
    expect(user.roles).toEqual(expect.arrayContaining(["admin", "user"]));
    expect(user.roles).toHaveLength(2);
    // scope (space-delimited string) normalizes into permissions.
    expect(user.permissions).toEqual(expect.arrayContaining(["read", "write"]));
    // tenant claims are lifted into first-class fields.
    expect(user.tenantId).toBe("tenant-42");
    expect(user.tenantName).toBe("Acme Corp");
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

  it("falls back to <issuerOrigin>/connect/logout when no end_session_endpoint is advertised", async () => {
    const config: BffConfig = makeConfig("https://logout-fallback.example.com");
    const session: BffSession = makeSession();
    const sealed: string = await sealSession(session, config.cookiePassword);
    // For this issuer, discovery advertises NO end_session_endpoint, forcing the
    // RP-initiated logout to take the /connect/logout fallback path (Appendix A).
    vi.mocked(discovery).mockImplementationOnce(
      (server: URL | string): Promise<Configuration> =>
        Promise.resolve({
          serverMetadata: (): Record<string, unknown> => {
            const origin: string = new URL(server).origin;
            return {
              issuer: origin,
              authorization_endpoint: `${origin}/connect/authorize`,
              token_endpoint: `${origin}/connect/token`,
            };
          },
        } as unknown as Configuration),
    );
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new Error("unexpected discovery fetch")),
    );
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/logout", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(302);
    const location: string = res.headers.get("location") ?? "";
    const url: URL = new URL(location);
    expect(url.origin).toBe(new URL(config.issuer).origin);
    expect(url.pathname).toBe("/connect/logout");
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

/** The `Set-Cookie` value for a given cookie name, or `undefined` if absent. */
function setCookieFor(res: Response, name: string): string | undefined {
  return res.headers
    .getSetCookie()
    .find((cookie: string): boolean => cookie.startsWith(`${name}=`));
}

/** The value of a cookie from its `Set-Cookie` line (empty string if unset). */
function cookieValueOf(setCookie: string): string {
  const pair: string = setCookie.split(";", 1)[0] ?? "";
  return pair.slice(pair.indexOf("=") + 1);
}

/** True when the `Set-Cookie` line carries the given attribute (case-insensitive). */
function hasAttribute(setCookie: string, attribute: string): boolean {
  return setCookie
    .split(";")
    .slice(1)
    .some(
      (part: string): boolean =>
        part.trim().toLowerCase().split("=", 1)[0] === attribute.toLowerCase(),
    );
}

/**
 * The value of a `Set-Cookie` attribute (e.g. `Max-Age`), or `undefined` when the
 * attribute is absent. Attribute names are matched case-insensitively.
 */
function attributeValue(setCookie: string, attribute: string): string | undefined {
  const part: string | undefined = setCookie
    .split(";")
    .slice(1)
    .find(
      (candidate: string): boolean =>
        candidate.trim().toLowerCase().split("=", 1)[0] ===
        attribute.toLowerCase(),
    );
  if (part === undefined) {
    return undefined;
  }
  const eq: number = part.indexOf("=");
  return eq < 0 ? "" : part.slice(eq + 1).trim();
}

/** The shape the `/bff/user` endpoint returns once it exposes the CSRF token. */
type BffUserResponse = BffSession["user"] & { csrfToken?: string };

/**
 * Drive a full login callback for a fresh issuer and return the callback
 * response, from which the session and CSRF cookies can be read.
 */
async function completeCallback(
  config: BffConfig,
): Promise<{ res: Response; handle: (request: Request) => Promise<Response> }> {
  const tx: LoginTx = {
    state: "st-csrf",
    nonce: "no-csrf",
    verifier: "ver-csrf",
    returnTo: "/dashboard",
  };
  const sealed: string = await sealTx(tx, config.cookiePassword);
  authorizationCodeGrantMock.mockResolvedValue({
    access_token: "at",
    refresh_token: "rt",
    id_token: makeIdToken({ sub: "user-csrf", email: "u@e.com" }),
    expires_in: 3600,
    token_type: "Bearer",
  });
  vi.stubGlobal(
    "fetch",
    vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")),
  );
  const handle: (request: Request) => Promise<Response> = makeHandle(
    createBffHandlers(config),
  );
  const res: Response = await handle(
    new Request("http://localhost/bff/callback?code=code-csrf&state=st-csrf", {
      headers: { cookie: `wallow_bff_tx=${sealed}` },
    }),
  );
  return { res, handle };
}

describe("CSRF token issuance", () => {
  it("sets a companion CSRF cookie the browser can read (not HttpOnly)", async () => {
    const config: BffConfig = makeConfig("https://csrf-cookie.example.com");

    const { res } = await completeCallback(config);

    expect(res.status).toBe(302);
    const csrfCookie: string | undefined = setCookieFor(res, "wallow_bff-csrf");
    expect(csrfCookie).toBeDefined();
    // The double-submit token must be readable by browser JS, so it is the one
    // cookie the BFF writes WITHOUT HttpOnly.
    expect(hasAttribute(csrfCookie ?? "", "HttpOnly")).toBe(false);
    expect(hasAttribute(csrfCookie ?? "", "Secure")).toBe(true);
    expect(hasAttribute(csrfCookie ?? "", "SameSite")).toBe(true);
    expect(cookieValueOf(csrfCookie ?? "")).not.toBe("");
  });

  it("keeps the session cookie HttpOnly while the CSRF cookie is readable", async () => {
    const config: BffConfig = makeConfig("https://csrf-httponly.example.com");

    const { res } = await completeCallback(config);

    const sessionCookie: string | undefined = setCookieFor(res, "wallow_bff");
    expect(sessionCookie).toBeDefined();
    // Regression guard: exposing the CSRF token must not relax the session
    // cookie, which still carries the sealed tokens.
    expect(hasAttribute(sessionCookie ?? "", "HttpOnly")).toBe(true);
  });

  it("draws the token from the Web Crypto RNG, never Math.random", async () => {
    const config: BffConfig = makeConfig("https://csrf-rng.example.com");
    const randomSpy = vi.spyOn(Math, "random");
    const cryptoSpy = vi.spyOn(globalThis.crypto, "getRandomValues");

    const { res } = await completeCallback(config);

    expect(cryptoSpy).toHaveBeenCalled();
    expect(randomSpy).not.toHaveBeenCalled();

    // 24 random bytes base64url-encode to 32 characters of [A-Za-z0-9_-].
    const token: string = cookieValueOf(
      setCookieFor(res, "wallow_bff-csrf") ?? "",
    );
    expect(token).toMatch(/^[A-Za-z0-9_-]{32,}$/);
  });

  it("issues a distinct token per login", async () => {
    const first = await completeCallback(
      makeConfig("https://csrf-unique-1.example.com"),
    );
    const second = await completeCallback(
      makeConfig("https://csrf-unique-2.example.com"),
    );

    const firstToken: string = cookieValueOf(
      setCookieFor(first.res, "wallow_bff-csrf") ?? "",
    );
    const secondToken: string = cookieValueOf(
      setCookieFor(second.res, "wallow_bff-csrf") ?? "",
    );

    expect(firstToken).not.toBe("");
    expect(firstToken).not.toBe(secondToken);
  });

  it("returns the same token from /bff/user as the cookie carries (double submit)", async () => {
    const config: BffConfig = makeConfig("https://csrf-user.example.com");

    const { res, handle } = await completeCallback(config);
    const cookieToken: string = cookieValueOf(
      setCookieFor(res, "wallow_bff-csrf") ?? "",
    );

    const userRes: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: cookieHeaderFrom(res) },
      }),
    );

    expect(userRes.status).toBe(200);
    const body: BffUserResponse = (await userRes.json()) as BffUserResponse;
    // SPA clients that cannot read the cookie (or prefer not to) get the token
    // from the user endpoint; both must be the session's single token.
    expect(body.csrfToken).toBe(cookieToken);
    expect(body.sub).toBe("user-csrf");
  });

  it("exposes the stored session's csrfToken through /bff/user", async () => {
    const config: BffConfig = makeConfig("https://csrf-user-stored.example.com");
    const session: BffSession = makeSession({ csrfToken: "stored-csrf-token" });
    const sealed: string = await sealSession(session, config.cookiePassword);
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    const body: BffUserResponse = (await res.json()) as BffUserResponse;
    expect(body.csrfToken).toBe("stored-csrf-token");
    // The identity fields still surface unchanged alongside the token.
    expect(body.email).toBe(session.user.email);
  });

  it("never exposes session tokens through /bff/user", async () => {
    const config: BffConfig = makeConfig("https://csrf-user-leak.example.com");
    const session: BffSession = makeSession({ csrfToken: "stored-csrf-token" });
    const sealed: string = await sealSession(session, config.cookiePassword);
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    const body: string = await res.text();
    expect(body).not.toContain(session.accessToken);
    expect(body).not.toContain(session.refreshToken ?? "refresh-token-def");
  });
});

describe("session cookie hardening", () => {
  it("bounds the session cookie's lifetime with a Max-Age from sessionTtlSeconds", async () => {
    const config: BffConfig = makeConfig("https://cookie-maxage.example.com", {
      sessionTtlSeconds: 3600,
    });

    const { res } = await completeCallback(config);

    const sessionCookie: string | undefined = setCookieFor(res, "wallow_bff");
    expect(sessionCookie).toBeDefined();
    // Derived from config, never hardcoded: a different TTL must move this value.
    expect(attributeValue(sessionCookie ?? "", "Max-Age")).toBe("3600");
  });

  it("applies the configured Max-Age to every chunk of a chunked session cookie", async () => {
    const config: BffConfig = makeConfig("https://cookie-chunk-ttl.example.com", {
      sessionTtlSeconds: 7200,
    });
    const store: SessionStore = new CookieSessionStore({
      password: config.cookiePassword,
    });
    // A sealed session this large spans more than one cookie chunk.
    const session: BffSession = makeSession({ accessToken: "a".repeat(6000) });

    const app: App = createApp();
    app.use(
      "/write",
      defineEventHandler(async (event: H3Event): Promise<void> => {
        await writeSession(event, config, store, session);
      }),
    );
    const handle: (request: Request) => Promise<Response> = toWebHandler(app);

    const res: Response = await handle(new Request("http://localhost/write"));

    const written: string[] = res.headers
      .getSetCookie()
      .filter((cookie: string): boolean => cookieValueOf(cookie) !== "");
    // Guards the chunking path itself: a single-chunk write would pass the
    // Max-Age assertion below vacuously.
    expect(written.length).toBeGreaterThan(1);
    for (const cookie of written) {
      expect(attributeValue(cookie, "Max-Age")).toBe("7200");
    }
  });

  it("marks the session cookie Secure by default", async () => {
    const config: BffConfig = makeConfig("https://cookie-secure-on.example.com");

    const { res } = await completeCallback(config);

    const sessionCookie: string | undefined = setCookieFor(res, "wallow_bff");
    expect(hasAttribute(sessionCookie ?? "", "Secure")).toBe(true);
    expect(hasAttribute(sessionCookie ?? "", "HttpOnly")).toBe(true);
  });

  it("omits Secure when cookieSecure is false, without relaxing HttpOnly", async () => {
    const config: BffConfig = makeConfig("https://cookie-secure-off.example.com", {
      cookieSecure: false,
    });

    const { res } = await completeCallback(config);

    const sessionCookie: string | undefined = setCookieFor(res, "wallow_bff");
    expect(sessionCookie).toBeDefined();
    // Plain-HTTP local development drops Secure — and nothing else. The session
    // cookie carries the sealed tokens, so HttpOnly is not negotiable.
    expect(hasAttribute(sessionCookie ?? "", "Secure")).toBe(false);
    expect(hasAttribute(sessionCookie ?? "", "HttpOnly")).toBe(true);
    expect(hasAttribute(sessionCookie ?? "", "SameSite")).toBe(true);
  });

  it("keeps the CSRF cookie browser-readable while tracking the session cookie's Secure and Max-Age", async () => {
    const config: BffConfig = makeConfig("https://cookie-csrf-attrs.example.com", {
      sessionTtlSeconds: 1800,
      cookieSecure: false,
    });

    const { res } = await completeCallback(config);

    const csrfCookie: string | undefined = setCookieFor(res, "wallow_bff-csrf");
    expect(csrfCookie).toBeDefined();
    // Regression guard on Phase 6: the double-submit token is the ONE cookie the
    // BFF writes without HttpOnly, and hardening must not flip that.
    expect(hasAttribute(csrfCookie ?? "", "HttpOnly")).toBe(false);
    expect(cookieValueOf(csrfCookie ?? "")).not.toBe("");
    // Secure and Max-Age track the session cookie: the companion token must not
    // outlive the session it defends, nor demand HTTPS when the session does not.
    expect(hasAttribute(csrfCookie ?? "", "Secure")).toBe(false);
    expect(attributeValue(csrfCookie ?? "", "Max-Age")).toBe("1800");
  });

  it("marks the CSRF cookie Secure when cookieSecure is true", async () => {
    const config: BffConfig = makeConfig("https://cookie-csrf-secure.example.com", {
      sessionTtlSeconds: 900,
    });

    const { res } = await completeCallback(config);

    const csrfCookie: string | undefined = setCookieFor(res, "wallow_bff-csrf");
    expect(hasAttribute(csrfCookie ?? "", "Secure")).toBe(true);
    expect(hasAttribute(csrfCookie ?? "", "HttpOnly")).toBe(false);
    expect(attributeValue(csrfCookie ?? "", "Max-Age")).toBe("900");
  });

  it("keeps the login transaction cookie short-lived while honouring cookieSecure", async () => {
    const config: BffConfig = makeConfig("https://cookie-tx.example.com", {
      sessionTtlSeconds: 86400,
      cookieSecure: false,
    });
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

    const txCookie: string | undefined = setCookieFor(res, "wallow_bff_tx");
    expect(txCookie).toBeDefined();
    // The transaction cookie lives for the authorize round-trip only: the
    // session TTL must not leak into it.
    expect(attributeValue(txCookie ?? "", "Max-Age")).toBe("600");
    expect(hasAttribute(txCookie ?? "", "HttpOnly")).toBe(true);
    expect(hasAttribute(txCookie ?? "", "Secure")).toBe(false);
  });
});

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
