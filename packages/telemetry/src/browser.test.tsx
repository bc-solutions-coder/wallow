import { afterEach, expect, test, vi } from "vitest";
import { initializeBrowserTelemetry } from "./index.js";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

test("exports sanitized Faro logs and performance through only its same-origin relay", async () => {
  const batches: string[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (_input: unknown, init?: RequestInit) => {
      if (init?.method === "POST") {
        batches.push(String(init.body));
        return new Response(null, { status: 204 });
      }
      return Response.json({ context: "opaque-context" });
    }),
  );
  const telemetry = initializeBrowserTelemetry({ relayPath: "/telemetry" });
  try {
    await telemetry.ready;
    telemetry.logger.info("checkout.completed", {
      email: "private@example.com",
      nested: { token: "secret-marker" },
      url: "https://example.com/cart?secret=query-marker",
    });
    telemetry.measure("checkout.duration", 12);
    await telemetry.flush();
    const wire = batches.join("");
    expect(wire).toContain("checkout.completed");
    expect(wire).toContain("checkout.duration");
    expect(wire).not.toMatch(/private@example|secret-marker|query-marker/u);
  } finally {
    await telemetry.dispose();
  }
});

test("drops old buffered context and bounds memory while the relay is unavailable", async () => {
  const batches: string[] = [];
  let available = true;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (_input: unknown, init?: RequestInit) => {
      if (!available) {
        throw new Error("relay offline");
      }
      if (init?.method === "POST") {
        batches.push(String(init.body));
        return new Response(null, { status: 204 });
      }
      return Response.json({ context: "opaque-context" });
    }),
  );
  const telemetry = initializeBrowserTelemetry({ relayPath: "/telemetry" });
  try {
    await telemetry.ready;
    telemetry.logger.info("before.logout");
    await telemetry.resetContext();
    telemetry.logger.info("after.logout");
    await telemetry.flush();
    expect(batches.join("")).not.toContain("before.logout");
    expect(batches.join("")).toContain("after.logout");
    available = false;
    for (let index = 0; index < 2000; index += 1) {
      telemetry.logger.info("buffer.fill", { data: "x".repeat(900), index });
    }
    expect(telemetry.stats().queuedBytes).toBeLessThanOrEqual(262144);
    await telemetry.flush();
    expect(telemetry.stats().dropped).toBeGreaterThan(0);
    expect(telemetry.stats().failed).toBeGreaterThan(0);
  } finally {
    await telemetry.dispose();
  }
});

test("correlates owned request failures and keeps handled exceptions out of crash counts", async () => {
  const batches: string[] = [];
  const outgoing: Request[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: Request | URL, init?: RequestInit) => {
      if (input instanceof Request) {
        outgoing.push(input);
        return new Response(null, { status: 500 });
      }
      if (init?.method === "POST") {
        batches.push(String(init.body));
        return new Response(null, { status: 204 });
      }
      return Response.json({ context: "opaque-context" });
    }),
  );
  const telemetry = initializeBrowserTelemetry({ relayPath: "/telemetry" });
  try {
    await telemetry.ready;
    const handled = new Error("private@example.com handled secret");
    telemetry.captureException(handled, true);
    telemetry.captureException(handled);
    const failure = new Error("private@example.com unexpected secret");
    telemetry.captureException(failure);
    telemetry.captureException(failure);
    const ownedResponse = await telemetry.fetch("/owned", {
      redirect: "error",
      headers: { baggage: "secret-baggage" },
    });
    expect(ownedResponse.status).toBe(500);
    const unownedResponse = await telemetry.fetch("https://unowned.example/path", {
      headers: { traceparent: "forged-trace" },
    });
    expect(unownedResponse.status).toBe(500);
    await telemetry.flush();
    expect(outgoing[0]?.headers.get("traceparent")).toMatch(/^00-[a-f0-9]{32}-[a-f0-9]{16}-01$/u);
    expect(outgoing[0]?.headers.has("baggage")).toBe(false);
    expect(outgoing[1]?.headers.has("traceparent")).toBe(false);
    const wire = batches.join("");
    expect(wire.match(/Unexpected application error/gu)).toHaveLength(1);
    expect(wire).toContain("application.failure.handled");
    expect(wire).toContain("browser.request");
    expect(wire).not.toContain("private@example.com");
  } finally {
    await telemetry.dispose();
  }
});

