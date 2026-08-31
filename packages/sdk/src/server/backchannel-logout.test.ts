import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { exportJWK, generateKeyPair, SignJWT, type JWK } from "jose";

import type { BffConfig } from "./config";
import { createBackchannelLogoutHandler } from "./backchannel-logout";
import { type BffHandler } from "./handlers";
import { type BffSession } from "./session";
import { type SessionStore } from "./store/types";

/**
 * Hermetic mock of openid-client, mirroring handlers.test.ts: `discover()`
 * resolves through `discovery()`, whose stub advertises a jwks_uri rooted at
 * the requested metadata URL's origin. RFC 7009 revocation is delegated to
 * `tokenRevocation`, observed through the hoisted mock. The logout token itself
 * is verified by REAL jose crypto against a JWKS served through a stubbed
 * `fetch` — the signature checks in these specs are not simulated.
 */
const { tokenRevocationMock } = vi.hoisted(() => ({
  tokenRevocationMock: vi.fn(() => Promise.resolve()),
}));

vi.mock("openid-client", () => ({
  discovery: vi.fn((server: URL) => {
    const origin: string = new URL(server).origin;
    return Promise.resolve({
      serverMetadata: (): Record<string, unknown> => ({
        issuer: origin,
        authorization_endpoint: `${origin}/connect/authorize`,
        token_endpoint: `${origin}/connect/token`,
        jwks_uri: `${origin}/.well-known/jwks`,
        backchannel_logout_supported: true,
      }),
    });
  }),
  allowInsecureRequests: vi.fn(),
  tokenRevocation: tokenRevocationMock,
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

/** The event URI a logout token's `events` claim must carry (Back-Channel Logout 1.0). */
const LOGOUT_EVENT: string = "http://schemas.openid.net/event/backchannel-logout";

const CLIENT_ID: string = "web-bff";
const SID: string = "op-session-abc";
const SUBJECT: string = "user-123";

/** The signing key pair every issuer in this suite publishes, generated once. */
let signingKey: CryptoKey;
let publicJwk: JWK;

beforeAll(async () => {
  const pair: { privateKey: CryptoKey; publicKey: CryptoKey } = await generateKeyPair("RS256", {
    extractable: true,
  });
  signingKey = pair.privateKey;
  publicJwk = await exportJWK(pair.publicKey);
  publicJwk.alg = "RS256";
});

/** Serve the suite's JWKS to jose's remote key set through the global fetch. */
function stubJwksFetch(): void {
  vi.stubGlobal(
    "fetch",
    vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ keys: [publicJwk] }), {
          status: 200,
          headers: { "content-type": "application/json" },
        }),
      ),
    ),
  );
}

/**
 * Build a config. Each test passes a unique issuer so the module-level
 * discovery cache in oidc.ts never leaks a stubbed doc across tests.
 */
