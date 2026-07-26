import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import type { BffHandlers } from "@bc-solutions-coder/sdk/server";
import type { EventHandler } from "h3";

/**
 * SPIKE spec (Wallow-8w1h.2.3): prove the SDK's h3 BFF/proxy handlers can be
 * mounted and driven through a single web-handler bridge (`handleBffRequest`)
 * that a TanStack Start server route (or the design's sidecar fallback) forwards
 * to. These assert the exact gating behaviors the bead's acceptance and the C#
 * `BffFlowTests`/`DockerComposeFixture` depend on: `/health` -> {status:"ok"},
 * unauthenticated `/bff/user` -> 401, and login -> 302.
 *
 * OIDC discovery + authorize-URL construction are hermetically mocked at the
 * `openid-client` module boundary (the same pattern the SDK's own handler tests
 * use). The mock intercepts the bare `"openid-client"` import inside the built
 * SDK, so no live OP or network is touched — the login handler still produces a
 * real 302 to the advertised authorization endpoint.
 *
 * NOTE (RED): `src/lib/bff-server.ts` is a stub, so the behavioral cases below
 * fail because the port is missing; the GREEN phase of this same bead makes them
 * pass. HTTP-level "reachable as a Start server route" is proven separately by
 * the C# BffFlowTests over live infra; here we pin the framework-agnostic core.
 */
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
  buildAuthorizationUrl: vi.fn(
    (
      configuration: { serverMetadata: () => Record<string, unknown> },
      params: Record<string, string>,
    ): URL => {
      const endpoint: string = configuration.serverMetadata().authorization_endpoint as string;
      const url: URL = new URL(endpoint);
      for (const [key, value] of Object.entries(params)) {
        url.searchParams.set(key, String(value));
      }
      return url;
    },
  ),
  buildEndSessionUrl: vi.fn(
    (
      configuration: { serverMetadata: () => Record<string, unknown> },
      params: Record<string, string>,
    ): URL => {
      const endpoint: string = configuration.serverMetadata().end_session_endpoint as string;
      const url: URL = new URL(endpoint);
      for (const [key, value] of Object.entries(params)) {
        url.searchParams.set(key, String(value));
      }
      return url;
    },
  ),
  authorizationCodeGrant: vi.fn(),
  refreshTokenGrant: vi.fn(),
  fetchUserInfo: vi.fn(),
  skipSubjectCheck: Symbol("skipSubjectCheck"),
}));

const ISSUER = "https://login-spike.example.com";

let handleBffRequest: (request: Request) => Promise<Response>;
let bff: BffHandlers;
let apiProxy: EventHandler;

beforeAll(async () => {
  // bff-server constructs config/store/handlers at import (mirroring server.ts),
  // so the required BFF env must exist before the module is evaluated. Load it
  // dynamically after seeding process.env.
  Object.assign(process.env, {
    OIDC_ISSUER: ISSUER,
    OIDC_CLIENT_ID: "wallow-web",
    OIDC_CLIENT_SECRET: "test-client-secret",
    OIDC_REDIRECT_URI: "http://localhost:3000/bff/callback",
    OIDC_POST_LOGOUT_REDIRECT_URI: "http://localhost:3000/",
    BFF_API_BASE_URL: "http://localhost:5001",
    COOKIE_PASSWORD: "test-cookie-password-at-least-32-chars-long",
    COOKIE_SECURE: "false",
  });

  const mod = await import("./bff-server");
  handleBffRequest = mod.handleBffRequest;
  bff = mod.bff;
  apiProxy = mod.apiProxy;
});

afterEach(() => {
  vi.clearAllMocks();
});

describe("bff-server exported surface (ported from server.ts)", () => {
  it("exposes the four BFF handlers login/callback/user/logout", () => {
    expect(typeof bff.login).toBe("function");
    expect(typeof bff.callback).toBe("function");
    expect(typeof bff.user).toBe("function");
    expect(typeof bff.logout).toBe("function");
  });

  it("exposes the /api reverse proxy handler", () => {
    expect(typeof apiProxy).toBe("function");
  });
});

describe("handleBffRequest bridge (Start-server-route / sidecar equivalent)", () => {
  it("serves GET /health as {status:'ok'} for the E2E fixture readiness wait", async () => {
    const res: Response = await handleBffRequest(new Request("http://localhost/health"));

    expect(res.status).toBe(200);
    await expect(res.json()).resolves.toEqual({ status: "ok" });
  });

  it("returns 401 for unauthenticated GET /bff/user", async () => {
    const res: Response = await handleBffRequest(new Request("http://localhost/bff/user"));

    expect(res.status).toBe(401);
  });

  it("issues a 302 redirect to the OIDC authorize endpoint on GET /bff/login", async () => {
    const res: Response = await handleBffRequest(
      new Request("http://localhost/bff/login?returnTo=/dashboard"),
    );

    expect(res.status).toBe(302);
    const location: string = res.headers.get("location") ?? "";
    expect(location.startsWith(`${ISSUER}/connect/authorize`)).toBe(true);
    const authorizeUrl: URL = new URL(location);
    expect(authorizeUrl.searchParams.get("state")).toBeTruthy();
    // The transaction cookie must be planted so the callback can complete.
    expect(res.headers.get("set-cookie") ?? "").toContain("wallow_bff_tx");
  });

  it("auth-gates the /api proxy with a 401 when there is no session", async () => {
    const res: Response = await handleBffRequest(new Request("http://localhost/api/notifications"));

    expect(res.status).toBe(401);
  });
});
