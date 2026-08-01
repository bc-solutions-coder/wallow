import { afterEach, describe, expect, it, vi } from "vitest";

import { createLogger, type Logger, type LogBatch } from "./index";
import { REDACTED } from "./log-event";

/**
 * The browser core's buffering, filtering, redaction and transport rules, driven
 * on node.
 *
 * Outside a page `createLogger` registers no listeners and starts no timer,
 * which is exactly what makes the buffer assertable without a DOM. The two
 * behaviours that genuinely need a browser — `pagehide` and `visibilitychange` —
 * live in `logger.test.tsx`.
 */

const ENDPOINT = "/bff/logs";

function stubFetch(response: Response | Error = new Response(null, { status: 204 })) {
  const fetchImpl = vi.fn<typeof fetch>(
    response instanceof Error
      ? () => Promise.reject(response)
      : () => Promise.resolve(response.clone()),
  );
  vi.stubGlobal("fetch", fetchImpl);

  return fetchImpl;
}

function bodyOf(fetchImpl: ReturnType<typeof stubFetch>, call = 0): LogBatch {
  const init: RequestInit | undefined = fetchImpl.mock.calls[call]?.[1];

  return JSON.parse(init?.body as string) as LogBatch;
}

function headersOf(fetchImpl: ReturnType<typeof stubFetch>, call = 0): Record<string, string> {
  return (fetchImpl.mock.calls[call]?.[1]?.headers ?? {}) as Record<string, string>;
}

function makeLogger(overrides: Partial<Parameters<typeof createLogger>[0]> = {}): Logger {
  return createLogger({
    service: "wallow-web",
    endpoint: ENDPOINT,
    flushAtEvents: 1000,
    flushIntervalMs: 0,
    ...overrides,
  });
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("buffering", () => {
  it("sends nothing until asked", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger();

    logger.info("form.submitted");

    expect(fetchImpl).not.toHaveBeenCalled();

    await logger.flush();

    expect(bodyOf(fetchImpl).events).toHaveLength(1);
  });

  it("flushes on its own once the buffer reaches the trigger", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger({ flushAtEvents: 2 });

    logger.info("a.one");
    logger.info("a.two");

    await vi.waitFor(() => {
      expect(fetchImpl).toHaveBeenCalledTimes(1);
    });
    expect(bodyOf(fetchImpl).events.map((event) => event.event)).toEqual(["a.one", "a.two"]);
  });

  it("flushes nothing when the buffer is empty", async () => {
    const fetchImpl = stubFetch();

    await makeLogger().flush();

    expect(fetchImpl).not.toHaveBeenCalled();
  });

  it("drops the OLDEST events past the ceiling and reports the count", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger({ maxBufferedEvents: 2 });

    logger.info("a.one");
    logger.info("a.two");
    logger.info("a.three");
    await logger.flush();

    expect(bodyOf(fetchImpl).events.map((event) => event.event)).toEqual([
      "logger.dropped",
      "a.two",
      "a.three",
    ]);
    expect(bodyOf(fetchImpl).events[0]?.attrs).toEqual({ count: 1 });
  });
});

