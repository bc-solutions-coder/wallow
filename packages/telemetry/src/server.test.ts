import { createServer } from "node:http";
import { once } from "node:events";
import { expect, test } from "vitest";
import { initializeTelemetry } from "./server.js";

test("exports correlated failures and recursively removes sensitive values", async () => {
  const received: string[] = [];
  const collector = createServer(async (request, response) => {
    let body = "";
    for await (const chunk of request) {
      body += String(chunk);
    }
    received.push(body);
    response.writeHead(200).end("{}");
  });
  collector.listen(0, "127.0.0.1");
  await once(collector, "listening");
  const address = collector.address();
  if (address === null || typeof address === "string") {
    throw new Error("No TCP listener");
  }
  const telemetry = initializeTelemetry({
    endpoint: `http://127.0.0.1:${address.port}`,
    credential: "test.secret",
    environment: "test",
    release: "1.0.0",
  });
  try {
    const handler = telemetry.instrument(
      async () => {
        telemetry.logger.info("work.started", {
          count: 3,
          nested: [{ password: "PASSWORD-MARKER", email: "EMAIL-MARKER", count: 4 }],
          url: "https://example.com/work?token=QUERY-MARKER",
        });
        throw new Error("EXCEPTION-MARKER user@example.com");
      },
      { route: "/work" },
    );
    await expect(handler(new Request("http://localhost/work?token=URL-MARKER"))).rejects.toThrow(
      "EXCEPTION-MARKER",
    );
    await telemetry.shutdown();
    const exported = received.join("\n");
    expect(exported).toContain("work.started");
    expect(exported).toContain("exception.unexpected");
    expect(exported).toContain('"traceId"');
    for (const marker of [
      "PASSWORD-MARKER",
      "EMAIL-MARKER",
      "QUERY-MARKER",
      "URL-MARKER",
      "EXCEPTION-MARKER",
      "user@example.com",
    ]) {
      expect(exported).not.toContain(marker);
    }
    expect(telemetry.stats().exported).toBeGreaterThan(0);
  } finally {
    await telemetry.shutdown();
    collector.close();
  }
});

test("keeps application work successful while a collector rejects data and the buffer overflows", async () => {
  const collector = createServer((_request, response) => response.writeHead(503).end());
  collector.listen(0, "127.0.0.1");
  await once(collector, "listening");
  const address = collector.address();
  if (address === null || typeof address === "string") {
    throw new Error("No TCP listener");
  }
  const telemetry = initializeTelemetry({
    endpoint: `http://127.0.0.1:${address.port}`,
    credential: "test.secret",
  });
  try {
    const attrs = Object.fromEntries(
      Array.from({ length: 32 }, (_, i) => [`field${i}`, "x".repeat(1024)]),
    );
    for (let i = 0; i < 400; i += 1) {
      telemetry.logger.info("buffer.load", attrs);
    }
    expect(telemetry.stats().queuedBytes).toBeLessThanOrEqual(8 * 1024 * 1024);
    expect(telemetry.stats().dropped).toBeGreaterThan(0);
    const handle = telemetry.instrument(() => new Response("working"), { route: "/health" });
    const response = await handle(new Request("http://localhost/health"));
    expect(await response.text()).toBe("working");
    await telemetry.shutdown();
    expect(telemetry.stats().failed).toBeGreaterThan(0);
    expect(telemetry.stats().queuedBytes).toBe(0);
  } finally {
    await telemetry.shutdown();
    collector.close();
  }
});

test("distinguishes handled errors and captures a repeated unexpected Error only once", async () => {
  const received: string[] = [];
  const collector = createServer(async (request, response) => {
    let body = "";
    for await (const chunk of request) {
      body += String(chunk);
    }
    received.push(body);
    response.writeHead(200).end("{}");
  });
  collector.listen(0, "127.0.0.1");
  await once(collector, "listening");
  const address = collector.address();
  if (address === null || typeof address === "string") {
    throw new Error("No TCP listener");
  }
  const telemetry = initializeTelemetry({
    endpoint: `http://127.0.0.1:${address.port}`,
    credential: "test.secret",
  });
  try {
    telemetry.captureException(new Error("validation"), true);
    const failure = new Error("crash");
    telemetry.captureException(failure);
    telemetry.captureException(failure);
    await telemetry.shutdown();
    const output = received.join("\n");
    expect(output.match(/exception\.unexpected/gu)).toHaveLength(1);
    expect(output.match(/exception\.handled/gu)).toHaveLength(1);
  } finally {
    await telemetry.shutdown();
    collector.close();
  }
});

