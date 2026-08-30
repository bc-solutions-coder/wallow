import { afterEach, describe, expect, it, vi } from "vitest";
import { discovery, type Configuration } from "openid-client";

import type { BffConfig } from "./config";
import {
  createBffHandlers,
  readSession,
  writeSession,
  type BffHandlers,
  type BffHandler,
} from "./handlers";
import type { DiscoveryDoc } from "./oidc";
import { CSRF_HEADER, CSRF_INVALID_CODE } from "./csrf";
import { sealSession, type BffSession } from "./session";
import { CookieSessionStore } from "./store/cookie";
import type { SessionStore } from "./store/types";
import { sealTx, unsealTx, type LoginTx } from "./txstate";

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
      const endpoint: string = configuration.serverMetadata().authorization_endpoint as string;
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
      const endpoint: string = configuration.serverMetadata().end_session_endpoint as string;
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

/** The CSRF token bound to every session {@link makeSession} builds. */
const CSRF_FIXTURE_TOKEN: string = "csrf-fixture-token-aaaaaaaaaaaaaaaa";

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

/**
 * Dispatch a request to one of the four handlers by path.
 *
 * The handlers are plain `(Request) => Promise<Response>` functions, so this is
 * a four-line `switch` rather than an h3 app: no `createApp`, no `toWebHandler`,
 * and nothing between the test's `new Request(...)` and the handler under test.
 */
function makeHandle(handlers: BffHandlers): (request: Request) => Promise<Response> {
  const routes: Record<string, BffHandler | undefined> = {
    "/bff/login": handlers.login,
    "/bff/callback": handlers.callback,
    "/bff/user": handlers.user,
    "/bff/logout": handlers.logout,
  };
  return async (request: Request): Promise<Response> => {
    const handler: BffHandler | undefined = routes[new URL(request.url).pathname];
    if (handler === undefined) {
      return new Response(null, { status: 404 });
    }
    return await handler(request);
  };
}

/** A minimal, unsigned JWT with the given payload (BFF trusts the TLS channel). */
function makeIdToken(payload: Record<string, unknown>): string {
  const encoded: string = Buffer.from(JSON.stringify(payload)).toString("base64url");
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
    csrfToken: CSRF_FIXTURE_TOKEN,
    ...overrides,
  };
}

/** A logout request carrying the method and CSRF token the handler requires. */
function logoutRequest(cookie: string, csrfToken: string = CSRF_FIXTURE_TOKEN): Request {
  return new Request("http://localhost/bff/logout", {
    method: "POST",
    headers: { cookie, [CSRF_HEADER]: csrfToken },
  });
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
    expect(setCookieFor(res, "wallow_bff_tx")).toBeDefined();
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
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")));
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/callback?code=code-123&state=st-1", {
        headers: { cookie: `wallow_bff_tx=${sealed}` },
      }),
    );

    expect(res.status).toBe(302);
    expect(res.headers.get("location")).toBe("/welcome");
    expect(setCookieFor(res, "wallow_bff")).toBeDefined();

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

  it("maps role/organization/scope claims into first-class session.user fields", async () => {
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
        org_id: "org-42",
        org_name: "Acme Corp",
      }),
      expires_in: 3600,
      token_type: "Bearer",
    });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")));
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
    expect(user.organizationId).toBe("org-42");
    expect(user.organizationName).toBe("Acme Corp");
  });
});

/**
 * The token-exchange `currentUrl` must be rooted at `config.redirectUri`
 * (Wallow-pu6a.3.1, NEW RISK 1).
 *
 * openid-client derives the token request's `redirect_uri` from the `currentUrl`
 * it is handed, while the authorize step sends `config.redirectUri`. Deriving
 * `currentUrl` from the incoming request instead would make the two disagree
 * behind TLS termination — the hosts build the `Request` as
 * `http://${req.headers.host}${req.url}` and ignore `x-forwarded-proto`, so a
 * request that reached the edge as `https://app.example.com/bff/callback`
 * arrives here as `http://localhost:3000/bff/callback`. The IdP would then
 * answer `invalid_grant` in every TLS-terminated deployment while every local
 * test still passed.
 */
describe("callback token-exchange currentUrl", () => {
  it("roots currentUrl at config.redirectUri, not at the incoming request URL", async () => {
    const config: BffConfig = makeConfig("https://cb-currenturl.example.com", {
      redirectUri: "https://app.example.com/bff/callback",
    });
    const sealed: string = await sealTx(
      { state: "st-u", nonce: "no-u", verifier: "ver-u", returnTo: "/" },
      config.cookiePassword,
    );
    authorizationCodeGrantMock.mockResolvedValue({
      access_token: "at",
      refresh_token: "rt",
      id_token: makeIdToken({ sub: "user-u" }),
      expires_in: 3600,
      token_type: "Bearer",
    });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")));
    const handle = makeHandle(createBffHandlers(config));

    // Exactly what a TLS-terminating proxy hands the Node host.
    await handle(
      new Request("http://localhost:3000/bff/callback?code=code-u&state=st-u", {
        headers: { cookie: `wallow_bff_tx=${sealed}` },
      }),
    );

    const [, currentUrl] = authorizationCodeGrantMock.mock.calls[0] as [unknown, URL];
    const passed: URL = new URL(String(currentUrl));
    // The redirect_uri openid-client derives from this must equal the one the
    // authorize request already sent.
    expect(passed.origin).toBe(new URL(config.redirectUri).origin);
    expect(passed.pathname).toBe(new URL(config.redirectUri).pathname);
    // The code/state openid-client validates still come from the real request.
    expect(passed.searchParams.get("code")).toBe("code-u");
    expect(passed.searchParams.get("state")).toBe("st-u");
  });
});

