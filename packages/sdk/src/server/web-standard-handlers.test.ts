/**
 * The server entry is a web-standard handler factory (Wallow-pu6a.3.4).
 *
 * `server/handlers.ts` and `server/proxy.ts` were ported off h3 to
 * `Request`/`Response` in Wallow-pu6a.3.2/3.3. What that port has to keep true
 * is exercised here: every handler answers a bare WHATWG `Request` with a bare
 * WHATWG `Response`, and the handler TYPES are nameable by consumers — h3's
 * `EventHandler` (which the hosts annotated their handlers with beforehand) is
 * no longer available to them.
 *
 * The "no h3 anywhere" half of this claim used to be a source sweep over the
 * manifest and every `.ts` file under `src/` and `apps/`. It is not a test:
 * pnpm's strict `node_modules` already fails a build that imports a dependency
 * the manifest does not declare, so an h3 import cannot reach a passing run.
 */
import { describe, expect, it } from "vitest";

import {
  createApiProxy,
  createBffHandlers,
  CookieSessionStore,
  type ApiProxyHandler,
  type BffConfig,
  type BffHandler,
  type BffHandlers,
  type SessionStore,
} from "./index";

/** A config that needs no environment and no live OP. */
function makeConfig(): BffConfig {
  return {
    issuer: "https://issuer.h3-free.example.com",
    clientId: "web-bff",
    clientSecret: "s3cret",
    redirectUri: "https://app.example.com/bff/callback",
    postLogoutRedirectUri: "https://app.example.com/",
    scopes: ["openid", "profile", "email", "offline_access"],
    apiBaseUrl: "https://api.example.com",
    cookieName: "wallow_bff",
    cookiePassword: "x".repeat(32),
    sessionTtlSeconds: 86_400,
    cookieSecure: true,
  };
}

function makeStore(): SessionStore {
  return new CookieSessionStore({ password: "x".repeat(32) });
}

describe("the server entry stays functional on the web-standard API", () => {
  it("exposes the four BFF handlers as (Request) => Promise<Response>", async () => {
    const handlers: BffHandlers = createBffHandlers(makeConfig(), makeStore());
    // Named as the exported handler type: with h3 gone, `EventHandler` is no
    // longer available to hosts, so the SDK must name this shape itself.
    const user: BffHandler = handlers.user;

    const response: Response = await user(new Request("https://app.example.com/bff/user"));

    expect(response).toBeInstanceOf(Response);
    expect(response.status).toBe(401);
  });

  it("exposes the api proxy as (Request) => Promise<Response>", async () => {
    const proxy: ApiProxyHandler = createApiProxy(makeConfig(), makeStore());

    const response: Response = await proxy(
      new Request("https://app.example.com/api/notifications"),
    );

    expect(response).toBeInstanceOf(Response);
    expect(response.status).toBe(401);
  });

  it("answers a request built with no framework event object at all", async () => {
    const handlers: BffHandlers = createBffHandlers(makeConfig(), makeStore());

    // Nothing h3-shaped is constructed anywhere in this call: a bare WHATWG
    // Request goes in and a bare WHATWG Response comes out. Under h3 this
    // needed createApp() + app.use() + toWebHandler().
    const response: Response = await handlers.logout(
      new Request("https://app.example.com/bff/logout", { method: "GET" }),
    );

    expect(response.status).toBe(405);
    expect(response.headers.get("allow")).toBe("POST");
  });
});
