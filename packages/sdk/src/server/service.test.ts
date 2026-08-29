import { afterEach, describe, expect, it, vi } from "vitest";

import { createServiceClient, loadServiceConfigFromEnv } from "./service";
import { createMemoryRedis } from "./store/memory";

/**
 * Hermetic mock of openid-client, as in `oidc.test.ts`: the service client's
 * only network I/O is discovery and the client-credentials grant, both stubbed.
 * Every test uses a unique issuer because discovery is cached per metadata URL.
 */
const { discoveryMock, clientCredentialsGrantMock } = vi.hoisted(() => ({
  discoveryMock: vi.fn(),
  clientCredentialsGrantMock: vi.fn(),
}));

// `./server/service` is its own subpath precisely so a service-account consumer
// never pulls the BFF handler graph into its bundle. Loading either module from
// here therefore fails the whole file, not just one test.
vi.mock("./handlers", () => {
  throw new Error("service.ts must not import the BFF handler graph (./handlers)");
});
vi.mock("./proxy", () => {
  throw new Error("service.ts must not import the BFF proxy (./proxy)");
});

const { redisCreateClientMock } = vi.hoisted(() => {
  const data: Map<string, string> = new Map<string, string>();
  const redisCreateClientMock: ReturnType<typeof vi.fn> = vi.fn(() => ({
    on: vi.fn(),
    connect: vi.fn(() => Promise.resolve()),
    get: (key: string): Promise<string | null> => Promise.resolve(data.get(key) ?? null),
    set: (key: string, value: string, options?: { NX?: true }): Promise<string | null> => {
      if (options?.NX === true && data.has(key)) {
        return Promise.resolve(null);
      }
      data.set(key, value);
      return Promise.resolve("OK");
    },
    del: (key: string): Promise<number> => Promise.resolve(data.delete(key) ? 1 : 0),
  }));
  return { redisCreateClientMock };
});

/** `redis` is an OPTIONAL peer the client imports lazily; stand it in here. */
vi.mock("redis", () => ({ createClient: redisCreateClientMock }));