test("marks returned server errors as failed spans and removes IPv6 values", async () => {
  const received: unknown[] = [];
  const collector = createServer(async (request, response) => {
    let body = "";
    for await (const chunk of request) {
      body += String(chunk);
    }
    received.push(JSON.parse(body));
    response.writeHead(200).end("{}");
  });
  collector.listen(0, "127.0.0.1");
  await once(collector, "listening");
  const address = collector.address();
  if (address === null || typeof address === "string") {
    throw new Error("No TCP listener");
  }
  const telemetry = initializeTelemetry({
    endpoint: `http://127.0.0.1:${address.port}`,
    credential: "test.secret",
  });
  try {
    telemetry.logger.info("network.detail", {
      nested: [{ detail: "2001:db8::1234", url: "http://[2001:db8::1234]/work" }],
    });
    const handler = telemetry.instrument(() => new Response("failed", { status: 500 }), {
      route: "/failed",
    });
    const result = await handler(new Request("http://localhost/failed"));
    expect(result.status).toBe(500);
    await telemetry.shutdown();
    const failedSpan = expect.objectContaining({ status: { code: 2 } });
    const scope = expect.objectContaining({ spans: [failedSpan] });
    const resource = expect.objectContaining({ scopeSpans: [scope] });
    expect(received).toContainEqual(expect.objectContaining({ resourceSpans: [resource] }));
    expect(JSON.stringify(received)).not.toContain("2001:db8");
  } finally {
    await telemetry.shutdown();
    collector.close();
  }
});

test("follows redirects without propagating trace context to an unowned destination", async () => {
  const seen: (string | string[] | undefined)[] = [];
  const outside = createServer((request, response) => {
    seen.push(request.headers.traceparent);
    response.end("arrived");
  });
  outside.listen(0, "127.0.0.1");
  await once(outside, "listening");
  const outsideAddress = outside.address();
  if (outsideAddress === null || typeof outsideAddress === "string") {
    throw new Error("No TCP listener");
  }
  const owned = createServer((request, response) => {
    seen.push(request.headers.traceparent);
    response.writeHead(302, { location: `http://127.0.0.1:${outsideAddress.port}/target` }).end();
  });
  owned.listen(0, "127.0.0.1");
  await once(owned, "listening");
  const ownedAddress = owned.address();
  if (ownedAddress === null || typeof ownedAddress === "string") {
    throw new Error("No TCP listener");
  }
  const origin = `http://127.0.0.1:${ownedAddress.port}`;
  const telemetry = initializeTelemetry({
    endpoint: origin,
    credential: "test.secret",
    ownedOrigins: [origin],
  });
  try {
    const result = await telemetry.trace("redirect", () => telemetry.fetch(`${origin}/redirect`));
    expect(await result.text()).toBe("arrived");
    expect(seen[0]).toMatch(/^00-/u);
    expect(seen[1]).toBeUndefined();
  } finally {
    await telemetry.shutdown();
    owned.close();
    outside.close();
  }
});

test("sends streaming uploads once and replays only explicit reusable bodies on redirects", async () => {
  const received: string[] = [];
  const server = createServer(async (request, response) => {
    let body = "";
    for await (const chunk of request) {
      body += String(chunk);
    }
    received.push(body);
    if (request.url === "/redirect") {
      response.writeHead(307, { location: "/target" }).end();
    } else {
      response.end("received");
    }
  });
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  const address = server.address();
  if (address === null || typeof address === "string") {
    throw new Error("No TCP listener");
  }
  const origin = `http://127.0.0.1:${address.port}`;
  const telemetry = initializeTelemetry({ endpoint: origin, credential: "test.secret" });
  function streaming(path: string): Request {
    const body = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(new TextEncoder().encode("streamed"));
        controller.close();
      },
    });
    const init = { method: "POST", body, duplex: "half" };
    return new Request(`${origin}${path}`, init);
  }
  try {
    const direct = await telemetry.fetch(streaming("/target"));
    expect(await direct.text()).toBe("received");
    await expect(telemetry.fetch(streaming("/redirect"))).rejects.toThrow("cannot be replayed");
    const replayed = await telemetry.fetch(`${origin}/redirect`, {
      method: "POST",
      body: "reusable",
    });
    expect(await replayed.text()).toBe("received");
    expect(received).toEqual(["streamed", "streamed", "reusable", "reusable"]);
  } finally {
    await telemetry.shutdown();
    server.close();
  }
});
