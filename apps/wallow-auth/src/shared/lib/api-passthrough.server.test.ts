import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * What `handleApiPassthrough` adds on top of the SDK preset: base-path
 * rebasing, and handing the INBOUND request object straight through so the
 * SDK can read the peer address off it.
 *
 * The no-clone case is a real regression: `new Request(request, { headers })`
 * throws `Cannot read private member #state` under srvx, whose request only
 * claims to be a `Request` via `Symbol.hasInstance`, so undici's copy
 * constructor accepts it and then reads a field it does not have. It type-checks,
 * so only a spec catches it before a booted server does.
 */

const mocks = vi.hoisted(() => ({
  // The parameter is declared (though unused) so `mock.calls[0][0]` is typed as
  // the forwarded request rather than as an empty tuple.
  handle: vi.fn(async (_request: Request): Promise<Response> => new Response("ok")),
  createApiPassthrough: vi.fn(),
}));

mocks.createApiPassthrough.mockImplementation(() => ({
  handle: mocks.handle,
  matches: (): boolean => true,
  apiInternalUrl: "http://api.test",
  prefixes: [],
}));

vi.mock("@bc-solutions-coder/sdk/server/passthrough", () => ({
  createApiPassthrough: mocks.createApiPassthrough,
}));

/** A stand-in for the srvx request a Start server route receives: a `Request` plus `ip`. */
function peerRequest(
  ip?: string,
  url = "http://localhost:3002/v1/ping",
): Request & { ip?: string } {
  const request = new Request(url) as Request & { ip?: string };
  if (ip !== undefined) {
    Object.defineProperty(request, "ip", { value: ip });
  }
  return request;
}

/** Re-evaluate the module so its memoised passthrough starts empty. */
async function importModule(): Promise<{
  handleApiPassthrough: (
    request: Request & { ip?: string },
    basePath?: string,
  ) => Promise<Response>;
}> {
  vi.resetModules();
  return import("./api-passthrough.server");
}

/** The URL the SDK preset was actually asked to forward. */
function forwardedUrl(): URL {
  const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
  if (forwarded === undefined) {
    throw new Error("the SDK passthrough was never called");
  }
  return new URL(forwarded.url);
}

describe("handleApiPassthrough", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("forwards the inbound request itself rather than a copy", async () => {
    const { handleApiPassthrough } = await importModule();
    const request = peerRequest("198.51.100.4");

    await handleApiPassthrough(request);

    // Identity, not `toHaveBeenCalledWith` — that compares structurally, and a
    // clone of this request would satisfy it, while losing the `ip` the SDK
    // resolves the client address from.
    const forwarded = mocks.handle.mock.calls[0]?.[0] as (Request & { ip?: string }) | undefined;
    expect(forwarded).toBe(request);
    expect(forwarded?.ip).toBe("198.51.100.4");
  });

  it("builds the passthrough lazily and only once", async () => {
    const { handleApiPassthrough } = await importModule();

    expect(mocks.createApiPassthrough).not.toHaveBeenCalled();

    await handleApiPassthrough(peerRequest("198.51.100.4"));
    await handleApiPassthrough(peerRequest("198.51.100.5"));

    expect(mocks.createApiPassthrough).toHaveBeenCalledTimes(1);
  });
});

/**
 * TanStack Start strips the basepath from the pathname it MATCHES against the
 * route tree — which is why `/auth/v1/me` reaches the `/v1/$` route — but hands
 * the handler the ORIGINAL request, whose URL still carries the prefix. The SDK
 * preset reads that URL both for its allowlist and for the upstream path, so the
 * prefix comes off here and the SDK's allowlist stays unprefixed (it is shared
 * with wallow-web).
 *
 * That allowlist is the passthrough's entire security boundary, so the rebasing
 * matches on segment boundaries exactly as the SDK's own prefix check does.
 */
describe("handleApiPassthrough under a base path", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("hands the SDK an unprefixed path so the allowlist matches and the API is hit at /v1", async () => {
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(peerRequest(undefined, "http://localhost:3002/auth/v1/me"), "/auth");

    expect(forwardedUrl().pathname).toBe("/v1/me");
  });

  it("rebases the OIDC endpoints too, not just /v1", async () => {
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(
      peerRequest(undefined, "http://localhost:3002/auth/.well-known/openid-configuration"),
      "/auth",
    );

    expect(forwardedUrl().pathname).toBe("/.well-known/openid-configuration");
  });

  it("keeps the query string, which carries the whole authorize request", async () => {
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(
      peerRequest(
        undefined,
        "http://localhost:3002/auth/connect/authorize?client_id=web&scope=openid",
      ),
      "/auth",
    );

    const url: URL = forwardedUrl();
    expect(url.pathname).toBe("/connect/authorize");
    expect(url.searchParams.get("client_id")).toBe("web");
    expect(url.searchParams.get("scope")).toBe("openid");
  });

  it("leaves the origin alone — the prefix is a path, not a host", async () => {
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(peerRequest(undefined, "http://localhost:3002/auth/v1/me"), "/auth");

    expect(forwardedUrl().origin).toBe("http://localhost:3002");
  });

  it("does not rebase a path that only looks like it carries the prefix", async () => {
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(
      peerRequest(undefined, "http://localhost:3002/authentic/v1/me"),
      "/auth",
    );

    // Untouched — the SDK's own allowlist then rejects it, which is the point.
    expect(forwardedUrl().pathname).toBe("/authentic/v1/me");
  });

  it("still forwards an already-unprefixed path unchanged", async () => {
    // The router's basepath rewrite is a no-op for a path that does not start
    // with the prefix, so `/v1/me` still reaches this handler under a based build.
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(peerRequest(undefined, "http://localhost:3002/v1/me"), "/auth");

    expect(forwardedUrl().pathname).toBe("/v1/me");
  });

  it("keeps the peer address on the request after rebasing", async () => {
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(
      peerRequest("198.51.100.4", "http://localhost:3002/auth/v1/me"),
      "/auth",
    );

    const forwarded = mocks.handle.mock.calls[0]?.[0] as (Request & { ip?: string }) | undefined;
    expect(forwarded?.ip).toBe("198.51.100.4");
  });

  it("preserves the method and body, so a rebased POST is still a POST", async () => {
    const { handleApiPassthrough } = await importModule();
    const request = new Request("http://localhost:3002/auth/v1/sessions", {
      method: "POST",
      body: JSON.stringify({ email: "admin@wallow.dev" }),
      headers: { "content-type": "application/json" },
    }) as Request & { ip?: string };

    await handleApiPassthrough(request, "/auth");

    const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
    expect(forwarded?.method).toBe("POST");
    await expect(forwarded?.text()).resolves.toBe(JSON.stringify({ email: "admin@wallow.dev" }));
  });

  it("changes nothing when the base path is empty — the default build", async () => {
    const { handleApiPassthrough } = await importModule();
    const request = peerRequest("198.51.100.4");

    await handleApiPassthrough(request, "");

    expect(forwardedUrl().pathname).toBe("/v1/ping");
    expect(mocks.handle.mock.calls[0]?.[0]).toBe(request);
  });
});