vi.mock("openid-client", () => ({
  discovery: discoveryMock,
  clientCredentialsGrant: clientCredentialsGrantMock,
  allowInsecureRequests: vi.fn(),
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

function serviceEnv(overrides: Record<string, string | undefined> = {}): NodeJS.ProcessEnv {
  return {
    OIDC_ISSUER: `https://auth.${crypto.randomUUID()}.example`,
    OIDC_SERVICE_CLIENT_ID: "sa-acme-contact",
    OIDC_SERVICE_CLIENT_SECRET: "sa-secret",
    OIDC_SERVICE_SCOPES: "inquiries.write",
    BFF_API_BASE_URL: "https://api.example/api",
    ...overrides,
  } as NodeJS.ProcessEnv;
}

describe("loadServiceConfigFromEnv", () => {
  it("reports every missing required variable in one error", () => {
    expect(() => loadServiceConfigFromEnv({} as NodeJS.ProcessEnv)).toThrow(
      /OIDC_ISSUER[\s\S]*OIDC_SERVICE_CLIENT_ID[\s\S]*OIDC_SERVICE_CLIENT_SECRET[\s\S]*OIDC_SERVICE_SCOPES[\s\S]*BFF_API_BASE_URL/u,
    );
  });

  it("reads only the service subset: user-BFF variables are neither required nor read", () => {
    const config = loadServiceConfigFromEnv(serviceEnv({ OIDC_CLIENT_ID: "app-other" }));
    expect(config.clientId).toBe("sa-acme-contact");
    expect(config.scopes).toEqual(["inquiries.write"]);
    expect(config.apiBaseUrl).toBe("https://api.example/api");
    expect(config.metadataUrl).toBeUndefined();
  });

  it("splits OIDC_SERVICE_SCOPES on whitespace and takes OIDC_METADATA_URL when set", () => {
    const config = loadServiceConfigFromEnv(
      serviceEnv({
        OIDC_SERVICE_SCOPES: "  inquiries.write   users.read ",
        OIDC_METADATA_URL: "http://api:5001/.well-known/openid-configuration",
      }),
    );
    expect(config.scopes).toEqual(["inquiries.write", "users.read"]);
    expect(config.metadataUrl).toBe("http://api:5001/.well-known/openid-configuration");
  });
});

/** A discovery stub whose configuration is recognisable to the grant mock. */
function stubDiscovery(): void {
  discoveryMock.mockImplementation((server: URL) =>
    Promise.resolve({
      serverMetadata: (): Record<string, unknown> => ({
        issuer: new URL(server).origin,
        token_endpoint: `${new URL(server).origin}/connect/token`,
      }),
    }),
  );
}

function grantResponse(accessToken: string, expiresIn: number = 3600) {
  return {
    access_token: accessToken,
    token_type: "bearer",
    expires_in: expiresIn,
    expiresIn: () => expiresIn,
  };
}

/** Yield for a few real milliseconds so a pending lock acquisition can land. */
function tick(): Promise<void> {
  return new Promise<void>((resolve): void => {
    setTimeout(resolve, 10);
  });
}

/** A transport that records every request and answers with `status`. */
function recordingFetch(status: number = 200): {
  calls: Request[];
  fetch: typeof globalThis.fetch;
} {
  const calls: Request[] = [];
  return {
    calls,
    fetch: (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
      const request: Request = input instanceof Request ? input : new Request(input, init);
      calls.push(request);
      return Promise.resolve(Response.json({ ok: true }, { status }));
    },
  };
}

describe("createServiceClient — bearer", () => {
  it("obtains a client-credentials token for the configured scopes and sends it as a bearer", async () => {
    stubDiscovery();
    clientCredentialsGrantMock.mockResolvedValue(grantResponse("svc-token-1"));
    const transport = recordingFetch();

    const service = createServiceClient({ env: serviceEnv(), fetch: transport.fetch });
    await service.client.get({ url: "/v1/ping" });

    expect(clientCredentialsGrantMock).toHaveBeenCalledTimes(1);
    expect(clientCredentialsGrantMock.mock.calls[0]?.[1]).toEqual({ scope: "inquiries.write" });
    expect(transport.calls).toHaveLength(1);
    expect(transport.calls[0]?.headers.get("authorization")).toBe("Bearer svc-token-1");
    expect(transport.calls[0]?.url).toBe("https://api.example/api/v1/ping");
  });
});

describe("createServiceClient — caching and coordination", () => {
  it("fetches the token once under concurrency and reuses it for later calls", async () => {
    stubDiscovery();
    clientCredentialsGrantMock.mockResolvedValue(grantResponse("svc-token-1"));
    const transport = recordingFetch();
    const service = createServiceClient({ env: serviceEnv(), fetch: transport.fetch });

    await Promise.all([
      service.client.get({ url: "/v1/a" }),
      service.client.get({ url: "/v1/b" }),
      service.client.get({ url: "/v1/c" }),
    ]);
    await service.client.get({ url: "/v1/d" });

    expect(clientCredentialsGrantMock).toHaveBeenCalledTimes(1);
    expect(transport.calls.map((r) => r.headers.get("authorization"))).toEqual(
      Array.from({ length: 4 }, () => "Bearer svc-token-1"),
    );
  });

  it("shares the token between instances that share a store", async () => {
    stubDiscovery();
    clientCredentialsGrantMock.mockResolvedValue(grantResponse("svc-token-shared"));
    const env = serviceEnv();
    const store = createMemoryRedis();
    const first = createServiceClient({ env, store, fetch: recordingFetch().fetch });
    const secondTransport = recordingFetch();
    const second = createServiceClient({ env, store, fetch: secondTransport.fetch });

    await first.accessToken();
    await second.client.get({ url: "/v1/x" });

    expect(clientCredentialsGrantMock).toHaveBeenCalledTimes(1);
    expect(secondTransport.calls[0]?.headers.get("authorization")).toBe("Bearer svc-token-shared");
  });

  it("shares the token cache through REDIS_URL when no store is supplied", async () => {
    stubDiscovery();
    clientCredentialsGrantMock.mockResolvedValue(grantResponse("shared-token"));
    const transport: ReturnType<typeof recordingFetch> = recordingFetch(200);
    const env: NodeJS.ProcessEnv = serviceEnv({ REDIS_URL: "redis://valkey:6379" });

    const first = createServiceClient({ env, fetch: transport.fetch });
    const second = createServiceClient({ env, fetch: transport.fetch });
    // Construction touches no network: the connection opens on first use.
    expect(redisCreateClientMock).not.toHaveBeenCalled();

    await expect(first.accessToken()).resolves.toBe("shared-token");
    await expect(second.accessToken()).resolves.toBe("shared-token");

    expect(redisCreateClientMock).toHaveBeenCalledWith({ url: "redis://valkey:6379" });
    expect(clientCredentialsGrantMock).toHaveBeenCalledTimes(1);
  });

  it("waits for the replica holding the refresh lock instead of fetching a second token", async () => {
    stubDiscovery();
    const env = serviceEnv();
    const store = createMemoryRedis();
    const winner = createServiceClient({ env, store, fetch: recordingFetch().fetch });
    const loser = createServiceClient({ env, store, fetch: recordingFetch().fetch });

    let releaseGrant: (value: ReturnType<typeof grantResponse>) => void = () => {};
    clientCredentialsGrantMock.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          releaseGrant = resolve;
        }),
    );

    const winnerToken: Promise<string> = winner.accessToken();
    // Let the winner take the lock before the loser asks.
    await tick();
    const loserToken: Promise<string> = loser.accessToken();
    await tick();
    releaseGrant(grantResponse("svc-token-locked"));

    expect(await winnerToken).toBe("svc-token-locked");
    expect(await loserToken).toBe("svc-token-locked");
    expect(clientCredentialsGrantMock).toHaveBeenCalledTimes(1);
  });

  it("renews the token before it expires rather than sending one about to lapse", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-08-29T12:00:00Z"));
    stubDiscovery();
    clientCredentialsGrantMock
      .mockResolvedValueOnce(grantResponse("svc-token-1", 120))
      .mockResolvedValueOnce(grantResponse("svc-token-2", 120));
    const transport = recordingFetch();
    const service = createServiceClient({ env: serviceEnv(), fetch: transport.fetch });

    await service.client.get({ url: "/v1/first" });
    // 60 s left: still fresh.
    vi.setSystemTime(new Date("2026-08-29T12:01:00Z"));
    await service.client.get({ url: "/v1/second" });
    // 15 s left: inside the skew.
    vi.setSystemTime(new Date("2026-08-29T12:01:45Z"));
    await service.client.get({ url: "/v1/third" });

    expect(clientCredentialsGrantMock).toHaveBeenCalledTimes(2);
    expect(transport.calls.map((r) => r.headers.get("authorization"))).toEqual([
      "Bearer svc-token-1",
      "Bearer svc-token-1",
      "Bearer svc-token-2",
    ]);
  });
});

