import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import { type AddressInfo } from "node:net";

import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";

import {
  createAuthServer,
  resolveApiInternalUrl,
  CLIENT_IP_HEADER,
  type AuthServer,
} from "./auth-server";

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
  forwardedProto: string | undefined;
  forwardedHost: string | undefined;
  forwardedFor: string | undefined;
  clientIpHeader: string | undefined;
}

let upstream: Server;
let upstreamUrl: string;
let lastRequest: RecordedRequest | undefined;
let auth: AuthServer;

const SET_COOKIE_ACCESS = "wallow_auth=access-token-abc; Path=/; HttpOnly; SameSite=Lax";
const SET_COOKIE_REFRESH = "wallow_refresh=refresh-token-def; Path=/; HttpOnly; SameSite=Strict";

const DISCOVERY_DOCUMENT = {
  issuer: "http://localhost",
  authorization_endpoint: "http://localhost/connect/authorize",
  token_endpoint: "http://localhost/connect/token",
  jwks_uri: "http://localhost/.well-known/jwks",
};

const JWKS_DOCUMENT = { keys: [{ kty: "RSA", kid: "test-key" }] };

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
        forwardedProto: req.headers["x-forwarded-proto"] as string | undefined,
        forwardedHost: req.headers["x-forwarded-host"] as string | undefined,
        forwardedFor: req.headers["x-forwarded-for"] as string | undefined,
        clientIpHeader: req.headers[CLIENT_IP_HEADER] as string | undefined,
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
      if (target === "/.well-known/openid-configuration") {
        res.statusCode = 200;
        res.setHeader("content-type", "application/json");
        res.end(JSON.stringify(DISCOVERY_DOCUMENT));
        return;
      }
      if (target === "/.well-known/jwks") {
        res.statusCode = 200;
        res.setHeader("content-type", "application/json");
        res.end(JSON.stringify(JWKS_DOCUMENT));
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

  it("returns 404 for paths outside /health, /v1, /connect, and /.well-known", async () => {
    const res: Response = await auth.handle(new Request("http://localhost/dashboard"));

    expect(res.status).toBe(404);
  });
});

/**
 * Spec (Wallow-vec7.4.2): once the issuer is this origin (Wallow-vec7.4.1), every OIDC client
 * whose Authority points here resolves discovery at `${origin}/.well-known/openid-configuration`
 * and fetches signing keys from the `jwks_uri` that document advertises. The proxy mounted only
 * `/v1/**` and `/connect/**`, so both 404'd — clients could only work around it with an explicit
 * out-of-band metadata URL. The subtree wildcard is what makes an Authority pointed at this
 * origin work without one.
 */
describe("auth-server discovery passthrough", () => {
  it("proxies GET /.well-known/openid-configuration to upstream", async () => {
    const res: Response = await auth.handle(
      new Request("http://localhost/.well-known/openid-configuration"),
    );

    expect(res.status).toBe(200);
    await expect(res.json()).resolves.toEqual(DISCOVERY_DOCUMENT);
    expect(lastRequest?.target).toBe("/.well-known/openid-configuration");
  });

  it("proxies the whole /.well-known subtree, including the jwks endpoint", async () => {
    const res: Response = await auth.handle(new Request("http://localhost/.well-known/jwks"));

    expect(res.status).toBe(200);
    await expect(res.json()).resolves.toEqual(JWKS_DOCUMENT);
    expect(lastRequest?.target).toBe("/.well-known/jwks");
  });
});

/**
 * Spec (Wallow-vec7.4.3): cookie attributes must stay correct now that BOTH top-level
 * navigation (`/connect/*`, exchange-ticket, logout) and XHR data calls reach the API
 * through this proxy instead of hitting the API's own origin.
 *
 * The attributes themselves are decided upstream, not here: the durable Identity cookie
 * uses `SecurePolicy.Always` outside Development and OpenIddict rejects plain HTTP with
 * ID2083 ("This server only accepts HTTPS requests"). Both are computed from the scheme
 * the API *sees*, and `api/src/Wallow.Api/Program.cs:372-385` derives that scheme from
 * `X-Forwarded-Proto` (its comment names ID2083 as the exact failure mode).
 *
 * The proxy's browser-facing leg is HTTPS in production but its upstream leg is plain
 * HTTP (`http://wallow-api`), so unless the inbound scheme is forwarded the API sees HTTP
 * and top-level nav to `/connect/authorize` fails outright. Standard proxy-chaining
 * semantics apply: an `X-Forwarded-*` header set by an outer trusted proxy (TLS-terminating
 * ingress) wins, since it alone knows the real browser leg; otherwise this proxy derives
 * the value from its own inbound request.
 */