test("counts oversize loss and splits more than 256 compact logs without discarding them", async () => {
  const messages: string[] = [];
  const sizes: number[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (_input: unknown, init?: RequestInit) => {
      if (init?.method === "POST") {
        const batch: { logs: { message: string }[] } = JSON.parse(String(init.body));
        messages.push(...batch.logs.map((log) => log.message));
        sizes.push(batch.logs.length);
        return new Response(null, { status: 204 });
      }
      return Response.json({ context: "opaque-context" });
    }),
  );
  const telemetry = initializeBrowserTelemetry({ relayPath: "/telemetry" });
  try {
    await telemetry.ready;
    const before = telemetry.stats().dropped;
    telemetry.logger.info(
      "oversize.log",
      Object.fromEntries(
        Array.from({ length: 32 }, (_, index) => [`field${index}`, "x".repeat(900)]),
      ),
    );
    expect(telemetry.stats().dropped).toBeGreaterThan(before);
    for (let index = 0; index < 600; index += 1) {
      telemetry.logger.info("compact.log");
    }
    await telemetry.flush();
    expect(messages.filter((message) => message === "compact.log")).toHaveLength(600);
    expect(sizes.every((count) => count <= 256)).toBe(true);
  } finally {
    await telemetry.dispose();
  }
});

test("an old flush cannot discard events queued after a new context handshake", async () => {
  const delayed = Promise.withResolvers<Response>();
  let first = true;
  const bodies: string[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (_input: unknown, init?: RequestInit) => {
      if (init?.method === "POST") {
        bodies.push(String(init.body));
        if (first) {
          first = false;
          return delayed.promise;
        }
        return new Response(null, { status: 204 });
      }
      return Response.json({ context: "opaque-context" });
    }),
  );
  const telemetry = initializeBrowserTelemetry({ relayPath: "/telemetry" });
  try {
    await telemetry.ready;
    telemetry.logger.info("old.context");
    const oldFlush = telemetry.flush();
    await expect.poll(() => bodies.length).toBe(1);
    await telemetry.resetContext();
    telemetry.logger.info("new.context");
    delayed.resolve(new Response(null, { status: 409 }));
    await oldFlush;
    await telemetry.flush();
    expect(bodies.at(-1)).toContain("new.context");
  } finally {
    delayed.resolve(new Response(null, { status: 204 }));
    await telemetry.dispose();
  }
});

test("does not attribute an old in-flight operation to a new context or change fetch redirect policy", async () => {
  const delayed = Promise.withResolvers<Response>();
  const bodies: string[] = [];
  const requests: Request[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: unknown, init?: RequestInit) => {
      if (input instanceof Request) {
        requests.push(input);
        return delayed.promise;
      }
      if (init?.method === "POST") {
        bodies.push(String(init.body));
        return new Response(null, { status: 204 });
      }
      return Response.json({ context: "opaque-context" });
    }),
  );
  const telemetry = initializeBrowserTelemetry({ relayPath: "/telemetry" });
  try {
    await telemetry.ready;
    const operation = telemetry.fetch("/ordinary-redirect");
    await telemetry.resetContext();
    delayed.resolve(new Response(null, { status: 500 }));
    const result = await operation;
    expect(result.status).toBe(500);
    expect(requests[0]?.redirect).toBe("follow");
    expect(requests[0]?.headers.has("traceparent")).toBe(false);
    await telemetry.flush();
    expect(bodies.join("")).not.toContain("browser.request");
    expect(telemetry.stats().dropped).toBeGreaterThan(0);
  } finally {
    delayed.resolve(new Response(null, { status: 204 }));
    await telemetry.dispose();
  }
});

test("supports one active browser session and reuses document instrumentation after disposal", async () => {
  const batches: string[] = [];
  const delayed = Promise.withResolvers<Response>();
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: unknown, init?: RequestInit) => {
      if (input instanceof Request) {
        return delayed.promise;
      }
      if (init?.method === "POST") {
        batches.push(String(init.body));
        return new Response(null, { status: 204 });
      }
      return Response.json({ context: "opaque-context" });
    }),
  );
  const first = initializeBrowserTelemetry({ relayPath: "/telemetry" });
  await first.ready;
  expect(() => initializeBrowserTelemetry({ relayPath: "/telemetry" })).toThrow(
    "already initialized",
  );
  const oldOperation = first.fetch("/old");
  await first.dispose();
  const second = initializeBrowserTelemetry({ relayPath: "/telemetry" });
  try {
    await second.ready;
    await first.dispose();
    delayed.resolve(new Response(null, { status: 500 }));
    await oldOperation;
    second.logger.info("second.session");
    await second.flush();
    expect(batches.join("")).toContain("second.session");
    expect(batches.join("")).not.toContain("browser.request.failed");
  } finally {
    await second.dispose();
  }
});