/**
 * Open-redirect guard on `returnTo` (Wallow-pu6a.1.5, finding F6/R5).
 *
 * `/bff/login?returnTo=` is attacker-reachable: anyone can hand a victim a link
 * to the app's own login endpoint carrying a foreign `returnTo`, and the value
 * survives the whole OIDC round trip inside the tx cookie before the callback
 * issues its redirect. Both ends must run the value through the existing
 * `isSafeReturnUrl` guard from `../auth-oidc`.
 *
 * SANITIZE, DO NOT REFUSE. An unsafe `returnTo` falls back to "/" and login
 * still proceeds — matching the backend's `ReturnUrlValidator.Sanitize`
 * behaviour. (The pure builders in `auth-oidc.ts` deliberately throw instead,
 * but those are called by app code with a value it chose; this handler is a
 * browser navigation entry point where a hard 400 would turn a merely-malformed
 * link into a broken login.) The guard covers the callback too, so a tx cookie
 * sealed by an older build — or by any path that bypasses the login handler —
 * cannot still land the browser on a foreign origin.
 */
describe("returnTo open-redirect guard", () => {
  /** Round-trip the login response's tx cookie back into its `LoginTx`. */
  async function txFromLogin(res: Response, password: string): Promise<LoginTx | null> {
    const setCookie: string | undefined = setCookieFor(res, "wallow_bff_tx");
    expect(setCookie).toBeDefined();
    return unsealTx(cookieValueOf(setCookie as string), password);
  }

  /** Drive the login handler once with the given raw query string. */
  async function loginWithQuery(issuer: string, query: string): Promise<[Response, BffConfig]> {
    const config: BffConfig = makeConfig(issuer);
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
    const res: Response = await handle(new Request(`http://localhost/bff/login?${query}`));
    return [res, config];
  }

  /** Drive the login handler once with the given raw `returnTo` query value. */
  async function login(issuer: string, returnTo: string): Promise<[Response, BffConfig]> {
    return loginWithQuery(issuer, `returnTo=${encodeURIComponent(returnTo)}`);
  }

  it.each([
    ["absolute foreign origin", "https://evil.example/pwn"],
    ["protocol-relative", "//evil.example/pwn"],
    ["backslash-escaped protocol-relative", String.raw`/\evil.example/pwn`],
    // oxlint-disable-next-line no-script-url -- the payload under test IS a script url
    ["javascript scheme", "javascript:alert(1)"],
  ])("login sanitizes a %s returnTo to '/'", async (label: string, returnTo: string) => {
    const [res, config] = await login(
      `https://login-guard-${label.replaceAll(" ", "-")}.example.com`,
      returnTo,
    );

    expect(res.status).toBe(302);
    const tx: LoginTx | null = await txFromLogin(res, config.cookiePassword);
    expect(tx?.returnTo).toBe("/");
  });

  it("login forwards an organization hint to the authorize request", async () => {
    // The organization picker re-authorizes with the hint; the BFF passes it
    // through untouched so the IdP runs that organization's enrollment policy.
    const [res] = await loginWithQuery(
      "https://login-org-hint.example.com",
      "returnTo=%2Fdashboard&organization=11111111-2222-3333-4444-555555555555",
    );

    expect(res.status).toBe(302);
    const url: URL = new URL(res.headers.get("location") ?? "");
    expect(url.searchParams.get("organization")).toBe("11111111-2222-3333-4444-555555555555");
  });

  it("login sends no organization parameter when the hint is absent or blank", async () => {
    const [res] = await loginWithQuery(
      "https://login-org-none.example.com",
      "returnTo=%2Fdashboard&organization=",
    );

    const url: URL = new URL(res.headers.get("location") ?? "");
    expect(url.searchParams.has("organization")).toBe(false);
  });

  it("login preserves a safe relative returnTo", async () => {
    const [res, config] = await login(
      "https://login-guard-safe.example.com",
      "/dashboard?tab=apps",
    );

    const tx: LoginTx | null = await txFromLogin(res, config.cookiePassword);
    expect(tx?.returnTo).toBe("/dashboard?tab=apps");
  });

  it("takes the first value of a repeated returnTo parameter and still sanitizes it", async () => {
    // A deliberate behaviour delta from the h3 handler this replaces: h3's
    // getQuery returned an ARRAY for a repeated parameter, which failed the
    // `typeof === "string"` test and fell back to "/". `searchParams.get`
    // returns the FIRST value instead, so the guard — not the parameter
    // parser — is what has to reject a smuggled second value.
    const [res, config] = await loginWithQuery(
      "https://login-guard-repeated.example.com",
      `returnTo=${encodeURIComponent("/safe")}&returnTo=${encodeURIComponent("//evil.test/pwn")}`,
    );

    const tx: LoginTx | null = await txFromLogin(res, config.cookiePassword);
    expect(tx?.returnTo).toBe("/safe");
  });

  it("sanitizes a repeated returnTo whose first value is the unsafe one", async () => {
    const [res, config] = await loginWithQuery(
      "https://login-guard-repeated-unsafe.example.com",
      `returnTo=${encodeURIComponent("//evil.test/pwn")}&returnTo=${encodeURIComponent("/safe")}`,
    );

    const tx: LoginTx | null = await txFromLogin(res, config.cookiePassword);
    expect(tx?.returnTo).toBe("/");
  });

  it("callback re-checks returnTo and redirects to '/' when the tx carries a foreign origin", async () => {
    const config: BffConfig = makeConfig("https://cb-guard.example.com");
    // A tx cookie sealed with an unsafe returnTo — what a build predating the
    // login-side guard would have issued. The callback must not trust it.
    const sealed: string = await sealTx(
      {
        state: "st-g",
        nonce: "no-g",
        verifier: "ver-g",
        returnTo: "https://evil.example/pwn",
      },
      config.cookiePassword,
    );
    authorizationCodeGrantMock.mockResolvedValue({
      access_token: "at",
      refresh_token: "rt",
      id_token: makeIdToken({ sub: "user-g" }),
      expires_in: 3600,
      token_type: "Bearer",
    });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")));
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/callback?code=code-g&state=st-g", {
        headers: { cookie: `wallow_bff_tx=${sealed}` },
      }),
    );

    expect(res.status).toBe(302);
    expect(res.headers.get("location")).toBe("/");
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

  it("answers with JSON, which the h3 layer used to add implicitly", async () => {
    const config: BffConfig = makeConfig("https://user-json.example.com");
    const session: BffSession = makeSession();
    const sealed: string = await sealSession(session, config.cookiePassword);
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    // h3 serialized the handler's returned object and stamped the content type;
    // nothing does that for us now, so the handler must do it itself.
    expect(res.headers.get("content-type") ?? "").toContain("application/json");
    const body: BffUserResponse = (await res.json()) as BffUserResponse;
    expect(body.sub).toBe(session.user.sub);
  });

  it("marks the identity response no-store", async () => {
    // This body carries the user's claims AND the CSRF token (Wallow-vufu.5.1,
    // finding L2). Without an explicit directive a shared intermediary — or the
    // browser's own back/forward cache — may keep one user's session data and
    // hand it to the next request on the same connection.
    const config: BffConfig = makeConfig("https://user-no-store.example.com");
    const sealed: string = await sealSession(makeSession(), config.cookiePassword);
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(
      new Request("http://localhost/bff/user", {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    expect(res.headers.get("cache-control") ?? "").toContain("no-store");
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
    withRefreshLock: <T>(ref: string, fn: () => Promise<T>): Promise<T | undefined> =>
      delegate.withRefreshLock(ref, fn),
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

    const res: Response = await handle(logoutRequest(`wallow_bff=${sealed}`));

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

    const res: Response = await handle(logoutRequest(`wallow_bff=${sealed}`));

    expect(res.status).toBe(302);
    const location: string = res.headers.get("location") ?? "";
    expect(location.startsWith(doc.end_session_endpoint ?? "")).toBe(true);
    const url: URL = new URL(location);
    expect(url.searchParams.get("post_logout_redirect_uri")).toBe(config.postLogoutRedirectUri);
    expect(url.searchParams.get("id_token_hint")).toBe(session.idToken);
    const cleared: string | undefined = setCookieFor(res, "wallow_bff");
    expect(cleared).toBeDefined();
    expect(cookieValueOf(cleared ?? "")).toBe("");
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
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("unexpected discovery fetch")));
    const handle = makeHandle(createBffHandlers(config));

    const res: Response = await handle(logoutRequest(`wallow_bff=${sealed}`));

    expect(res.status).toBe(302);
    const location: string = res.headers.get("location") ?? "";
    const url: URL = new URL(location);
    expect(url.origin).toBe(new URL(config.issuer).origin);
    expect(url.pathname).toBe("/connect/logout");
    expect(url.searchParams.get("post_logout_redirect_uri")).toBe(config.postLogoutRedirectUri);
    expect(url.searchParams.get("id_token_hint")).toBe(session.idToken);
    expect(setCookieFor(res, "wallow_bff")).toBeDefined();
  });
});