describe("createServiceClient — rejected bearer", () => {
  it("refetches the token and replays the request exactly once after a 401", async () => {
    stubDiscovery();
    clientCredentialsGrantMock
      .mockResolvedValueOnce(grantResponse("svc-token-stale"))
      .mockResolvedValueOnce(grantResponse("svc-token-fresh"));
    const calls: Request[] = [];
    const bodies: string[] = [];
    const transport: typeof globalThis.fetch = async (input, init) => {
      const request: Request = input instanceof Request ? input : new Request(input, init);
      calls.push(request);
      bodies.push(await request.text());
      return request.headers.get("authorization") === "Bearer svc-token-stale"
        ? new Response(null, { status: 401 })
        : Response.json({ ok: true });
    };
    const service = createServiceClient({ env: serviceEnv(), fetch: transport });

    await service.client.post({ url: "/v1/inquiries", body: { subject: "hi" } });

    expect(calls).toHaveLength(2);
    expect(calls.map((r) => r.headers.get("authorization"))).toEqual([
      "Bearer svc-token-stale",
      "Bearer svc-token-fresh",
    ]);
    expect(bodies[1]).toBe(bodies[0]);
    expect(clientCredentialsGrantMock).toHaveBeenCalledTimes(2);
  });

  it("surfaces the second 401 instead of replaying again", async () => {
    stubDiscovery();
    clientCredentialsGrantMock.mockResolvedValue(grantResponse("svc-token-rejected"));
    const transport = recordingFetch(401);
    const service = createServiceClient({ env: serviceEnv(), fetch: transport.fetch });

    await expect(service.client.get({ url: "/v1/ping" })).rejects.toThrow();
    expect(transport.calls).toHaveLength(2);
  });
});
