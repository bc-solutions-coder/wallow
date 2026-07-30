import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * The successor to the deleted `bff-server.test.ts`.
 *
 * Config loading, store selection, handler construction and path dispatch all
 * moved into the SDK's `createWallowBffServer` and are covered there. What is
 * left for this module — and what this spec pins — is the HOST's share:
 *
 *  - the server is built LAZILY and memoised, because a Start server-route
 *    module is evaluated as part of the server bundle, where a config throw at
 *    module load takes SSR down with it;
 *  - a failed build is not cached, so a Redis that was not up yet does not
 *    permanently disable the BFF;
 *  - the Redis client is constructed and CONNECTED here and handed to the
 *    preset, which is the whole reason the host still depends on `redis` —
 *    `createWallowBffServer` throws rather than silently serving stateless
 *    cookie sessions when `REDIS_URL` is set with no client;
 *  - the peer address is stamped onto the SDK's client-IP seam header, because
 *    only the host can see a socket (Wallow-vufu.4.2).
 */

const mocks = vi.hoisted(() => ({
  handleBff: vi.fn(async (_request: Request): Promise<Response> => new Response("bff")),
  handleApi: vi.fn(async (_request: Request): Promise<Response> => new Response("api")),
  handleHealth: vi.fn((): Response => new Response("health")),
  createWallowBffServer: vi.fn(),
  connect: vi.fn(async (): Promise<void> => undefined),
  createClient: vi.fn(),
}));

mocks.createWallowBffServer.mockImplementation(() => ({
  handleBff: mocks.handleBff,
  handleApi: mocks.handleApi,
  handleHealth: mocks.handleHealth,
}));

mocks.createClient.mockImplementation(() => ({
  on: vi.fn(),
  connect: mocks.connect,
  get: vi.fn(),
  set: vi.fn(),
  del: vi.fn(),
}));

vi.mock("@bc-solutions-coder/sdk/server", () => ({
  CLIENT_IP_HEADER: "x-wallow-client-ip",
  createWallowBffServer: mocks.createWallowBffServer,
}));

vi.mock("redis", () => ({ createClient: mocks.createClient }));

/** The SDK's client-IP seam header, restated so the mock above is the only source. */
const CLIENT_IP_HEADER = "x-wallow-client-ip";

/** A stand-in for the srvx request a Start server route receives: a `Request` plus `ip`. */
function peerRequest(
  ip?: string,
  url = "http://localhost:3000/api/v1/users/me",
): Request & { ip?: string } {
  const request = new Request(url) as Request & { ip?: string };
  if (ip !== undefined) {
    Object.defineProperty(request, "ip", { value: ip });
  }
  return request;
}

/** Re-evaluate the module so its memoised server starts empty. */
async function importModule(): Promise<typeof import("./bff")> {
  vi.resetModules();
  return import("./bff");
}

/** The options `createWallowBffServer` was called with on the Nth (0-based) build. */
function buildOptions(index = 0): Record<string, unknown> {
  return (mocks.createWallowBffServer.mock.calls[index]?.[0] ?? {}) as Record<string, unknown>;
}

const originalRedisUrl: string | undefined = process.env.REDIS_URL;

describe("the wallow-web BFF host", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    delete process.env.REDIS_URL;
  });

  afterEach(() => {
    if (originalRedisUrl === undefined) {
      delete process.env.REDIS_URL;
    } else {
      process.env.REDIS_URL = originalRedisUrl;
    }
  });

  it("builds nothing at module load", async () => {
    await importModule();

    expect(mocks.createWallowBffServer).not.toHaveBeenCalled();
  });

  it("builds the server once and shares it across every handler", async () => {
    const { handleApiRequest, handleBffRequest, handleHealthRequest } = await importModule();

    await handleBffRequest(new Request("http://localhost:3000/bff/user"));
    await handleApiRequest(new Request("http://localhost:3000/api/v1/users/me"));
    await handleHealthRequest();

    expect(mocks.createWallowBffServer).toHaveBeenCalledTimes(1);
  });

  it("delegates each mount to the matching preset handler", async () => {
    const { handleApiRequest, handleBffRequest, handleHealthRequest } = await importModule();
    const bffRequest = new Request("http://localhost:3000/bff/user");
    const apiRequest = new Request("http://localhost:3000/api/v1/users/me");

    await handleBffRequest(bffRequest);
    await handleApiRequest(apiRequest);
    await handleHealthRequest();

    expect(mocks.handleBff).toHaveBeenCalledWith(bffRequest);
    expect(mocks.handleApi).toHaveBeenCalledWith(apiRequest);
    expect(mocks.handleHealth).toHaveBeenCalledTimes(1);
  });

  it("supplies no redis client when REDIS_URL is unset", async () => {
    const { handleHealthRequest } = await importModule();

    await handleHealthRequest();

    expect(mocks.createClient).not.toHaveBeenCalled();
    expect(buildOptions()).not.toHaveProperty("redisClient");
  });

  it("connects a redis client and hands it to the preset when REDIS_URL is set", async () => {
    process.env.REDIS_URL = "redis://valkey:6379";
    const { handleHealthRequest } = await importModule();

    await handleHealthRequest();

    expect(mocks.createClient).toHaveBeenCalledWith({ url: "redis://valkey:6379" });
    expect(mocks.connect).toHaveBeenCalledTimes(1);
    expect(buildOptions()).toHaveProperty("redisClient");
  });

  it("stamps the peer address onto the SDK's client-IP seam header", async () => {
    const { handleApiRequest } = await importModule();
    const request = peerRequest("198.51.100.4");

    await handleApiRequest(request);

    const forwarded: Request | undefined = mocks.handleApi.mock.calls[0]?.[0];
    expect(forwarded?.headers.get(CLIENT_IP_HEADER)).toBe("198.51.100.4");
  });

  it("forwards the inbound request itself rather than a copy", async () => {
    const { handleApiRequest } = await importModule();
    const request = peerRequest("198.51.100.4");

    await handleApiRequest(request);

    // Identity, not `toHaveBeenCalledWith` — that compares structurally, and a
    // clone of this request would satisfy it. srvx's request class cannot
    // survive undici's copy constructor, so the header must be set in place.
    expect(mocks.handleApi.mock.calls[0]?.[0]).toBe(request);
  });

  it("stamps nothing when the host supplied no peer address", async () => {
    const { handleApiRequest } = await importModule();
    const request = peerRequest();

    await handleApiRequest(request);

    const forwarded: Request | undefined = mocks.handleApi.mock.calls[0]?.[0];
    expect(forwarded?.headers.has(CLIENT_IP_HEADER)).toBe(false);
  });

  it("does not cache a failed build", async () => {
    mocks.createWallowBffServer.mockImplementationOnce(() => {
      throw new Error("COOKIE_PASSWORD is required");
    });
    const { handleHealthRequest } = await importModule();

    await expect(handleHealthRequest()).rejects.toThrow("COOKIE_PASSWORD is required");
    await expect(handleHealthRequest()).resolves.toBeInstanceOf(Response);
    expect(mocks.createWallowBffServer).toHaveBeenCalledTimes(2);
  });
});
