import { afterEach, describe, expect, it, vi } from "vitest";

import {
  createLogIngestHandler,
  createServerLogger,
  type LogIngestHandler,
  type LogIngestOptions,
  type ServerLogRecord,
} from "./server";
import { REDACTED, type LogEvent } from "./log-event";

/**
 * The ingest handler's guard chain, the fields it stamps, and the server-side
 * logger.
 *
 * The route is unauthenticated by design, so the guards ARE the security model.
 * The client address is a guard input rather than a claim: it comes from the
 * host's `clientAddress` seam, and an inbound header naming one is ignored.
 */

const ORIGIN = "https://app.wallow.dev";
const NOW_MS = 1_785_499_201_000;

/** The header a caller could try to pass an address on. Nothing reads it. */
const CLIENT_IP_HEADER = "x-wallow-client-ip";

/**
 * A host that knows its peer. A real one reads the socket; this one reads a
 * header only so a spec can vary the address per request.
 */
function hostPeer(request: Request): string | undefined {
  return request.headers.get("x-spec-peer") ?? undefined;
}

function event(overrides: Partial<LogEvent> = {}): LogEvent {
  return {
    ts: "2026-07-31T12:00:00.000Z",
    level: "info",
    event: "form.submitted",
    attrs: {},
    ...overrides,
  };
}

function request(
  body: unknown,
  init: { method?: string; origin?: string | null; headers?: Record<string, string> } = {},
): Request {
  const headers: Record<string, string> = {
    "content-type": "application/json",
    ...(init.origin === null ? {} : { origin: init.origin ?? ORIGIN }),
    ...init.headers,
  };

  const method: string = init.method ?? "POST";

  return new Request("https://app.wallow.dev/bff/logs", {
    method,
    headers,
    // GET and HEAD cannot carry one, and the method guard runs first anyway.
    ...(method === "GET" || method === "HEAD"
      ? {}
      : { body: typeof body === "string" ? body : JSON.stringify(body) }),
  });
}

function handlerWith(overrides: Partial<LogIngestOptions> = {}): {
  handler: LogIngestHandler;
  sink: ReturnType<typeof vi.fn>;
} {
  const sink = vi.fn();

  return {
    sink,
    handler: createLogIngestHandler({
      service: "wallow-web",
      allowedOrigins: [ORIGIN],
      now: () => NOW_MS,
      sink,
      ...overrides,
    }),
  };
}

function recordsOf(sink: ReturnType<typeof vi.fn>): ServerLogRecord[] {
  return (sink.mock.calls[0]?.[0] ?? []) as ServerLogRecord[];
}

