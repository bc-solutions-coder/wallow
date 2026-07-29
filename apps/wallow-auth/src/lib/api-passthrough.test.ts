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
function peerRequest(ip?: string): Request & { ip?: string } {
  const request = new Request("http://localhost:3002/v1/ping") as Request & { ip?: string };
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
