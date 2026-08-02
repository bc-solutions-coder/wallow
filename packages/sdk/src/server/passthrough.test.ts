import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import { type AddressInfo } from "node:net";

import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";

import {
  CLIENT_IP_HEADER,
  createApiPassthrough,
  DEFAULT_API_INTERNAL_URL,
  DEFAULT_PASSTHROUGH_PREFIXES,
  resolveApiInternalUrl,
  type ApiPassthrough,
} from "./passthrough";

/**
 * Spec (Wallow-pu6a.3.7): `createApiPassthrough` absorbs the pure reverse-proxy
 * topology every fork used to hand-assemble — 201 lines in the deleted
 * `apps/wallow-auth/src/lib/auth-server.ts` and a near-identical 139 in
 * `apps/examples/minimal-app/src/lib/proxy-server.ts`. These cases are ported
 * from that suite (the stronger of the two, since it also stamps the client IP)
 * and generalized to the preset's options: the hardcoded `/v1`, `/connect`,
 * `/.well-known` list becomes `prefixes`, and the XFF stamping becomes
 * `forwardClientIp`.
 *
 * A real fake upstream HTTP server stands in for Wallow.Api, so the assertions
 * pin what actually reaches the wire: method, path, query, body, `Cookie`, the
 * `X-Forwarded-*` chain, and every upstream `Set-Cookie` coming back verbatim.
 */

interface RecordedRequest {
  method: string;
  target: string;
  cookie: string | undefined;
  contentType: string | undefined;
  body: string;
  host: string | undefined;
  forwardedProto: string | undefined;
  forwardedHost: string | undefined;
  forwardedFor: string | undefined;
  clientIpHeader: string | undefined;
}