function makeConfig(issuer: string, overrides: Partial<BffConfig> = {}): BffConfig {
  return {
    issuer,
    clientId: CLIENT_ID,
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

function makeSession(overrides: Partial<BffSession> = {}): BffSession {
  return {
    sessionId: "sess-fixture-000",
    accessToken: "access-token-abc-123",
    refreshToken: "refresh-token-def-456",
    expiresAt: Date.now() + 60_000,
    user: { sub: SUBJECT },
    sid: SID,
    version: 1,
    ...overrides,
  };
}

/** A store whose revocation calls are recorded and answered from a fixture. */
interface RevokingStore extends SessionStore {
  revokeBySid: ReturnType<typeof vi.fn<(sid: string) => Promise<BffSession[]>>>;
  revokeBySubject: ReturnType<typeof vi.fn<(sub: string) => Promise<BffSession[]>>>;
}

function revokingStore(revoked: BffSession[] = [makeSession()]): RevokingStore {
  return {
    read: vi.fn(() => Promise.resolve(null)),
    write: vi.fn(() => Promise.resolve("ref")),
    destroy: vi.fn(() => Promise.resolve()),
    withRefreshLock: <T>(_ref: string, fn: () => Promise<T>): Promise<T | undefined> => fn(),
    revokeBySid: vi.fn(() => Promise.resolve(revoked)),
    revokeBySubject: vi.fn(() => Promise.resolve(revoked)),
  };
}

/** A store exposing neither revocation method, like the cookie store. */
function inertStore(): SessionStore {
  return {
    read: vi.fn(() => Promise.resolve(null)),
    write: vi.fn(() => Promise.resolve("ref")),
    destroy: vi.fn(() => Promise.resolve()),
    withRefreshLock: <T>(_ref: string, fn: () => Promise<T>): Promise<T | undefined> => fn(),
  };
}

interface TokenShape {
  issuer?: string;
  audience?: string;
  events?: unknown;
  sid?: string;
  sub?: string;
  nonce?: string;
  typ?: string;
  expiresIn?: string;
  key?: CryptoKey;
}

/** Mint a logout token; every field defaults to the spec-valid shape. */
async function mintLogoutToken(issuer: string, shape: TokenShape = {}): Promise<string> {
  const payload: Record<string, unknown> = {
    jti: "token-id-000",
    events: "events" in shape ? shape.events : { [LOGOUT_EVENT]: {} },
  };
  const sid: string | undefined = "sid" in shape ? shape.sid : SID;
  if (sid !== undefined) {
    payload.sid = sid;
  }
  if (shape.sub !== undefined) {
    payload.sub = shape.sub;
  }
  if (shape.nonce !== undefined) {
    payload.nonce = shape.nonce;
  }
  return new SignJWT(payload)
    .setProtectedHeader({ alg: "RS256", typ: shape.typ ?? "logout+jwt" })
    .setIssuer(shape.issuer ?? issuer)
    .setAudience(shape.audience ?? CLIENT_ID)
    .setIssuedAt()
    .setExpirationTime(shape.expiresIn ?? "2m")
    .sign(shape.key ?? signingKey);
}

function postLogoutToken(handler: BffHandler, token: string | null): Promise<Response> {
  return handler(
    new Request("https://app.example.com/bff/backchannel-logout", {
      method: "POST",
      headers: { "content-type": "application/x-www-form-urlencoded" },
      body: token === null ? "" : new URLSearchParams({ logout_token: token }).toString(),
    }),
  );
}

describe("createBackchannelLogoutHandler — success paths", () => {
  it("destroys the sid's session and answers 200 with cache-control: no-store", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-901.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    const response: Response = await postLogoutToken(handler, await mintLogoutToken(issuer));

    expect(response.status).toBe(200);
    expect(response.headers.get("cache-control")).toBe("no-store");
    expect(store.revokeBySid).toHaveBeenCalledWith(SID);
    expect(store.revokeBySubject).not.toHaveBeenCalled();
  });

  it("accepts a token whose iss carries the trailing slash of Uri.AbsoluteUri", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-902.example.com";
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), revokingStore());

    const response: Response = await postLogoutToken(
      handler,
      await mintLogoutToken(issuer, { issuer: `${issuer}/` }),
    );

    expect(response.status).toBe(200);
  });

  it("revokes each destroyed session's refresh token upstream via RFC 7009", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-903.example.com";
    const handler: BffHandler = createBackchannelLogoutHandler(
      makeConfig(issuer),
      revokingStore([makeSession({ refreshToken: "refresh-to-revoke" })]),
    );

    const response: Response = await postLogoutToken(handler, await mintLogoutToken(issuer));

    expect(response.status).toBe(200);
    expect(tokenRevocationMock).toHaveBeenCalledTimes(1);
    expect(tokenRevocationMock).toHaveBeenCalledWith(expect.anything(), "refresh-to-revoke", {
      token_type_hint: "refresh_token",
    });
  });

  it("still answers 200 when the upstream RFC 7009 revocation fails", async () => {
    stubJwksFetch();
    tokenRevocationMock.mockRejectedValueOnce(new Error("revocation endpoint down"));
    const issuer: string = "https://op-904.example.com";
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), revokingStore());

    const response: Response = await postLogoutToken(handler, await mintLogoutToken(issuer));

    expect(response.status).toBe(200);
  });

  it("answers 200 for an unknown sid — already-gone is success", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-905.example.com";
    const store: RevokingStore = revokingStore([]);
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    const response: Response = await postLogoutToken(handler, await mintLogoutToken(issuer));

    expect(response.status).toBe(200);
    expect(tokenRevocationMock).not.toHaveBeenCalled();
  });

  it("falls back to revokeBySubject for a sid-less token carrying sub", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-906.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    const response: Response = await postLogoutToken(
      handler,
      await mintLogoutToken(issuer, { sid: undefined, sub: SUBJECT }),
    );

    expect(response.status).toBe(200);
    expect(store.revokeBySubject).toHaveBeenCalledWith(SUBJECT);
    expect(store.revokeBySid).not.toHaveBeenCalled();
  });

  it("answers 200 as a no-op when the store exposes no revocation method", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-907.example.com";
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), inertStore());

    const response: Response = await postLogoutToken(handler, await mintLogoutToken(issuer));

    expect(response.status).toBe(200);
  });
});

