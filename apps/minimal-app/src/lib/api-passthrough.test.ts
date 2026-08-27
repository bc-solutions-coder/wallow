import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * Guards the two things `handleApiPassthrough` adds on top of the SDK preset:
 * the client-IP stamp, and the fact that it hands the INBOUND request object
 * straight through.
 *
 * The no-clone case is a real regression, not a style preference. The obvious
 * `new Request(request, { headers })` throws `Cannot read private member #state`
 * at runtime under srvx: its request is a bespoke class that only claims to be a
 * `Request` via `Symbol.hasInstance`, so undici's copy constructor accepts it and
 * then reads a private field it does not have. Nothing else in this app's suite
 * exercises a server route, so without this spec that failure only ever shows up
 * as a 500 from a booted server.
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
function peerRequest(ip?: string, forwardedFor?: string): Request & { ip?: string } {
  const headers: Headers = new Headers();
  if (forwardedFor !== undefined) {
    headers.set("x-forwarded-for", forwardedFor);
  }
  const request = new Request("http://localhost:3010/v1/ping", { headers }) as Request & {
    ip?: string;
  };
  if (ip !== undefined) {
    Object.defineProperty(request, "ip", { value: ip });
  }
  return request;
}

/** Re-evaluate the module so its memoised passthrough starts empty. */
async function importModule(): Promise<{
  handleApiPassthrough: (request: Request & { ip?: string }) => Promise<Response>;
}> {
  vi.resetModules();
  return import("./api-passthrough");
}

describe("handleApiPassthrough", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it("stamps the peer address onto the SDK's client-IP seam header", async () => {
    const { handleApiPassthrough } = await importModule();
    const request = peerRequest("198.51.100.4");

    await handleApiPassthrough(request);

    const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
    expect(forwarded?.headers.get(CLIENT_IP_HEADER)).toBe("198.51.100.4");
  });

  it("reads the forwarded chain when the peer is a trusted proxy", async () => {
    // The production shape: Caddy on the container bridge network is the peer,
    // and the address it appended is the real caller's.
    vi.stubEnv("WALLOW_TRUSTED_PROXIES", "private");
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(peerRequest("10.0.0.7", "198.51.100.4"));

    const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
    expect(forwarded?.headers.get(CLIENT_IP_HEADER)).toBe("198.51.100.4");
  });

  it("ignores a forwarded chain from an untrusted peer, which cannot forge its address", async () => {
    // The load-bearing half. A caller reaching this app directly can write any
    // chain it likes; believing it would let it pick the API's rate-limit bucket.
    vi.stubEnv("WALLOW_TRUSTED_PROXIES", "private");
    const { handleApiPassthrough } = await importModule();

    await handleApiPassthrough(peerRequest("203.0.113.5", "198.51.100.4"));

    const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
    expect(forwarded?.headers.get(CLIENT_IP_HEADER)).toBe("203.0.113.5");
  });

  it("removes an inbound seam header rather than letting a forged one through", async () => {
    // The seam header is an ordinary request header, so a caller can send one.
    // Every request WITH a peer stamps over it; this is the case that does not.
    const { handleApiPassthrough } = await importModule();
    const request = peerRequest();
    request.headers.set(CLIENT_IP_HEADER, "198.51.100.4");

    await handleApiPassthrough(request);

    const forwarded: Request | undefined = mocks.handle.mock.calls[0]?.[0];
    expect(forwarded?.headers.has(CLIENT_IP_HEADER)).toBe(false);
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