let upstream: Server;
let upstreamUrl: string;
let lastRequest: RecordedRequest | undefined;
let proxy: ApiPassthrough;

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
        host: req.headers.host,
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
      if (target.startsWith("/v1/identity/redirects/found")) {
        res.statusCode = 302;
        res.setHeader("location", "/v1/identity/redirects/target");
        res.end();
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
  proxy = createApiPassthrough({ apiInternalUrl: upstreamUrl });
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

/**
 * The prefix allowlist is the whole security boundary of this preset: anything
 * matched is forwarded upstream verbatim, so the match must be a segment-
 * boundary test and `/.well-known/**` must be in the default set (leaving it
 * out has already broken OIDC discovery once in this repo).
 */
describe("createApiPassthrough — prefix allowlist", () => {
  it("includes /.well-known/** in the default prefixes, alongside /v1 and /connect", () => {
    expect(DEFAULT_PASSTHROUGH_PREFIXES).toContain("/.well-known/**");
    expect(DEFAULT_PASSTHROUGH_PREFIXES).toContain("/v1/**");
    expect(DEFAULT_PASSTHROUGH_PREFIXES).toContain("/connect/**");
  });

  it("matches the default subtrees and nothing else", () => {
    expect(proxy.matches("/v1/identity/users/me")).toBe(true);
    expect(proxy.matches("/connect/authorize")).toBe(true);
    expect(proxy.matches("/.well-known/openid-configuration")).toBe(true);
    expect(proxy.matches("/.well-known/jwks")).toBe(true);

    expect(proxy.matches("/dashboard")).toBe(false);
    expect(proxy.matches("/")).toBe(false);
    expect(proxy.matches("/health")).toBe(false);
  });

  it("matches on segment boundaries, so a lookalike prefix is not proxied", () => {
    expect(proxy.matches("/v1/secrets")).toBe(true);
    expect(proxy.matches("/v1extra/secrets")).toBe(false);
    expect(proxy.matches("/connected/thing")).toBe(false);
    expect(proxy.matches("/.well-knownsuffix")).toBe(false);
  });

  it("matches the bare prefix itself, not just its children", () => {
    expect(proxy.matches("/v1")).toBe(true);
    expect(proxy.matches("/v1/")).toBe(true);
  });

  it("returns 404 for a path outside the allowlist, without touching upstream", async () => {
    lastRequest = undefined;

    const res: Response = await proxy.handle(new Request("http://localhost/dashboard"));

    expect(res.status).toBe(404);
    expect(lastRequest).toBeUndefined();
  });

  it("honours an explicit prefix list, accepting both wildcard and bare forms", async () => {
    const custom: ApiPassthrough = createApiPassthrough({
      apiInternalUrl: upstreamUrl,
      prefixes: ["/v1/**", "/connect"],
    });

    expect(custom.matches("/v1/identity/users/me")).toBe(true);
    expect(custom.matches("/connect/authorize")).toBe(true);
    // Not in the custom list, even though it is a default.
    expect(custom.matches("/.well-known/openid-configuration")).toBe(false);

    const res: Response = await custom.handle(
      new Request("http://localhost/.well-known/openid-configuration"),
    );
    expect(res.status).toBe(404);
  });

  it("exposes the resolved prefixes and upstream target", () => {
    expect(proxy.prefixes).toEqual(DEFAULT_PASSTHROUGH_PREFIXES);
    expect(proxy.apiInternalUrl).toBe(upstreamUrl);
  });
});

/**
 * Spec ported from `auth-server.test.ts` (Wallow-vec7.4.2): discovery and the
 * `jwks_uri` it advertises both resolve at this origin, so the whole
 * `/.well-known` subtree must proxy — not just `openid-configuration`.
 */
describe("createApiPassthrough — discovery passthrough", () => {
  it("proxies GET /.well-known/openid-configuration to upstream", async () => {
    const res: Response = await proxy.handle(
      new Request("http://localhost/.well-known/openid-configuration"),
    );

    expect(res.status).toBe(200);
    await expect(res.json()).resolves.toEqual(DISCOVERY_DOCUMENT);
    expect(lastRequest?.target).toBe("/.well-known/openid-configuration");
  });

  it("proxies the whole /.well-known subtree, including the jwks endpoint", async () => {
    const res: Response = await proxy.handle(new Request("http://localhost/.well-known/jwks"));

    expect(res.status).toBe(200);
    await expect(res.json()).resolves.toEqual(JWKS_DOCUMENT);
    expect(lastRequest?.target).toBe("/.well-known/jwks");
  });
});

describe("createApiPassthrough — reverse-proxy passthrough", () => {
  it("forwards ALL upstream Set-Cookie headers verbatim on POST /v1/identity/auth/login", async () => {
    const res: Response = await proxy.handle(
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
    const res: Response = await proxy.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { cookie: "wallow_auth=session-xyz" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.cookie).toBe("wallow_auth=session-xyz");
    await expect(res.json()).resolves.toEqual({ receivedCookie: "wallow_auth=session-xyz" });
  });

  it("proxies GET /connect/authorize path and query to upstream unchanged", async () => {
    const res: Response = await proxy.handle(
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

  it("strips the inbound Host so the upstream sees its own authority", async () => {
    const res: Response = await proxy.handle(
      new Request("http://auth.wallow.dev/v1/identity/users/me"),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.host).not.toBe("auth.wallow.dev");
  });

  it("returns an upstream redirect as-is rather than following it", async () => {
    const res: Response = await proxy.handle(
      new Request("http://localhost/v1/identity/redirects/found"),
    );

    expect(res.status).toBe(302);
    expect(res.headers.get("location")).toBe("/v1/identity/redirects/target");
  });
});

/**
 * Spec ported from `auth-server.test.ts` (Wallow-vec7.4.3): the API computes the
 * Identity cookie's `Secure` attribute and OpenIddict's HTTPS check (ID2083)
 * from the scheme it sees, which it derives from `X-Forwarded-Proto`. This
 * proxy's upstream leg is plain HTTP even when the browser leg is HTTPS, and an
 * outer TLS-terminating ingress is the only hop that knows the real scheme — so
 * a header it already set must win.
 */
describe("createApiPassthrough — forwarded-scheme propagation", () => {
  it("sends X-Forwarded-Proto derived from the inbound scheme when the client sent none", async () => {
    const res: Response = await proxy.handle(
      new Request("https://auth.wallow.dev/v1/identity/users/me", {
        headers: { cookie: "wallow_auth=session-xyz" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedProto).toBe("https");
  });

  it("sends X-Forwarded-Host derived from the inbound host when the client sent none", async () => {
    const res: Response = await proxy.handle(
      new Request("https://auth.wallow.dev/v1/identity/users/me"),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedHost).toBe("auth.wallow.dev");
  });

  it("derives X-Forwarded-Proto as http for a plain-HTTP dev origin", async () => {
    const res: Response = await proxy.handle(
      new Request("http://localhost:3001/v1/identity/users/me"),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedProto).toBe("http");
  });

  it("preserves an outer proxy's X-Forwarded-Proto instead of overwriting it with its own leg", async () => {
    const res: Response = await proxy.handle(
      new Request("http://wallow-auth:3000/connect/authorize", {
        headers: { "x-forwarded-proto": "https", "x-forwarded-host": "auth.wallow.dev" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedProto).toBe("https");
    expect(lastRequest?.forwardedHost).toBe("auth.wallow.dev");
  });
});

/**
 * Spec ported from `auth-server.test.ts` (Wallow-tt5j): the API rate-limits per
 * client IP off `X-Forwarded-For` with `KnownProxies.Clear()`, so the chain must
 * be appended to — never overwritten — and this hop's entry comes from the
 * host-stamped seam header, which must not leak upstream.
 */
describe("createApiPassthrough — X-Forwarded-For chaining", () => {
  it("sets X-Forwarded-For to the host-stamped client IP when the client sent none", async () => {
    const res: Response = await proxy.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { [CLIENT_IP_HEADER]: "203.0.113.7" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedFor).toBe("203.0.113.7");
  });

  it("appends the host-stamped client IP to an inbound X-Forwarded-For rather than overwriting it", async () => {
    const res: Response = await proxy.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { "x-forwarded-for": "198.51.100.9", [CLIENT_IP_HEADER]: "203.0.113.7" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedFor).toBe("198.51.100.9, 203.0.113.7");
  });

  it("preserves a multi-hop inbound X-Forwarded-For chain, appending this hop's client IP last", async () => {
    const res: Response = await proxy.handle(
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
    const res: Response = await proxy.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { [CLIENT_IP_HEADER]: "203.0.113.7" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.clientIpHeader).toBeUndefined();
  });

  it("does not append the client IP when forwardClientIp is false, but still strips the seam header", async () => {
    const noXff: ApiPassthrough = createApiPassthrough({
      apiInternalUrl: upstreamUrl,
      forwardClientIp: false,
    });

    const res: Response = await noXff.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { [CLIENT_IP_HEADER]: "203.0.113.7" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedFor).toBeUndefined();
    expect(lastRequest?.clientIpHeader).toBeUndefined();
  });

  it("leaves an inbound X-Forwarded-For untouched when forwardClientIp is false", async () => {
    const noXff: ApiPassthrough = createApiPassthrough({
      apiInternalUrl: upstreamUrl,
      forwardClientIp: false,
    });

    const res: Response = await noXff.handle(
      new Request("http://localhost/v1/identity/users/me", {
        headers: { "x-forwarded-for": "198.51.100.9", [CLIENT_IP_HEADER]: "203.0.113.7" },
      }),
    );

    expect(res.status).toBe(200);
    expect(lastRequest?.forwardedFor).toBe("198.51.100.9");
  });
});

/**
 * Spec ported from `auth-server.test.ts` (Wallow-vpnt): precedence is explicit
 * config, then `WALLOW_API_INTERNAL_URL`, then the standalone-dev localhost
 * default — `http://wallow-api` only resolves under Aspire/Docker and 500s every
 * proxied call outside them.
 */
describe("createApiPassthrough — API target resolution", () => {
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

  it("falls back to the local API host (:5001) when no env var or config is set", () => {
    delete process.env[ENV_KEY];

    expect(resolveApiInternalUrl({})).toBe(DEFAULT_API_INTERNAL_URL);
    expect(DEFAULT_API_INTERNAL_URL).toBe("http://localhost:5001");
  });

  it("treats an empty WALLOW_API_INTERNAL_URL as unset and uses the local default", () => {
    expect(resolveApiInternalUrl({ env: { WALLOW_API_INTERNAL_URL: "" } })).toBe(
      DEFAULT_API_INTERNAL_URL,
    );
  });

  it("uses WALLOW_API_INTERNAL_URL when set, so Aspire and Docker still control the target", () => {
    expect(
      resolveApiInternalUrl({ env: { WALLOW_API_INTERNAL_URL: "http://wallow-api:8080" } }),
    ).toBe("http://wallow-api:8080");
  });

  it("reads process.env by default when no env source is supplied", () => {
    process.env[ENV_KEY] = "http://wallow-api:8080";

    expect(resolveApiInternalUrl({})).toBe("http://wallow-api:8080");
  });

  it("lets an explicit apiInternalUrl config win over the env var and the default", () => {
    expect(
      resolveApiInternalUrl({
        apiInternalUrl: "http://127.0.0.1:5555",
        env: { WALLOW_API_INTERNAL_URL: "http://wallow-api:8080" },
      }),
    ).toBe("http://127.0.0.1:5555");
  });

  it("resolves the same target inside createApiPassthrough", () => {
    const fromEnv: ApiPassthrough = createApiPassthrough({
      env: { WALLOW_API_INTERNAL_URL: "http://wallow-api:8080" },
    });

    expect(fromEnv.apiInternalUrl).toBe("http://wallow-api:8080");
  });
});