describe("what reaches the wire", () => {
  it("drops everything below the configured level", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger({ level: "warn" });

    logger.debug("a.debug");
    logger.info("a.info");
    logger.warn("a.warn");
    logger.error("a.error");
    await logger.flush();

    expect(bodyOf(fetchImpl).events.map((event) => event.event)).toEqual(["a.warn", "a.error"]);
  });

  it("redacts before an event is buffered", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger();

    logger.info("auth.attempted", { email: "a@b.dev", attempt: 1 });
    await logger.flush();

    expect(bodyOf(fetchImpl).events[0]?.attrs).toEqual({ email: REDACTED, attempt: 1 });
  });

  it("never names its own service", async () => {
    // The ingest handler stamps the service it was configured with: a record that
    // names its own is one any page can forge.
    const fetchImpl = stubFetch();
    const logger = makeLogger();

    logger.info("form.submitted");
    await logger.flush();

    expect(JSON.stringify(bodyOf(fetchImpl))).not.toContain("wallow-web");
  });

  it("stamps the correlation id at record time", async () => {
    const fetchImpl = stubFetch();
    let current = "req-1";
    const logger = makeLogger({ getCorrelationId: () => current });

    logger.info("a.one");
    current = "req-2";
    logger.info("a.two");
    await logger.flush();

    expect(bodyOf(fetchImpl).events.map((event) => event.correlationId)).toEqual([
      "req-1",
      "req-2",
    ]);
  });

  it("normalizes a thrown Error into three JSON-safe fields", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger();

    logger.error("bff.logout.failed", {}, new TypeError("boom"));
    await logger.flush();

    expect(bodyOf(fetchImpl).events[0]?.error).toMatchObject({
      name: "TypeError",
      message: "boom",
    });
  });

  it("normalizes a thrown non-Error", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger();

    logger.error("bff.logout.failed", {}, "just a string");
    await logger.flush();

    expect(bodyOf(fetchImpl).events[0]?.error).toEqual({
      name: "NonError",
      message: "just a string",
    });
  });

  it("sends the csrf token as a header on the fetch path", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger({ getCsrfToken: () => "t0ken" });

    logger.info("form.submitted");
    await logger.flush();

    expect(headersOf(fetchImpl)["x-csrf-token"]).toBe("t0ken");
    expect(bodyOf(fetchImpl).csrfToken).toBeUndefined();
  });

  it("sends no csrf header for an app that holds no session", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger();

    logger.info("form.submitted");
    await logger.flush();

    expect(headersOf(fetchImpl)).not.toHaveProperty("x-csrf-token");
  });

  it("keeps the request same-origin and keepalive", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger();

    logger.info("form.submitted");
    await logger.flush();

    expect(fetchImpl.mock.calls[0]?.[1]).toMatchObject({
      method: "POST",
      keepalive: true,
      credentials: "same-origin",
    });
  });
});

describe("child loggers", () => {
  it("merges its attributes over the parent's", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger({ attrs: { app: "web", scope: "root" } });

    logger.child({ scope: "checkout" }).info("form.submitted");
    await logger.flush();

    expect(bodyOf(fetchImpl).events[0]?.attrs).toEqual({ app: "web", scope: "checkout" });
  });

  it("shares one buffer with its parent", async () => {
    // A second buffer would race two requests to the same route for no gain.
    const fetchImpl = stubFetch();
    const logger = makeLogger();

    logger.info("a.parent");
    logger.child({}).info("a.child");
    await logger.flush();

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    expect(bodyOf(fetchImpl).events).toHaveLength(2);
  });
});

describe("transport failure", () => {
  it("falls back to the console rather than back into itself", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    stubFetch(new Error("offline"));
    const logger = makeLogger();

    logger.info("form.submitted");

    await expect(logger.flush()).resolves.toBeUndefined();
    expect(warn).toHaveBeenCalled();
  });

  it("treats a refusing route as a failure", async () => {
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const fetchImpl = stubFetch(new Response(null, { status: 500 }));
    const logger = makeLogger();

    logger.info("form.submitted");
    await logger.flush();
    logger.info("a.second");
    await logger.flush();

    // The backoff window is open, so the second flush never reaches the network.
    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });

  it("sends again once the backoff window closes", async () => {
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const fetchImpl = stubFetch(new Error("offline"));
    const logger = makeLogger({ transportBackoffMs: 0 });

    logger.info("a.one");
    await logger.flush();
    logger.info("a.two");
    await logger.flush();

    expect(fetchImpl).toHaveBeenCalledTimes(2);
  });

  it("does not requeue the events it failed to send", async () => {
    // A requeue turns one unreachable route into a buffer that refills itself
    // every window and never drains.
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const fetchImpl = stubFetch(new Error("offline"));
    const logger = makeLogger({ transportBackoffMs: 0 });

    logger.info("a.one");
    await logger.flush();
    await logger.flush();

    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });
});

describe("oversized batches", () => {
  it("splits rather than truncating", async () => {
    const fetchImpl = stubFetch();
    const logger = makeLogger({ maxBodyBytes: 400 });

    logger.info("a.one", { pad: "x".repeat(150) });
    logger.info("a.two", { pad: "y".repeat(150) });
    await logger.flush();

    expect(fetchImpl).toHaveBeenCalledTimes(2);
    expect(
      [bodyOf(fetchImpl, 0), bodyOf(fetchImpl, 1)].flatMap((batch) => batch.events),
    ).toHaveLength(2);
  });

  it("drops a single record that alone exceeds the cap", async () => {
    // Truncating would ship a record whose meaning changed on the way out.
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const fetchImpl = stubFetch();
    const logger = makeLogger({ maxBodyBytes: 100 });

    logger.info("a.one", { pad: "x".repeat(500) });
    await logger.flush();

    expect(fetchImpl).not.toHaveBeenCalled();
    expect(warn).toHaveBeenCalled();
  });
});