describe("auth-server forwarded-scheme propagation", () => {
  it("sends X-Forwarded-Proto derived from the inbound scheme when the client sent none", async () => {
    const res: Response = await auth.handle(
      new Request("https://auth.wallow.dev/v1/identity/users/me", {
        headers: { cookie: "wallow_auth=session-xyz" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedProto).toBe("https");
  });

  it("sends X-Forwarded-Host derived from the inbound host when the client sent none", async () => {
    const res: Response = await auth.handle(
      new Request("https://auth.wallow.dev/v1/identity/users/me"),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedHost).toBe("auth.wallow.dev");
  });

  it("forwards X-Forwarded-Proto on top-level nav to /connect/authorize (the ID2083 path)", async () => {
    const res: Response = await auth.handle(
      new Request(
        "https://auth.wallow.dev/connect/authorize?client_id=wallow-web&response_type=code",
      ),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedProto).toBe("https");
  });

  it("derives X-Forwarded-Proto as http for a plain-HTTP dev origin", async () => {
    const res: Response = await auth.handle(
      new Request("http://localhost:3001/v1/identity/users/me"),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedProto).toBe("http");
  });

  it("preserves an outer proxy's X-Forwarded-Proto instead of overwriting it with its own leg", async () => {
    // TLS-terminating ingress -> this proxy over plain HTTP. Only the ingress knows the
    // browser used HTTPS; overwriting with the inbound scheme would downgrade to http and
    // resurrect ID2083 behind an ingress.
    const res: Response = await auth.handle(
      new Request("http://wallow-auth:3000/connect/authorize", {
        headers: { "x-forwarded-proto": "https", "x-forwarded-host": "auth.wallow.dev" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedProto).toBe("https");
    expect(lastRequest?.forwardedHost).toBe("auth.wallow.dev");
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

/**
 * Spec (Wallow-tt5j): the API rate-limits per client IP off `X-Forwarded-For`
 * (`Wallow.Api` Program.cs configures `UseForwardedHeaders` with
 * `KnownProxies.Clear()`). The Blazor `CookieForwardingHandler` this proxy
 * replaces forwarded XFF deliberately ("rate-limit by real client IP, not
 * Docker network IP"); without this the API buckets every request against the
 * proxy's own IP — a regression at Blazor cutover.
 *
 * SEAM: a WHATWG `Request` carries no socket, so the proxy cannot derive the
 * peer address itself. The Node host (`server.ts` / `dev-server.ts`) stamps the
 * real `req.socket.remoteAddress` into the internal {@link CLIENT_IP_HEADER}
 * before calling `handle`; the proxy APPENDS that value to any inbound
 * `X-Forwarded-For` chain (RFC 7239 semantics — an outer ingress's leftmost
 * real-client entry survives), then STRIPS the seam header so it never leaks
 * upstream. This is the seam the bead's DESIGN settles on; the append/strip is
 * fully expressible through `handle(Request)`, only the socket read is not.
 */
describe("auth-server X-Forwarded-For chaining", () => {
  it("sets X-Forwarded-For to the host-stamped client IP when the client sent none", async () => {
    const res: Response = await auth.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { [CLIENT_IP_HEADER]: "203.0.113.7" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedFor).toBe("203.0.113.7");
  });

  it("appends the host-stamped client IP to an inbound X-Forwarded-For rather than overwriting it", async () => {
    const res: Response = await auth.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { "x-forwarded-for": "198.51.100.9", [CLIENT_IP_HEADER]: "203.0.113.7" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedFor).toBe("198.51.100.9, 203.0.113.7");
  });

  it("preserves a multi-hop inbound X-Forwarded-For chain, appending this hop's client IP last", async () => {
    const res: Response = await auth.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: {
          "x-forwarded-for": "198.51.100.9, 70.41.3.18",
          [CLIENT_IP_HEADER]: "203.0.113.7",
        },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedFor).toBe("198.51.100.9, 70.41.3.18, 203.0.113.7");
  });

  it("never leaks the internal client-IP seam header upstream", async () => {
    const res: Response = await auth.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { [CLIENT_IP_HEADER]: "203.0.113.7" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.clientIpHeader).toBeUndefined();
  });
});

/**
 * Spec (Wallow-vpnt): outside Aspire, a bare `pnpm --filter ./apps/wallow-auth dev`
 * against a locally-run API (`dotnet run` on :5001) must resolve a working upstream
 * by default. The previous fallback (`http://wallow-api`) only resolves under Aspire
 * service discovery or Docker networking, so standalone dev 500'd every proxied call
 * with `getaddrinfo ENOTFOUND wallow-api`.
 *
 * Every managed context sets `WALLOW_API_INTERNAL_URL` explicitly — Aspire wiring and
 * both Docker compose stacks (`docker-compose.production.yml`, `docker-compose.test.yml`) —
 * and the Playwright config injects `http://localhost:5001`. The bare default is reached
 * ONLY by standalone dev, so it must point at the local API host, not the container name.
 *
 * Precedence is unchanged: explicit `apiInternalUrl` config wins, then the
 * `WALLOW_API_INTERNAL_URL` env var, then the standalone-dev localhost default.
 */
describe("auth-server API target resolution", () => {
  const ENV_KEY = "WALLOW_API_INTERNAL_URL";
  let savedEnv: string | undefined;

  beforeEach((): void => {
    savedEnv = process.env[ENV_KEY];
  });

  afterEach((): void => {
    if (savedEnv === undefined) {
      delete process.env[ENV_KEY];
    } else {
      process.env[ENV_KEY] = savedEnv;
    }
  });

  it("falls back to the local API host (:5001) when no env var or config is set outside Aspire", () => {
    delete process.env[ENV_KEY];

    expect(resolveApiInternalUrl({})).toBe("http://localhost:5001");
  });

  it("treats an empty WALLOW_API_INTERNAL_URL as unset and uses the local default", () => {
    process.env[ENV_KEY] = "";

    expect(resolveApiInternalUrl({})).toBe("http://localhost:5001");
  });

  it("uses WALLOW_API_INTERNAL_URL when set, so Aspire and Docker still control the target", () => {
    process.env[ENV_KEY] = "http://wallow-api:8080";

    expect(resolveApiInternalUrl({})).toBe("http://wallow-api:8080");
  });

  it("lets an explicit apiInternalUrl config win over the env var and the default", () => {
    process.env[ENV_KEY] = "http://wallow-api:8080";

    expect(resolveApiInternalUrl({ apiInternalUrl: "http://127.0.0.1:5555" })).toBe(
      "http://127.0.0.1:5555",
    );
  });
});