describe("createBackchannelLogoutHandler — invalid tokens", () => {
  /** Assert the uniform rejection: 400, `{error}`, no-store, nothing revoked. */
  async function expectInvalid(response: Response, store: RevokingStore): Promise<void> {
    expect(response.status).toBe(400);
    expect(response.headers.get("cache-control")).toBe("no-store");
    expect(await response.json()).toEqual({ error: "invalid_request" });
    expect(store.revokeBySid).not.toHaveBeenCalled();
    expect(store.revokeBySubject).not.toHaveBeenCalled();
  }

  it("rejects a missing logout_token", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-910.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(await postLogoutToken(handler, null), store);
  });

  it("rejects a token for another audience", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-911.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(handler, await mintLogoutToken(issuer, { audience: "someone-else" })),
      store,
    );
  });

  it("rejects a token from a foreign issuer", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-912.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(
        handler,
        await mintLogoutToken(issuer, { issuer: "https://evil.example.com" }),
      ),
      store,
    );
  });

  it("rejects a token without the events claim", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-913.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(handler, await mintLogoutToken(issuer, { events: undefined })),
      store,
    );
  });

  it("rejects a token whose events claim lacks the back-channel logout event", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-914.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(
        handler,
        await mintLogoutToken(issuer, { events: { "urn:other-event": {} } }),
      ),
      store,
    );
  });

  it("rejects a token carrying a nonce — the mark of a replayed id token", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-915.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(handler, await mintLogoutToken(issuer, { nonce: "replayed-nonce" })),
      store,
    );
  });

  it("rejects a token with neither sid nor sub", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-916.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(handler, await mintLogoutToken(issuer, { sid: undefined })),
      store,
    );
  });

  it("rejects an expired token", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-917.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(handler, await mintLogoutToken(issuer, { expiresIn: "-5m" })),
      store,
    );
  });

  it("rejects a token signed by a key the issuer does not publish", async () => {
    stubJwksFetch();
    const foreign: { privateKey: CryptoKey } = await generateKeyPair("RS256");
    const issuer: string = "https://op-918.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(handler, await mintLogoutToken(issuer, { key: foreign.privateKey })),
      store,
    );
  });

  it("rejects a token whose typ header is not logout+jwt", async () => {
    stubJwksFetch();
    const issuer: string = "https://op-919.example.com";
    const store: RevokingStore = revokingStore();
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), store);

    await expectInvalid(
      await postLogoutToken(handler, await mintLogoutToken(issuer, { typ: "JWT" })),
      store,
    );
  });
});

describe("createBackchannelLogoutHandler — transport", () => {
  it("answers GET with 405, an Allow: POST header, and no-store", async () => {
    const issuer: string = "https://op-920.example.com";
    const handler: BffHandler = createBackchannelLogoutHandler(makeConfig(issuer), revokingStore());

    const response: Response = await handler(
      new Request("https://app.example.com/bff/backchannel-logout", { method: "GET" }),
    );

    expect(response.status).toBe(405);
    expect(response.headers.get("allow")).toBe("POST");
    expect(response.headers.get("cache-control")).toBe("no-store");
  });
});
