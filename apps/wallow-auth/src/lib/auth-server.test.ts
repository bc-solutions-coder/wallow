import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import { type AddressInfo } from "node:net";

import { afterAll, beforeAll, describe, expect, it } from "vitest";

import { createAuthServer, type AuthServer } from "./auth-server";

/**
 * Spec (Wallow-vec7.1.3): prove the reverse proxy forwards Set-Cookie per
 * request with no relay, replacing the Blazor cookie-relay subsystem. A real
 * fake upstream HTTP server stands in for Wallow.Api; `createAuthServer` is
 * pointed at it and driven through its framework-agnostic `handle(request)`
 * bridge. Assertions pin the exact passthrough behaviors the bead's acceptance
 * criteria require:
 *
 *   1. ALL upstream `Set-Cookie` headers survive verbatim (getSetCookie()).
 *   2. The inbound `Cookie` header is forwarded upstream per request.
 *   3. `/connect/authorize` path + query reach upstream unchanged.
 *
 * Plus the dispatch contract: `/health` -> 200 `ready`; unknown paths -> 404.
 *
 * NOTE (RED): `auth-server.ts` is a structural stub whose `handle` returns 501,
 * so every behavioral case below fails; the GREEN phase of this bead makes them
 * pass by wiring the h3 dispatch + fetch passthrough.
 */

interface RecordedRequest {
  method: string;
  target: string;
  cookie: string | undefined;
  contentType: string | undefined;
  body: string;
}

let upstream: Server;
let upstreamUrl: string;
let lastRequest: RecordedRequest | undefined;
let auth: AuthServer;

const SET_COOKIE_ACCESS = "wallow_auth=access-token-abc; Path=/; HttpOnly; SameSite=Lax";
const SET_COOKIE_REFRESH = "wallow_refresh=refresh-token-def; Path=/; HttpOnly; SameSite=Strict";

function startUpstream(): Promise<Server> {
  const server: Server = createServer((req: IncomingMessage, res: ServerResponse): void => {
    const chunks: Buffer[] = [];
    req.on("data", (chunk: Buffer): void => {
      chunks.push(chunk);
    });
    req.on("end", (): void => {
      lastRequest = {
        method: req.method ?? "",
        target: req.url ?? "",
        cookie: req.headers.cookie,
        contentType: req.headers["content-type"],
        body: Buffer.concat(chunks).toString("utf8"),
      };

      const target: string = req.url ?? "";
      if (target.startsWith("/v1/identity/auth/login")) {
        res.statusCode = 200;
        res.appendHeader("set-cookie", SET_COOKIE_ACCESS);
        res.appendHeader("set-cookie", SET_COOKIE_REFRESH);
        res.setHeader("content-type", "application/json");
        res.end(JSON.stringify({ succeeded: true }));
        return;
      }
      if (target.startsWith("/connect/authorize")) {
        res.statusCode = 200;
        res.setHeader("content-type", "text/html");
        res.end("<html>authorize</html>");
        return;
      }
      // Default: echo the received Cookie header back so the caller can assert
      // per-request cookie forwarding.
      res.statusCode = 200;
      res.setHeader("content-type", "application/json");
      res.end(JSON.stringify({ receivedCookie: req.headers.cookie ?? null }));
    });
  });
  return new Promise<Server>((resolve): void => {
    server.listen(0, "127.0.0.1", (): void => {
      resolve(server);
    });
  });
}

beforeAll(async (): Promise<void> => {
  upstream = await startUpstream();
  const address: AddressInfo = upstream.address() as AddressInfo;
  upstreamUrl = `http://127.0.0.1:${address.port}`;
  auth = createAuthServer({ apiInternalUrl: upstreamUrl });
});

afterAll(async (): Promise<void> => {
  await new Promise<void>((resolve, reject): void => {
    upstream.close((error?: Error): void => {
      if (error) {
        reject(error);
      } else {
        resolve();
      }
    });
  });
});

describe("auth-server dispatch", () => {
  it("responds 200 'ready' to GET /health", async () => {
    const res: Response = await auth.handle(new Request("http://localhost/health"));

    expect(res.status).toBe(200);
    await expect(res.text()).resolves.toBe("ready");
  });

  it("returns 404 for paths outside /health, /v1, and /connect", async () => {
    const res: Response = await auth.handle(new Request("http://localhost/dashboard"));

    expect(res.status).toBe(404);
  });
});

describe("auth-server reverse-proxy passthrough", () => {
  it("forwards ALL upstream Set-Cookie headers verbatim on POST /v1/identity/auth/login", async () => {
    const res: Response = await auth.handle(
      new Request("http://localhost/v1/identity/auth/login", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: "user@test.local", password: "pw" }),
      }),
    );

    expect(res.status).toBe(200);
    const cookies: string[] = res.headers.getSetCookie();
    expect(cookies).toContain(SET_COOKIE_ACCESS);
    expect(cookies).toContain(SET_COOKIE_REFRESH);

    // The POST method and body reached the upstream unchanged.
    expect(lastRequest?.method).toBe("POST");
    expect(lastRequest?.contentType).toBe("application/json");
    expect(lastRequest?.body).toContain("user@test.local");
  });

  it("forwards the inbound Cookie header upstream per request", async () => {
    const res: Response = await auth.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { cookie: "wallow_auth=session-xyz" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.cookie).toBe("wallow_auth=session-xyz");
    await expect(res.json()).resolves.toEqual({ receivedCookie: "wallow_auth=session-xyz" });
  });

  it("proxies GET /connect/authorize path and query to upstream unchanged", async () => {
    const res: Response = await auth.handle(
      new Request(
        "http://localhost/connect/authorize?client_id=wallow-web&response_type=code&scope=openid%20profile",
      ),
    );

    expect(res.status).toBe(200);
    const received: URL = new URL(lastRequest?.target ?? "", "http://upstream");
    expect(received.pathname).toBe("/connect/authorize");
    expect(received.searchParams.get("client_id")).toBe("wallow-web");
    expect(received.searchParams.get("response_type")).toBe("code");
    expect(received.searchParams.get("scope")).toBe("openid profile");
  });
});