function firstRecord(sink: ReturnType<typeof vi.fn>): ServerLogRecord | undefined {
  return recordsOf(sink)[0];
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("the guard chain", () => {
  it("refuses anything but POST", async () => {
    const { handler } = handlerWith();

    const response = await handler(request({ events: [event()] }, { method: "GET" }));

    expect(response.status).toBe(405);
  });

  it("refuses a request with no Origin", async () => {
    // A browser sets Origin on every POST, and script cannot forge it — it is a
    // forbidden header name. Its absence is not a browser.
    const { handler } = handlerWith();

    const response = await handler(request({ events: [event()] }, { origin: null }));

    expect(response.status).toBe(403);
  });

  it("refuses an origin that is not on the list", async () => {
    const { handler } = handlerWith();

    const response = await handler(
      request({ events: [event()] }, { origin: "https://evil.example" }),
    );

    expect(response.status).toBe(403);
  });

  it("refuses everything when the allowlist is empty", async () => {
    // The failure direction a misconfigured deployment should take.
    const { handler } = handlerWith({ allowedOrigins: [] });

    const response = await handler(request({ events: [event()] }));

    expect(response.status).toBe(403);
  });

  it("resolves a function allowlist against the request it was handed", async () => {
    // What an app with no configured public origin passes: the origin THIS
    // request was addressed to, which only a page served from here can match.
    const { handler } = handlerWith({
      allowedOrigins: (incoming: Request) => [new URL(incoming.url).origin],
    });

    const accepted = await handler(request({ events: [event()] }));
    const refused = await handler(
      request({ events: [event()] }, { origin: "https://evil.example" }),
    );

    expect([accepted.status, refused.status]).toEqual([204, 403]);
  });

  it("rate-limits per address the host supplies", async () => {
    const { handler } = handlerWith({
      rateLimit: { limit: 1, windowMs: 60_000, maxTrackedKeys: 10 },
      clientAddress: hostPeer,
    });
    const headers = { "x-spec-peer": "203.0.113.7" };

    await handler(request({ events: [event()] }, { headers }));
    const second = await handler(request({ events: [event()] }, { headers }));
    const other = await handler(
      request({ events: [event()] }, { headers: { "x-spec-peer": "203.0.113.8" } }),
    );

    expect(second.status).toBe(429);
    expect(other.status).toBe(204);
  });

  it("gives a caller rotating the client-IP header no fresh buckets", async () => {
    // The key is the whole strength of an unauthenticated route's limiter. Read
    // off the wire it is attacker-chosen, and a limit of one becomes no limit.
    const { handler } = handlerWith({
      rateLimit: { limit: 1, windowMs: 60_000, maxTrackedKeys: 10 },
    });

    await handler(
      request({ events: [event()] }, { headers: { [CLIENT_IP_HEADER]: "203.0.113.7" } }),
    );
    const rotated = await handler(
      request({ events: [event()] }, { headers: { [CLIENT_IP_HEADER]: "203.0.113.8" } }),
    );

    expect(rotated.status).toBe(429);
  });

  it("puts every caller in one bucket when the host supplies no address", async () => {
    // Limiting too much is the right way to fail: a beacon flush sets no headers
    // and a misconfigured host answers nothing, and neither may open a bypass.
    const { handler } = handlerWith({
      rateLimit: { limit: 1, windowMs: 60_000, maxTrackedKeys: 10 },
      clientAddress: () => undefined,
    });

    await handler(request({ events: [event()] }));
    const second = await handler(request({ events: [event()] }));

    expect(second.status).toBe(429);
  });

  it("keeps its limiter across requests", async () => {
    // A limiter built per call counts to one and never refuses anything.
    const { handler } = handlerWith({
      rateLimit: { limit: 2, windowMs: 60_000, maxTrackedKeys: 10 },
    });

    const statuses: number[] = [];
    for (let index = 0; index < 3; index += 1) {
      const response = await handler(request({ events: [event()] }));
      statuses.push(response.status);
    }

    expect(statuses).toEqual([204, 204, 429]);
  });

  it("refuses a declared content-length past the cap", async () => {
    const { handler } = handlerWith();

    const response = await handler(
      request({ events: [event()] }, { headers: { "content-length": "999999" } }),
    );

    expect(response.status).toBe(413);
  });

  it("refuses an actual body past the cap", async () => {
    const { handler } = handlerWith({
      limits: {
        maxBodyBytes: 50,
        maxEventsPerBatch: 100,
        maxEventNameLength: 120,
        maxAttributesPerEvent: 32,
      },
    });

    const response = await handler(request({ events: [event()] }));

    expect(response.status).toBe(413);
  });

  it("refuses a body that is not JSON", async () => {
    const { handler } = handlerWith();

    const response = await handler(request("{not json"));

    expect(response.status).toBe(400);
  });

  it("refuses a batch whose events are not events", async () => {
    const { handler } = handlerWith();

    const response = await handler(request({ events: [event({ event: "user did a thing" })] }));

    expect(response.status).toBe(400);
    await expect(response.json()).resolves.toEqual({
      reason: "event name is not a dotted low-cardinality name",
    });
  });
});

describe("authorization", () => {
  it("refuses when the verifier says no", async () => {
    const { handler, sink } = handlerWith({ authorize: () => false });

    const response = await handler(request({ events: [event()] }));

    expect(response.status).toBe(403);
    expect(sink).not.toHaveBeenCalled();
  });

  it("hands the verifier the parsed batch, token and all", async () => {
    // sendBeacon cannot set headers, so a terminal flush carries the token in the
    // body and the verifier has to be able to see it.
    const authorize = vi.fn().mockReturnValue(true);
    const { handler } = handlerWith({ authorize });

    await handler(request({ events: [event()], csrfToken: "t0ken" }));

    expect(authorize).toHaveBeenCalledWith(
      expect.any(Request),
      expect.objectContaining({ csrfToken: "t0ken" }),
    );
  });

  it("asks for nothing when an app supplies no verifier", async () => {
    // Correct for a passthrough app: it holds no session, so it has no token.
    const { handler } = handlerWith();

    const response = await handler(request({ events: [event()] }));

    expect(response.status).toBe(204);
  });
});

describe("what the server stamps", () => {
  it("stamps its own service, not the payload's", async () => {
    const { handler, sink } = handlerWith();

    await handler(request({ events: [{ ...event(), service: "spoofed" }] }));

    expect(recordsOf(sink)[0]?.service).toBe("wallow-web");
  });

  it("keeps the client's clock apart from its own", async () => {
    const { handler, sink } = handlerWith();

    await handler(request({ events: [event({ ts: "2020-01-01T00:00:00.000Z" })] }));

    expect(recordsOf(sink)[0]).toMatchObject({
      ts: new Date(NOW_MS).toISOString(),
      clientTs: "2020-01-01T00:00:00.000Z",
    });
  });

  it("stamps the client address the host supplies", async () => {
    const { handler, sink } = handlerWith({ clientAddress: hostPeer });

    await handler(request({ events: [event()] }, { headers: { "x-spec-peer": "203.0.113.7" } }));

    expect(recordsOf(sink)[0]?.clientIp).toBe("203.0.113.7");
  });

  it("ignores a client-IP header the caller sent", async () => {
    // `client.address` reads as server-stamped in the collector, so anything
    // able to produce a valid Origin — page script on a same-origin fetch —
    // must not be able to write it.
    const { handler, sink } = handlerWith();

    await handler(
      request({ events: [event()] }, { headers: { [CLIENT_IP_HEADER]: "203.0.113.7" } }),
    );

    expect(recordsOf(sink)[0]?.clientIp).toBeUndefined();
  });

  it("prefers the host's address over a header claiming another", async () => {
    const { handler, sink } = handlerWith({ clientAddress: hostPeer });

    await handler(
      request(
        { events: [event()] },
        { headers: { "x-spec-peer": "198.51.100.4", [CLIENT_IP_HEADER]: "203.0.113.7" } },
      ),
    );

    expect(recordsOf(sink)[0]?.clientIp).toBe("198.51.100.4");
  });

  it("stamps no client address when the host answers with an empty string", async () => {
    const { handler, sink } = handlerWith({ clientAddress: () => "" });

    await handler(request({ events: [event()] }));

    expect(recordsOf(sink)[0]?.clientIp).toBeUndefined();
  });

  it("falls back to the request's correlation header", async () => {
    const { handler, sink } = handlerWith();

    await handler(request({ events: [event()] }, { headers: { "x-request-id": "req-9" } }));

    expect(recordsOf(sink)[0]?.correlationId).toBe("req-9");
  });

  it("prefers the correlation id the event carries", async () => {
    const { handler, sink } = handlerWith();

    await handler(
      request(
        { events: [event({ correlationId: "req-1" })] },
        { headers: { "x-request-id": "req-9" } },
      ),
    );

    expect(recordsOf(sink)[0]?.correlationId).toBe("req-1");
  });

  it("stamps the session fields the app resolves", async () => {
    const { handler, sink } = handlerWith({
      context: () => ({ userId: "u1", tenantId: "t1" }),
    });

    await handler(request({ events: [event()] }));

    expect(recordsOf(sink)[0]).toMatchObject({ userId: "u1", tenantId: "t1" });
  });

  it("redacts again, authoritatively", async () => {
    // A fork cannot bypass this pass from a page it does not control.
    const { handler, sink } = handlerWith();

    await handler(request({ events: [event({ attrs: { email: "a@b.dev" } })] }));

    expect(recordsOf(sink)[0]?.attrs).toEqual({ email: REDACTED });
  });
});

describe("answering the page", () => {
  it("answers 204 with no body for a valid batch", async () => {
    const { handler } = handlerWith();

    const response = await handler(request({ events: [event()] }));

    expect(response.status).toBe(204);
    expect(await response.text()).toBe("");
  });

  it("answers 204 even when the sink fails", async () => {
    // The batch was valid and accepted; a failing collector is not the page's
    // problem, and a 500 here would surface telemetry outages as app errors.
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const { handler } = handlerWith({
      sink: () => {
        throw new Error("collector down");
      },
    });

    const response = await handler(request({ events: [event()] }));

    expect(response.status).toBe(204);
    expect(warn).toHaveBeenCalled();
  });

  it("emits OTLP when a collector is configured", async () => {
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 200 }));
    const handler = createLogIngestHandler({
      service: "wallow-web",
      allowedOrigins: [ORIGIN],
      otlpEndpoint: "http://collector:4318",
      now: () => NOW_MS,
      fetch: fetchImpl,
    });

    await handler(request({ events: [event()] }));

    expect(fetchImpl).toHaveBeenCalledWith("http://collector:4318/v1/logs", expect.anything());
  });
});

