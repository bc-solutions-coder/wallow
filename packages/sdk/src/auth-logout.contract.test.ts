import { afterEach, describe, expect, it, vi } from "vitest";

import { logout } from "./auth";
import { setCsrfToken } from "./csrf";
import type { BffConfig } from "./server/config";
import { createBffHandlers, type BffHandlers } from "./server/handlers";
import { CSRF_HEADER } from "./server/proxy";
import { sealSession, type BffSession } from "./server/session";
import { CookieSessionStore } from "./server/store/cookie";
import type { SessionStore } from "./server/store/types";

/**
 * Contract spec for Wallow-pu6a.3.9: the browser helper `logout()` driven
 * against the REAL ported `/bff/logout` handler, not a hand-written stub of it.
 *
 * The two sides were regressed apart by Wallow-pu6a.3.2, which hardened the
 * handler to require `POST` + `x-csrf-token` (F12a) while the browser helper
 * still navigated with a GET. A unit spec with a mocked `fetch` cannot catch
 * that class of drift — only running the helper's real request through the real
 * handler can. The `fetch` global is stubbed with a transport that hands the
 * request straight to the handler, so this asserts the acceptance criteria
 * end-to-end without a live backend: the session cookie is cleared, the
 * server-side session is destroyed, and the browser lands on the IdP
 * end-session URL.
 */

/** Hermetic openid-client stub: endpoints derived from the requested issuer. */
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
  buildAuthorizationUrl: vi.fn(),
  authorizationCodeGrant: vi.fn(),
  refreshTokenGrant: vi.fn(),
  fetchUserInfo: vi.fn(),
  skipSubjectCheck: Symbol("skipSubjectCheck"),
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
  setCsrfToken(null);
});

/** The origin the SPA is served from; relative helper URLs resolve against it. */
const APP_ORIGIN: string = "https://app.example.com";

/** The CSRF token bound to the session fixture. */
const CSRF_FIXTURE_TOKEN: string = "csrf-fixture-token-aaaaaaaaaaaaaaaa";

/** A writable stand-in for the `location` global. */
interface FakeLocation {
  href: string;
}

/** Config for a BFF whose IdP endpoints hang off a per-test issuer origin. */
function makeConfig(issuer: string): BffConfig {
  return {
    issuer,
    clientId: "web-bff",
    clientSecret: "s3cret",
    redirectUri: `${APP_ORIGIN}/bff/callback`,
    postLogoutRedirectUri: `${APP_ORIGIN}/`,
    scopes: ["openid", "profile", "email", "offline_access"],
    apiBaseUrl: "https://api.example.com",
    cookieName: "wallow_bff",
    cookiePassword: "x".repeat(32),
    sessionTtlSeconds: 86400,
    cookieSecure: true,
  };
}

/** A minimal, unsigned JWT — the BFF trusts the TLS channel, not the signature. */
function makeIdToken(payload: Record<string, unknown>): string {
  return `header.${Buffer.from(JSON.stringify(payload)).toString("base64url")}.signature`;
}

function makeSession(): BffSession {
  return {
    sessionId: "sess-fixture-000",
    accessToken: "access-token-abc",
    refreshToken: "refresh-token-def",
    idToken: makeIdToken({ sub: "user-123" }),
    expiresAt: Date.now() + 3_600_000,
    user: { sub: "user-123", email: "user@example.com", name: "Test User" },
    version: 1,
    csrfToken: CSRF_FIXTURE_TOKEN,
  };
}

/**
 * A store that behaves like the real cookie store but records every `ref`
 * handed to `destroy`, so a test can assert logout tore the session down.
 */
