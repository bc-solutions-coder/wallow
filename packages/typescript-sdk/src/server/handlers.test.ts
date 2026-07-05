import { createApp, toWebHandler, type App } from "h3";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { BffConfig } from "./config";
import { createBffHandlers, type BffHandlers } from "./handlers";
import type { DiscoveryDoc } from "./oidc";
import { sealSession, type BffSession } from "./session";
import { sealTx, type LoginTx } from "./txstate";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
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
    accessToken: "access-token-abc",
    refreshToken: "refresh-token-def",
    idToken: makeIdToken({ sub: "user-123" }),
    expiresAt: Date.now() + 3_600_000,
    user: { sub: "user-123", email: "user@example.com", name: "Test User" },
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

  it("exchanges the code and 302s to returnTo on a valid callback", async () => {
    const config: BffConfig = makeConfig("https://cb-ok.example.com");
    const doc: DiscoveryDoc = makeDoc(config.issuer);
    const tx: LoginTx = {
      state: "st-1",
      nonce: "no-1",
      verifier: "ver-1",
      returnTo: "/welcome",
    };
    const sealed: string = await sealTx(tx, config.cookiePassword);

    vi.stubGlobal(
      "fetch",
      vi.fn((input: unknown) => {
        const requestUrl: string = String(input);
        if (requestUrl.includes(".well-known")) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: async (): Promise<DiscoveryDoc> => doc,
          });
        }
        return Promise.resolve({
          ok: true,
          status: 200,
          json: async () => ({
            access_token: "at",
            refresh_token: "rt",
            id_token: makeIdToken({ sub: "user-123", email: "u@e.com" }),
            expires_in: 3600,
            token_type: "Bearer",
          }),
        });
      }),
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

describe("logout handler", () => {
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