describe("createServerLogger", () => {
  it("records at and above its level", () => {
    const sink = vi.fn();
    const logger = createServerLogger({
      service: "wallow-web",
      level: "warn",
      console: false,
      sink,
    });

    logger.info("a.info");
    logger.error("a.error");

    expect(sink).toHaveBeenCalledTimes(1);
    expect(firstRecord(sink)?.event).toBe("a.error");
  });

  it("stamps the service and redacts", () => {
    const sink = vi.fn();
    const logger = createServerLogger({ service: "wallow-auth", console: false, sink });

    logger.info("bff.session.opened", { email: "a@b.dev" });

    expect(firstRecord(sink)).toMatchObject({
      service: "wallow-auth",
      attrs: { email: REDACTED },
    });
  });

  it("normalizes a thrown Error", () => {
    const sink = vi.fn();
    const logger = createServerLogger({ service: "wallow-web", console: false, sink });

    logger.error("redis.client.failed", {}, new TypeError("boom"));

    expect(firstRecord(sink)?.error).toMatchObject({
      name: "TypeError",
      message: "boom",
    });
  });

  it("merges a child's attributes over its parent's", () => {
    const sink = vi.fn();
    const logger = createServerLogger({
      service: "wallow-web",
      console: false,
      sink,
      attrs: { component: "root" },
    });

    logger.child({ component: "bff" }).info("bff.started");

    expect(firstRecord(sink)?.attrs).toEqual({ component: "bff" });
  });

  it("writes to stdout by default, collector or not", () => {
    // In a container stdout is the one path that still works when the collector
    // does not, and it is what `docker logs` shows.
    const log = vi.spyOn(console, "log").mockImplementation(() => {});
    const logger = createServerLogger({ service: "wallow-web", sink: vi.fn() });

    logger.info("bff.started");

    expect(log).toHaveBeenCalledTimes(1);
    expect(JSON.parse(log.mock.calls[0]?.[0] as string)).toMatchObject({ event: "bff.started" });
  });
});