/**
 * Logout is a state-changing operation and must be gated like one
 * (Wallow-pu6a.3.2, finding F12a).
 *
 * The h3 handler this replaces accepted a bare `GET /bff/logout` from anyone:
 * an `<img src="/bff/logout">` on any page the victim visited was enough to
 * revoke their session server-side and clear their cookies. The port takes the
 * opportunity to require `POST` plus the session-bound CSRF token the `/api`
 * proxy already demands — and, critically, to destroy NOTHING when the check
 * fails. A rejected logout that still cleared the cookies would be the same
 * denial of service wearing a 403.
 */
describe("logout CSRF gate", () => {
  /** A logout handler over a recording store, with discovery stubbed. */
  function makeLogout(issuer: string): {
    handle: (request: Request) => Promise<Response>;
    destroyed: string[];
    config: BffConfig;
  } {
    const config: BffConfig = makeConfig(issuer);
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const { store, destroyed } = makeRecordingStore(config.cookiePassword);
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async (): Promise<DiscoveryDoc> => doc,
      }),
    );
    return { handle: makeHandle(createBffHandlers(config, store)), destroyed, config };
  }

  it.each(["GET", "HEAD", "PUT", "DELETE"])(
    "rejects a %s logout with 405 and destroys nothing",
    async (method: string) => {
      const { handle, destroyed, config } = makeLogout(
        `https://logout-method-${method.toLowerCase()}.example.com`,
      );
      const sealed: string = await sealSession(makeSession(), config.cookiePassword);

      const res: Response = await handle(
        new Request("http://localhost/bff/logout", {
          method,
          headers: { cookie: `wallow_bff=${sealed}` },
        }),
      );

      expect(res.status).toBe(405);
      expect(res.headers.get("allow") ?? "").toContain("POST");
      // The session survives: nothing revoked, nothing cleared.
      expect(destroyed).toEqual([]);
      expect(res.headers.getSetCookie()).toEqual([]);
    },
  );

  it("rejects a POST with no CSRF token and destroys nothing", async () => {
    const { handle, destroyed, config } = makeLogout("https://logout-csrf-missing.example.com");
    const sealed: string = await sealSession(makeSession(), config.cookiePassword);

    const res: Response = await handle(
      new Request("http://localhost/bff/logout", {
        method: "POST",
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(403);
    expect(res.headers.get("content-type") ?? "").toContain("problem+json");
    const body: Record<string, unknown> = (await res.json()) as Record<string, unknown>;
    expect(body["code"]).toBe(CSRF_INVALID_CODE);
    expect(destroyed).toEqual([]);
    expect(res.headers.getSetCookie()).toEqual([]);
  });

  it("rejects a POST whose CSRF token does not match the session token", async () => {
    const { handle, destroyed, config } = makeLogout("https://logout-csrf-mismatch.example.com");
    const sealed: string = await sealSession(makeSession(), config.cookiePassword);

    const res: Response = await handle(
      logoutRequest(`wallow_bff=${sealed}`, "not-the-session-token-bbbbbbbbbbbbbb"),
    );

    expect(res.status).toBe(403);
    expect(destroyed).toEqual([]);
    expect(res.headers.getSetCookie()).toEqual([]);
  });

  it("rejects a token minted for a different session", async () => {
    // The double-submit token is bound to the session, so an attacker who owns
    // a perfectly valid token of their own still cannot log the victim out.
    const { handle, destroyed, config } = makeLogout("https://logout-csrf-crosstalk.example.com");
    const victim: BffSession = makeSession({ csrfToken: "victim-csrf-token-cccccccccccccccc" });
    const sealed: string = await sealSession(victim, config.cookiePassword);

    const res: Response = await handle(
      logoutRequest(`wallow_bff=${sealed}`, "attacker-csrf-token-dddddddddddddd"),
    );

    expect(res.status).toBe(403);
    expect(destroyed).toEqual([]);
  });

  it("accepts a POST carrying the session-bound token", async () => {
    const { handle, destroyed, config } = makeLogout("https://logout-csrf-valid.example.com");
    const session: BffSession = makeSession();
    const sealed: string = await sealSession(session, config.cookiePassword);

    const res: Response = await handle(logoutRequest(`wallow_bff=${sealed}`));

    expect(res.status).toBe(302);
    expect(destroyed).toEqual([sealed]);
  });

  /**
   * Anonymous logout is idempotent, not forbidden (Wallow-vufu.5.1, finding L1).
   *
   * The CSRF gate above used to run unconditionally, and
   * `csrfTokenMatches(undefined, presented)` is false by construction — so a
   * logout with no session at all was answered with the same 403 as a genuine
   * cross-site attempt, and the user saw a spurious "Logout failed" for a
   * session that was already gone. There is nothing to protect when there is no
   * session: the request is a no-op that should succeed and tidy up the
   * browser's stale cookies.
   *
   * The narrowness matters. ONLY a null session skips the gate. A session that
   * EXISTS but carries no `csrfToken` stays rejected — treating a missing token
   * as "no protection needed" would hand every unprotected session to any
   * cross-site caller, which is the opposite of the fix.
   */
  it("answers an anonymous POST with 204 instead of 403", async () => {
    const { handle, destroyed } = makeLogout("https://logout-anon-204.example.com");

    const res: Response = await handle(
      new Request("http://localhost/bff/logout", { method: "POST" }),
    );

    expect(res.status).toBe(204);
    // Nothing existed to revoke, so the store is never asked to destroy a ref.
    expect(destroyed).toEqual([]);
  });

  it("clears the session and CSRF cookies on an anonymous POST", async () => {
    const { handle } = makeLogout("https://logout-anon-clears.example.com");

    const res: Response = await handle(
      new Request("http://localhost/bff/logout", { method: "POST" }),
    );

    // The browser may still be holding a cookie this server can no longer read
    // (rotated password, expired seal). Clearing is the whole point of letting
    // the request through.
    const session: string | undefined = setCookieFor(res, "wallow_bff");
    const csrf: string | undefined = setCookieFor(res, "wallow_bff-csrf");
    expect(session).toBeDefined();
    expect(csrf).toBeDefined();
    expect(cookieValueOf(session ?? "")).toBe("");
    expect(cookieValueOf(csrf ?? "")).toBe("");
  });

  it("answers 204 for an anonymous POST even when it carries a stale CSRF token", async () => {
    const { handle, destroyed } = makeLogout("https://logout-anon-stale-token.example.com");

    // The realistic shape of this request: the session cookie expired but the
    // browser-readable CSRF companion is still in the jar, so the client sends
    // a token for a session that no longer exists.
    const res: Response = await handle(
      new Request("http://localhost/bff/logout", {
        method: "POST",
        headers: { [CSRF_HEADER]: "stale-csrf-token-eeeeeeeeeeeeeeee" },
      }),
    );

    expect(res.status).toBe(204);
    expect(destroyed).toEqual([]);
  });

  it("treats an unreadable session cookie as anonymous rather than as a CSRF failure", async () => {
    const { handle, destroyed } = makeLogout("https://logout-anon-garbage.example.com");

    // `readSession` answers null for a cookie it cannot unseal, which is the
    // same "no session" state as sending no cookie at all.
    const res: Response = await handle(
      new Request("http://localhost/bff/logout", {
        method: "POST",
        headers: { cookie: "wallow_bff=not-a-sealed-session" },
      }),
    );

    expect(res.status).toBe(204);
    expect(destroyed).toEqual([]);
  });

  it.each([
    ["undefined", undefined],
    ["empty", ""],
  ])(
    "still rejects a POST for a session that exists but whose csrfToken is %s",
    async (label: string, csrfToken: string | undefined) => {
      const { handle, destroyed, config } = makeLogout(
        `https://logout-csrf-${label}-token.example.com`,
      );
      const sealed: string = await sealSession(makeSession({ csrfToken }), config.cookiePassword);

      const res: Response = await handle(
        logoutRequest(`wallow_bff=${sealed}`, "any-token-at-all-ffffffffffffffff"),
      );

      // Fail-secure: a session with no token is unprotected, not unguarded. The
      // anonymous escape hatch is for `session === null` only.
      expect(res.status).toBe(403);
      const body: Record<string, unknown> = (await res.json()) as Record<string, unknown>;
      expect(body["code"]).toBe(CSRF_INVALID_CODE);
      expect(destroyed).toEqual([]);
      expect(res.headers.getSetCookie()).toEqual([]);
    },
  );

  it("leaves the authenticated logout redirect untouched", async () => {
    // Regression guard for the branch the anonymous path must not disturb: a
    // real session still gets revoked server-side and still hands the browser
    // the IdP's end-session URL rather than a 204.
    const { handle, destroyed, config } = makeLogout("https://logout-authn-regression.example.com");
    const session: BffSession = makeSession();
    const sealed: string = await sealSession(session, config.cookiePassword);

    const res: Response = await handle(logoutRequest(`wallow_bff=${sealed}`));

    expect(res.status).toBe(302);
    expect(destroyed).toEqual([sealed]);
    const url: URL = new URL(res.headers.get("location") ?? "");
    expect(url.pathname).toBe("/connect/logout");
    expect(url.searchParams.get("id_token_hint")).toBe(session.idToken);
    expect(cookieValueOf(setCookieFor(res, "wallow_bff") ?? "")).toBe("");
  });
});

/**
 * Rebuild a request `cookie` header from a response's `Set-Cookie` list, keeping
 * only cookies that carry a non-empty value (i.e. dropping the expired chunk
 * cookies that `writeSession` emits to clear a previously larger session).
 */
function cookieHeaderFrom(res: Response): string {
  return cookieHeaderFromHeaders(res.headers);
}

/** {@link cookieHeaderFrom} against a bare `Headers` accumulator. */
function cookieHeaderFromHeaders(headers: Headers): string {
  return headers
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

/** The name a `Set-Cookie` line writes. */
function cookieNameOf(setCookie: string): string {
  const pair: string = setCookie.split(";", 1)[0] ?? "";
  return pair.slice(0, pair.indexOf("="));
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
        candidate.trim().toLowerCase().split("=", 1)[0] === attribute.toLowerCase(),
    );
  if (part === undefined) {
    return undefined;
  }
  const eq: number = part.indexOf("=");
  return eq === -1 ? "" : part.slice(eq + 1).trim();
}

/** The shape the `/bff/user` endpoint returns once it exposes the CSRF token. */
type BffUserResponse = BffSession["user"] & { csrfToken?: string };

/**
 * Drive a full login callback for a fresh issuer and return the callback
 * response, from which the session and CSRF cookies can be read.
 */
async function completeCallback(
  config: BffConfig,
  tokenOverrides: Record<string, unknown> = {},
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
    ...tokenOverrides,
  });
  vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")));
  const handle: (request: Request) => Promise<Response> = makeHandle(createBffHandlers(config));
  const res: Response = await handle(
    new Request("http://localhost/bff/callback?code=code-csrf&state=st-csrf", {
      headers: { cookie: `${config.cookieName}_tx=${sealed}` },
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
    const token: string = cookieValueOf(setCookieFor(res, "wallow_bff-csrf") ?? "");
    expect(token).toMatch(/^[A-Za-z0-9_-]{32,}$/);
  });

  it("issues a distinct token per login", async () => {
    const first = await completeCallback(makeConfig("https://csrf-unique-1.example.com"));
    const second = await completeCallback(makeConfig("https://csrf-unique-2.example.com"));

    const firstToken: string = cookieValueOf(setCookieFor(first.res, "wallow_bff-csrf") ?? "");
    const secondToken: string = cookieValueOf(setCookieFor(second.res, "wallow_bff-csrf") ?? "");

    expect(firstToken).not.toBe("");
    expect(firstToken).not.toBe(secondToken);
  });

  it("returns the same token from /bff/user as the cookie carries (double submit)", async () => {
    const config: BffConfig = makeConfig("https://csrf-user.example.com");

    const { res, handle } = await completeCallback(config);
    const cookieToken: string = cookieValueOf(setCookieFor(res, "wallow_bff-csrf") ?? "");

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

    const headers: Headers = new Headers();
    await writeSession(headers, config, store, session);

    const written: string[] = headers
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

/**
 * A `__Host-`-prefixed cookie name must survive every name the BFF composes
 * (Wallow-pu6a.3.2, finding F10).
 *
 * RFC 6265bis only honours the prefix when the cookie is `Secure`, `Path=/`,
 * and carries no `Domain` — which `baseCookieOpts` already satisfies — and the
 * prefix is part of the NAME, so the chunk (`.1`), CSRF (`-csrf`), and
 * transaction (`_tx`) names all have to compose around it without colliding.
 * The default itself is `loadBffConfigFromEnv`'s business and is pinned in
 * config.test.ts; these tests pin the handler side.
 */
describe("__Host- prefixed cookie names", () => {
  const HOST_PREFIXED: string = "__Host-wallow_bff";

  it("writes the session, chunk, and CSRF cookies under distinct __Host- names", async () => {
    const config: BffConfig = makeConfig("https://host-prefix-names.example.com", {
      cookieName: HOST_PREFIXED,
    });

    // A token set this large forces the session across more than one chunk.
    const { res } = await completeCallback(config, { access_token: "a".repeat(6000) });

    const names: string[] = res.headers
      .getSetCookie()
      .filter((cookie: string): boolean => cookieValueOf(cookie) !== "")
      .map((cookie: string): string => cookieNameOf(cookie));

    expect(names).toContain(HOST_PREFIXED);
    expect(names).toContain(`${HOST_PREFIXED}.1`);
    expect(names).toContain(`${HOST_PREFIXED}-csrf`);
    // Every emitted cookie is a distinct name: a collapsed name would silently
    // overwrite a chunk and corrupt the session on reassembly.
    expect(new Set(names).size).toBe(names.length);
  });

  it("satisfies the __Host- attribute requirements on every cookie it writes", async () => {
    const config: BffConfig = makeConfig("https://host-prefix-attrs.example.com", {
      cookieName: HOST_PREFIXED,
    });

    const { res } = await completeCallback(config, { access_token: "a".repeat(6000) });

    for (const cookie of res.headers.getSetCookie()) {
      expect(hasAttribute(cookie, "Secure")).toBe(true);
      expect(attributeValue(cookie, "Path")).toBe("/");
      expect(hasAttribute(cookie, "Domain")).toBe(false);
    }
  });

  it("round-trips a __Host- session back through the read path", async () => {
    const config: BffConfig = makeConfig("https://host-prefix-roundtrip.example.com", {
      cookieName: HOST_PREFIXED,
    });
    const store: SessionStore = new CookieSessionStore({ password: config.cookiePassword });
    const session: BffSession = makeSession({ accessToken: "a".repeat(6000) });

    const headers: Headers = new Headers();
    await writeSession(headers, config, store, session);
    const restored: BffSession | null = await readSession(
      new Request("http://localhost/bff/user", {
        headers: { cookie: cookieHeaderFromHeaders(headers) },
      }),
      config,
      store,
    );

    expect(restored).toEqual(session);
  });
});

/**
 * Multiple `Set-Cookie` headers must survive the web-standard response
 * (Wallow-pu6a.3.1, risk (a)).
 *
 * `Headers` is the one place where the port can silently lose data: building a
 * response from an object literal collapses duplicate keys before `Headers`
 * ever sees them, and `Headers.set` after an `append` destroys every cookie
 * already written. Either mistake leaves a chunked session half-written and a
 * logout half-cleared, and neither shows up as an error — only as a session
 * that mysteriously fails to unseal.
 */
describe("multiple Set-Cookie headers", () => {
  it("emits one Set-Cookie per chunk plus the CSRF companion on a callback", async () => {
    const config: BffConfig = makeConfig("https://multi-setcookie-callback.example.com");

    const { res } = await completeCallback(config, { access_token: "a".repeat(6000) });

    const live: string[] = res.headers
      .getSetCookie()
      .filter((cookie: string): boolean => cookieValueOf(cookie) !== "");
    // Base chunk + at least one `.N` chunk + the CSRF companion.
    expect(live.length).toBeGreaterThanOrEqual(3);
    expect(new Set(live.map((cookie: string): string => cookieNameOf(cookie))).size).toBe(
      live.length,
    );
    // The redirect still carries its Location: the cookie appends must not have
    // been built in a way that clobbers the rest of the response headers.
    expect(res.headers.get("location")).toBe("/dashboard");
  });

  it("clears the base cookie, the CSRF companion, and every chunk on logout", async () => {
    const config: BffConfig = makeConfig("https://multi-setcookie-logout.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const store: SessionStore = new CookieSessionStore({ password: config.cookiePassword });
    const session: BffSession = makeSession({ accessToken: "a".repeat(6000) });

    // Write a multi-chunk session, then hand its cookies back on a logout.
    const written: Headers = new Headers();
    const ref: string = await writeSession(written, config, store, session);
    const chunkNames: string[] = written
      .getSetCookie()
      .filter((cookie: string): boolean => cookieValueOf(cookie) !== "")
      .map((cookie: string): string => cookieNameOf(cookie));
    expect(chunkNames.length).toBeGreaterThan(1);

    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async (): Promise<DiscoveryDoc> => doc,
      }),
    );
    const handle = makeHandle(createBffHandlers(config, store));

    const cookie: string = `${cookieHeaderFromHeaders(written)}; wallow_bff-csrf=${CSRF_FIXTURE_TOKEN}`;
    const res: Response = await handle(logoutRequest(cookie));

    expect(res.status).toBe(302);
    const cleared: string[] = res.headers.getSetCookie();
    // Every live cookie is cleared with its own Set-Cookie line — the base, each
    // chunk, and the readable CSRF companion.
    for (const name of [...chunkNames, "wallow_bff-csrf"]) {
      const line: string | undefined = cleared.find(
        (candidate: string): boolean => cookieNameOf(candidate) === name,
      );
      expect(line, `expected a clearing Set-Cookie for ${name}`).toBeDefined();
      expect(cookieValueOf(line ?? "")).toBe("");
    }
    // The whole multi-chunk reference is what gets revoked server-side, not
    // just the base cookie's slice of it.
    expect(ref.length).toBeGreaterThan(3800);
  });
});

describe("readSession/writeSession store threading", () => {
  it("round-trips a session through an injected CookieSessionStore", async () => {
    const config: BffConfig = makeConfig("https://store-roundtrip.example.com");
    const store: SessionStore = new CookieSessionStore({
      password: config.cookiePassword,
    });
    const session: BffSession = makeSession();

    const headers: Headers = new Headers();
    await writeSession(headers, config, store, session);
    const restored: BffSession | null = await readSession(
      new Request("http://localhost/read", {
        headers: { cookie: cookieHeaderFromHeaders(headers) },
      }),
      config,
      store,
    );

    expect(restored).toEqual(session);
  });

  it("round-trips a chunked session, so the iron seal survives reassembly", async () => {
    // Bead 1.6/F7 lives in session.ts and is untouched by this port, but the
    // whole read/write transport around it was rewritten: a reference that
    // reassembles even one byte wrong unseals as `null` and reads as "logged
    // out", so the round trip is re-pinned here through the new shape.
    const config: BffConfig = makeConfig("https://store-roundtrip-chunked.example.com");
    const store: SessionStore = new CookieSessionStore({
      password: config.cookiePassword,
    });
    const session: BffSession = makeSession({
      accessToken: "a".repeat(2600),
      idToken: makeIdToken({ sub: "user-123", padding: "b".repeat(2600) }),
    });

    const headers: Headers = new Headers();
    const ref: string = await writeSession(headers, config, store, session);
    const restored: BffSession | null = await readSession(
      new Request("http://localhost/read", {
        headers: { cookie: cookieHeaderFromHeaders(headers) },
      }),
      config,
      store,
    );

    expect(
      headers.getSetCookie().filter((c: string): boolean => cookieValueOf(c) !== "").length,
    ).toBeGreaterThan(1);
    expect(ref.length).toBeGreaterThan(3800);
    expect(restored).toEqual(session);
  });

  it("clears stale higher-index chunks when a shorter session replaces a longer one", async () => {
    const config: BffConfig = makeConfig("https://store-stale-chunks.example.com");
    const store: SessionStore = new CookieSessionStore({
      password: config.cookiePassword,
    });

    const long: Headers = new Headers();
    await writeSession(long, config, store, makeSession({ accessToken: "a".repeat(6000) }));
    const short: Headers = new Headers();
    await writeSession(short, config, store, makeSession());

    // The short write must leave the browser holding the base cookie only: a
    // surviving `.1` chunk would be concatenated onto the next read and corrupt
    // the reference beyond unsealing.
    const live: string[] = short
      .getSetCookie()
      .filter((cookie: string): boolean => cookieValueOf(cookie) !== "")
      .map((cookie: string): string => cookieNameOf(cookie));
    expect(live).toEqual(["wallow_bff"]);
    const clearedStale: string[] = short
      .getSetCookie()
      .filter((cookie: string): boolean => cookieValueOf(cookie) === "")
      .map((cookie: string): string => cookieNameOf(cookie));
    expect(clearedStale).toContain("wallow_bff.1");
  });
});

/**
 * OIDC front-channel logout (Wallow-whsz).
 *
 * The OP's logout page loads `/bff/frontchannel-logout?iss=...&sid=...` in a
 * hidden iframe when the SSO session ends. The handler destroys the local
 * session ONLY when both `iss` and `sid` match; every other GET answers the
 * same 200 page so a prober learns nothing about session state. It is
 * deliberately outside the CSRF gate — the notification is a cross-site GET by
 * design, and the sid requirement is what stops a forged teardown.
 */
describe("frontchannel logout handler", () => {
  const FC_PATH: string = "http://localhost/bff/frontchannel-logout";

  function fcSetup(issuer: string): {
    handlers: BffHandlers;
    destroyed: string[];
    config: BffConfig;
  } {
    const config: BffConfig = makeConfig(issuer);
    const { store, destroyed } = makeRecordingStore(config.cookiePassword);
    return { handlers: createBffHandlers(config, store), destroyed, config };
  }

  it("destroys the session and clears cookies when iss and sid both match", async () => {
    const { handlers, destroyed, config } = fcSetup("https://fc-match.example.com");
    const sealed: string = await sealSession(
      makeSession({ sid: "sid-abc" }),
      config.cookiePassword,
    );

    const res: Response = await handlers.frontchannelLogout(
      new Request(`${FC_PATH}?iss=${encodeURIComponent(config.issuer)}&sid=sid-abc`, {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    expect(res.headers.get("content-type") ?? "").toContain("text/html");
    expect(res.headers.get("cache-control")).toBe("no-store");
    expect(destroyed).toEqual([sealed]);
    const cleared: string | undefined = setCookieFor(res, "wallow_bff");
    expect(cleared).toBeDefined();
    expect(cookieValueOf(cleared ?? "")).toBe("");
  });

  it("treats a trailing slash on iss as the same issuer", async () => {
    const { handlers, destroyed, config } = fcSetup("https://fc-slash.example.com");
    const sealed: string = await sealSession(
      makeSession({ sid: "sid-abc" }),
      config.cookiePassword,
    );

    const res: Response = await handlers.frontchannelLogout(
      new Request(`${FC_PATH}?iss=${encodeURIComponent(`${config.issuer}/`)}&sid=sid-abc`, {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    expect(destroyed).toEqual([sealed]);
  });

  it("answers 200 without destroying anything when sid does not match", async () => {
    const { handlers, destroyed, config } = fcSetup("https://fc-badsid.example.com");
    const sealed: string = await sealSession(
      makeSession({ sid: "sid-abc" }),
      config.cookiePassword,
    );

    const res: Response = await handlers.frontchannelLogout(
      new Request(`${FC_PATH}?iss=${encodeURIComponent(config.issuer)}&sid=some-other-sid`, {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    expect(destroyed).toEqual([]);
    expect(setCookieFor(res, "wallow_bff")).toBeUndefined();
  });

  it("answers 200 without destroying anything when iss does not match", async () => {
    const { handlers, destroyed, config } = fcSetup("https://fc-badiss.example.com");
    const sealed: string = await sealSession(
      makeSession({ sid: "sid-abc" }),
      config.cookiePassword,
    );

    const res: Response = await handlers.frontchannelLogout(
      new Request(`${FC_PATH}?iss=${encodeURIComponent("https://evil.example.com")}&sid=sid-abc`, {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    expect(destroyed).toEqual([]);
  });

  it("answers 200 without destroying anything when the session carries no sid", async () => {
    // A session minted before the OP issued sids can never match a
    // notification; it must survive rather than be torn down on a guess.
    const { handlers, destroyed, config } = fcSetup("https://fc-nosid.example.com");
    const sealed: string = await sealSession(makeSession(), config.cookiePassword);

    const res: Response = await handlers.frontchannelLogout(
      new Request(`${FC_PATH}?iss=${encodeURIComponent(config.issuer)}&sid=sid-abc`, {
        headers: { cookie: `wallow_bff=${sealed}` },
      }),
    );

    expect(res.status).toBe(200);
    expect(destroyed).toEqual([]);
  });

  it("answers 200 when there is no session at all", async () => {
    const { handlers, destroyed, config } = fcSetup("https://fc-nosession.example.com");

    const res: Response = await handlers.frontchannelLogout(
      new Request(`${FC_PATH}?iss=${encodeURIComponent(config.issuer)}&sid=sid-abc`),
    );

    expect(res.status).toBe(200);
    expect(destroyed).toEqual([]);
  });

  it("answers 405 with Allow: GET for a non-GET request", async () => {
    const { handlers, destroyed } = fcSetup("https://fc-method.example.com");

    const res: Response = await handlers.frontchannelLogout(
      new Request(FC_PATH, { method: "POST" }),
    );

    expect(res.status).toBe(405);
    expect(res.headers.get("allow")).toBe("GET");
    expect(destroyed).toEqual([]);
  });
});

describe("callback sid capture", () => {
  it("stores the id_token's sid claim on the session for front-channel matching", async () => {
    const config: BffConfig = makeConfig("https://cb-sid.example.com");
    const tx: LoginTx = { state: "st-s", nonce: "no-s", verifier: "ver-s", returnTo: "/" };
    const sealed: string = await sealTx(tx, config.cookiePassword);

    authorizationCodeGrantMock.mockResolvedValue({
      access_token: "at",
      refresh_token: "rt",
      id_token: makeIdToken({ sub: "user-123", sid: "sid-from-op" }),
      expires_in: 3600,
      token_type: "Bearer",
    });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")));
    const handle = makeHandle(createBffHandlers(config));

    const cbRes: Response = await handle(
      new Request("http://localhost/bff/callback?code=code-s&state=st-s", {
        headers: { cookie: `wallow_bff_tx=${sealed}` },
      }),
    );
    expect(cbRes.status).toBe(302);

    const session: BffSession | null = await readSession(
      new Request("http://localhost/bff/user", { headers: { cookie: cookieHeaderFrom(cbRes) } }),
      config,
      new CookieSessionStore({ password: config.cookiePassword }),
    );
    expect(session?.sid).toBe("sid-from-op");
  });

  it("leaves sid unset when the id_token carries none", async () => {
    const config: BffConfig = makeConfig("https://cb-nosid.example.com");
    const tx: LoginTx = { state: "st-n", nonce: "no-n", verifier: "ver-n", returnTo: "/" };
    const sealed: string = await sealTx(tx, config.cookiePassword);

    authorizationCodeGrantMock.mockResolvedValue({
      access_token: "at",
      id_token: makeIdToken({ sub: "user-123" }),
      expires_in: 3600,
      token_type: "Bearer",
    });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("unexpected token-endpoint fetch")));
    const handle = makeHandle(createBffHandlers(config));

    const cbRes: Response = await handle(
      new Request("http://localhost/bff/callback?code=code-n&state=st-n", {
        headers: { cookie: `wallow_bff_tx=${sealed}` },
      }),
    );
    expect(cbRes.status).toBe(302);

    const session: BffSession | null = await readSession(
      new Request("http://localhost/bff/user", { headers: { cookie: cookieHeaderFrom(cbRes) } }),
      config,
      new CookieSessionStore({ password: config.cookiePassword }),
    );
    expect(session).not.toBeNull();
    expect(session?.sid).toBeUndefined();
  });
});

/**
 * The `COOKIE_SAMESITE` hardening knob (RFC 10017 §6.1.3.2): `strict` moves the
 * session and CSRF cookies to `SameSite=Strict`, while the login-transaction
 * cookie is pinned `Lax` — it has to ride the cross-site top-level redirect
 * back from the IdP, or `state` validation would reject every login.
 */
describe("cookieSameSite", () => {
  it("defaults the session and CSRF cookies to SameSite=Lax", async () => {
    const config: BffConfig = makeConfig("https://samesite-default.example.com");

    const { res } = await completeCallback(config);

    expect(res.status).toBe(302);
    expect(attributeValue(setCookieFor(res, "wallow_bff") ?? "", "SameSite")).toBe("Lax");
    expect(attributeValue(setCookieFor(res, "wallow_bff-csrf") ?? "", "SameSite")).toBe("Lax");
  });

  it("writes the session and CSRF cookies SameSite=Strict when configured", async () => {
    const config: BffConfig = makeConfig("https://samesite-strict.example.com", {
      cookieSameSite: "strict",
    });

    const { res } = await completeCallback(config);

    expect(res.status).toBe(302);
    expect(attributeValue(setCookieFor(res, "wallow_bff") ?? "", "SameSite")).toBe("Strict");
    expect(attributeValue(setCookieFor(res, "wallow_bff-csrf") ?? "", "SameSite")).toBe("Strict");
  });

  it("keeps the tx cookie SameSite=Lax even under a strict configuration", async () => {
    const config: BffConfig = makeConfig("https://samesite-tx.example.com", {
      cookieSameSite: "strict",
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

    const res: Response = await handle(new Request("http://localhost/bff/login"));

    expect(res.status).toBe(302);
    const txCookie: string | undefined = setCookieFor(res, "wallow_bff_tx");
    expect(txCookie).toBeDefined();
    expect(attributeValue(txCookie ?? "", "SameSite")).toBe("Lax");
  });
});
