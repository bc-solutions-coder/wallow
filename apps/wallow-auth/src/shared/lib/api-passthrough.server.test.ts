import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * Guards the two things `handleApiPassthrough` adds on top of the SDK preset:
 * the client-IP stamp, and the fact that it hands the INBOUND request object
 * straight through.
 *
 * The no-clone case is a real regression, not a style preference. The obvious
 * `new Request(request, { headers })` throws `Cannot read private member #state`
 * at runtime under srvx: its request is a bespoke class that only claims to be a
 * `Request` via `Symbol.hasInstance`, so undici's copy constructor accepts it and
 * then reads a private field it does not have. Every login on this app goes
 * through `/connect/**`, so that failure would take the whole auth flow down —
 * and it type-checks, so only a spec catches it before a booted server does.
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
  CLIENT_IP_HEADER: "x-wallow-client-ip",
  createApiPassthrough: mocks.createApiPassthrough,
}));

const CLIENT_IP_HEADER = "x-wallow-client-ip";

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

  it("stamps the peer address onto the SDK's client-IP seam header", async () => {
    const { handleApiPassthrough } = await importModule();
    const request = peerRequest("198.51.100.4");

    await handleApiPassthrough(request);

    const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
    expect(forwarded?.headers.get(CLIENT_IP_HEADER)).toBe("198.51.100.4");
  });

  it("forwards the inbound request itself rather than a copy", async () => {
    const { handleApiPassthrough } = await importModule();
    const request = peerRequest("198.51.100.4");

    await handleApiPassthrough(request);

    // Identity, not `toHaveBeenCalledWith` — that compares structurally, and a
    // clone of this request would satisfy it.
    expect(mocks.handle.mock.calls[0]?.[0]).toBe(request);
  });

  it("stamps nothing when the host supplied no peer address", async () => {
    const { handleApiPassthrough } = await importModule();
    const request = peerRequest();

    await handleApiPassthrough(request);

    const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
    expect(forwarded?.headers.has(CLIENT_IP_HEADER)).toBe(false);
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
 * Base-path support (Wallow-vufu.2.2).
 *
 * Established by spiking a real `AUTH_BASE_PATH=/auth` build against the built
 * Nitro server: TanStack Start strips the basepath from the pathname it MATCHES
 * against the route tree — which is why `/auth/v1/me` reaches the `/v1/$` route
 * at all — but hands the handler the ORIGINAL request, whose URL still carries
 * the prefix. The SDK preset then makes two decisions off that URL: whether the
 * path is in its allowlist (`/auth/v1/me` is not, so it 404s), and what path to
 * forward upstream (the API is not mounted under `/auth`). So the prefix has to
 * come off here, in the app's own wrapper, and the SDK's allowlist stays
 * unprefixed — it is shared with wallow-web and pinned by the SDK's own specs.
 *
 * The allowlist is the passthrough's entire security boundary, so the rebasing
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

  it("still stamps the client IP after rebasing", async () => {
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(
      peerRequest("198.51.100.4", "http://localhost:3002/auth/v1/me"),
      "/auth",
    );

    const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
    expect(forwarded?.headers.get(CLIENT_IP_HEADER)).toBe("198.51.100.4");
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