function makeRecordingStore(password: string): { store: SessionStore; destroyed: string[] } {
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

/**
 * Stand in for the browser's network stack: resolve the helper's relative URL
 * against the app origin, attach the cookie jar the way `credentials: "include"`
 * makes a browser do, and dispatch to the real handler.
 */
function stubBffTransport(
  handlers: BffHandlers,
  cookieHeader: string,
): { fetchMock: ReturnType<typeof vi.fn>; responses: Response[] } {
  const responses: Response[] = [];
  const fetchMock: ReturnType<typeof vi.fn> = vi.fn(
    async (input: unknown, init?: RequestInit): Promise<Response> => {
      // The session cookie only rides along when the caller asks for it; a
      // logout sent without credentials would be an anonymous 403.
      expect(init?.credentials).toBe("include");

      const url: URL = new URL(String(input), APP_ORIGIN);
      const headers: Headers = new Headers(init?.headers as HeadersInit | undefined);
      headers.set("cookie", cookieHeader);

      const response: Response =
        url.pathname === "/bff/logout"
          ? await handlers.logout(new Request(url, { method: init?.method ?? "GET", headers }))
          : new Response(null, { status: 404 });

      responses.push(response);
      return response;
    },
  );
  vi.stubGlobal("fetch", fetchMock);
  return { fetchMock, responses };
}

/** Every `Set-Cookie` the response wrote for `name`, if any. */
function setCookieFor(response: Response, name: string): string | undefined {
  return response.headers.getSetCookie().find((cookie: string) => cookie.startsWith(`${name}=`));
}

/** Prepare a logged-in browser: sealed session cookie, csrf cookie, location. */
async function signedIn(issuer: string): Promise<{
  config: BffConfig;
  handlers: BffHandlers;
  destroyed: string[];
  sealed: string;
  cookieHeader: string;
  location: FakeLocation;
}> {
  const config: BffConfig = makeConfig(issuer);
  const { store, destroyed } = makeRecordingStore(config.cookiePassword);
  const handlers: BffHandlers = createBffHandlers(config, store);
  const sealed: string = await sealSession(makeSession(), config.cookiePassword);
  const cookieHeader: string = `${config.cookieName}=${sealed}; ${config.cookieName}-csrf=${CSRF_FIXTURE_TOKEN}`;

  const location: FakeLocation = { href: "" };
  vi.stubGlobal("location", location);
  vi.stubGlobal("document", { cookie: `${config.cookieName}-csrf=${CSRF_FIXTURE_TOKEN}` });

  return { config, handlers, destroyed, sealed, cookieHeader, location };
}

describe("browser logout() against the ported /bff/logout handler", () => {
  it("rejects the old GET navigation with 405 and clears nothing (the regression under fix)", async () => {
    const { handlers, destroyed, cookieHeader } = await signedIn("https://logout-get.example.com");

    const response: Response = await handlers.logout(
      new Request(`${APP_ORIGIN}/bff/logout`, { headers: { cookie: cookieHeader } }),
    );

    expect(response.status).toBe(405);
    expect(response.headers.get("allow")).toBe("POST");
    expect(response.headers.getSetCookie()).toEqual([]);
    expect(destroyed).toEqual([]);
  });

  it("completes the logout and lands the browser on the IdP end-session URL", async () => {
    const issuer: string = "https://logout-ok.example.com";
    const { handlers, cookieHeader, location } = await signedIn(issuer);
    stubBffTransport(handlers, cookieHeader);
    setCsrfToken(CSRF_FIXTURE_TOKEN);

    await logout();

    const target: URL = new URL(location.href);
    expect(target.origin + target.pathname).toBe(`${issuer}/connect/logout`);
    expect(target.searchParams.get("post_logout_redirect_uri")).toBe(`${APP_ORIGIN}/`);
    expect(target.searchParams.get("id_token_hint")).toBeTruthy();
  });

  it("clears the session and CSRF cookies", async () => {
    const { handlers, cookieHeader } = await signedIn("https://logout-cookies.example.com");
    const { responses } = stubBffTransport(handlers, cookieHeader);
    setCsrfToken(CSRF_FIXTURE_TOKEN);

    await logout();

    const response: Response = responses[0]!;
    expect(setCookieFor(response, "wallow_bff")).toMatch(/wallow_bff=;/u);
    expect(setCookieFor(response, "wallow_bff")).toMatch(/Max-Age=0/u);
    expect(setCookieFor(response, "wallow_bff-csrf")).toMatch(/Max-Age=0/u);
  });

  it("destroys the server-side session before the browser leaves the page", async () => {
    const { handlers, destroyed, sealed, cookieHeader } = await signedIn(
      "https://logout-destroy.example.com",
    );
    stubBffTransport(handlers, cookieHeader);
    setCsrfToken(CSRF_FIXTURE_TOKEN);

    await logout();

    expect(destroyed).toEqual([sealed]);
  });

  it("carries the token the handler's CSRF gate compares against", async () => {
    const { handlers, cookieHeader } = await signedIn("https://logout-token.example.com");
    const { fetchMock } = stubBffTransport(handlers, cookieHeader);
    setCsrfToken(CSRF_FIXTURE_TOKEN);

    await logout();

    const init: RequestInit = (fetchMock.mock.calls[0]?.[1] ?? {}) as RequestInit;
    expect(new Headers(init.headers).get(CSRF_HEADER)).toBe(CSRF_FIXTURE_TOKEN);
  });

  it("is refused, and destroys nothing, when the browser holds no CSRF token", async () => {
    const { handlers, destroyed, cookieHeader, location } = await signedIn(
      "https://logout-no-token.example.com",
    );
    const { responses } = stubBffTransport(handlers, cookieHeader);
    vi.stubGlobal("document", { cookie: "" });

    await expect(logout()).rejects.toThrowError(/403/u);

    expect(responses[0]?.status).toBe(403);
    expect(responses[0]?.headers.getSetCookie()).toEqual([]);
    expect(destroyed).toEqual([]);
    expect(location.href).toBe("");
  });
});
