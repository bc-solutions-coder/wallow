import { createServer } from "node:http";
import { afterEach, expect, test } from "vitest";
import { createBrowserRelay } from "./server.js";

const cleanup: (() => Promise<void>)[] = [];
afterEach(async () => {
  await Promise.all(cleanup.splice(0).map((close) => close()));
});

test("stamps only validated session identity and refuses batches buffered before a context change", async () => {
  const received: unknown[] = [];
  const collector = createServer(async (request, response) => {
    const chunks: Buffer[] = [];
    for await (const chunk of request) {
      chunks.push(Buffer.from(chunk));
    }
    received.push(JSON.parse(Buffer.concat(chunks).toString()));
    response.writeHead(202).end();
  });
  await new Promise<void>((resolve) => {
    collector.listen(0, "127.0.0.1", resolve);
  });
  cleanup.push(
    () =>
      new Promise<void>((resolve, reject) => {
        collector.close((error) => {
          if (error) {
            reject(error);
          } else {
            resolve();
          }
        });
      }),
  );
  const address = collector.address();
  if (address === null || typeof address === "string") {
    throw new Error("Missing collector address");
  }
  let session: { userId: string; organizationId: string; sessionId: string } | undefined;
  const relay = createBrowserRelay({
    endpoint: `http://127.0.0.1:${address.port}`,
    credential: "test.secret",
    origin: "https://app.example",
    resolveSession: async () => session,
  });
  const handshake = await relay.handle(new Request("https://app.example/telemetry"));
  const cookie = handshake.headers.get("set-cookie")?.split(";")[0] ?? "";
  const context: { context: string } = await handshake.json();
  const payload = {
    context: context.context,
    logs: [
      {
        message: "checkout.failed",
        level: "error",
        timestamp: new Date().toISOString(),
        context: {
          email: "private@example.com",
          nested: { token: "sensitive-token" },
          url: "https://shop.example/cart?secret=marker",
          userId: "forged-user",
        },
      },
    ],
    traces: {
      resourceSpans: [
        {
          scopeSpans: [
            {
              spans: [
                {
                  traceId: "a".repeat(32),
                  spanId: "b".repeat(16),
                  name: "browser.request",
                  startTimeUnixNano: "1000000",
                  endTimeUnixNano: "2000000",
                  attributes: [
                    { key: "user.id", value: { stringValue: "forged-user" } },
                    { key: "session.id", value: { stringValue: "forged-session" } },
                    { key: "organization.id", value: { stringValue: "forged-org" } },
                    ...Array.from({ length: 32 }, (_, index) => ({
                      key: `field${index}`,
                      value: { stringValue: "value" },
                    })),
                  ],
                },
              ],
            },
          ],
        },
      ],
    },
    meta: { user: { id: "forged-user" }, session: { id: "forged-session" } },
  };
  const send = (origin = "https://app.example") =>
    relay.handle(
      new Request("https://app.example/telemetry", {
        method: "POST",
        headers: { origin, cookie, "content-type": "application/json" },
        body: JSON.stringify(payload),
      }),
    );
  const response1 = await send("https://attacker.example");
  expect(response1.status).toBe(403);
  const response2 = await send();
  expect(response2.status).toBe(204);
  expect(JSON.stringify(received)).not.toMatch(
    /private@example|sensitive-token|secret=marker|forged-user|forged-session/u,
  );
  session = { userId: "user-123", organizationId: "org-456", sessionId: "validated-session" };
  const response3 = await send();
  expect(response3.status).toBe(409);
  const signedIn = await relay.handle(
    new Request("https://app.example/telemetry", { headers: { cookie } }),
  );
  const nextContext: { context: string } = await signedIn.json();
  payload.context = nextContext.context;
  const response4 = await send();
  expect(response4.status).toBe(204);
  const signedWire = JSON.stringify(received.at(-1));
  expect(signedWire).toContain("user-123");
  expect(signedWire.match(/"key":"user.id"/gu)).toHaveLength(1);
  expect(signedWire.match(/"key":"session.id"/gu)).toHaveLength(1);
  expect(signedWire.match(/"key":"organization.id"/gu)).toHaveLength(1);
  expect(JSON.stringify(received.at(-1))).toContain("org-456");
  session = { ...session, organizationId: "org-789" };
  const response5 = await send();
  expect(response5.status).toBe(409);
  const switched = await relay.handle(
    new Request("https://app.example/telemetry", { headers: { cookie } }),
  );
  const switchedContext: { context: string } = await switched.json();
  payload.context = switchedContext.context;
  const response6 = await send();
  expect(response6.status).toBe(204);
  expect(JSON.stringify(received.at(-1))).toContain("org-789");
  expect(JSON.stringify(received.at(-1))).not.toContain("org-456");
  session = undefined;
  const response7 = await send();
  expect(response7.status).toBe(409);
  expect(received).toHaveLength(3);
});

test("bounds relay requests and hides collector outages from application responses", async () => {
  const relay = createBrowserRelay({
    endpoint: "http://127.0.0.1:1",
    credential: "test.secret",
    origin: "https://app.example",
    resolveSession: async () => undefined,
  });
  const handshake = await relay.handle(new Request("https://app.example/telemetry"));
  const cookie = handshake.headers.get("set-cookie")?.split(";")[0] ?? "";
  const context: { context: string } = await handshake.json();
  const request = (body: string, extra: Record<string, string> = {}) =>
    new Request("https://app.example/telemetry", {
      method: "POST",
      headers: {
        origin: "https://app.example",
        cookie,
        "content-type": "application/json",
        ...extra,
      },
      body,
    });
  const response8 = await relay.handle(request("x".repeat(50000)));
  expect(response8.status).toBe(413);
  const response9 = await relay.handle(request("{}", { "content-encoding": "gzip" }));
  expect(response9.status).toBe(415);
  const response10 = await relay.handle(request("invalid-json"));
  expect(response10.status).toBe(400);
  const payload = JSON.stringify({
    context: context.context,
    logs: [{ message: "application.success", timestamp: new Date().toISOString() }],
  });
  const response11 = await relay.handle(request(payload));
  expect(response11.status).toBe(204);
  expect(relay.stats().failed).toBe(1);
  expect(relay.stats().dropped).toBe(1);
  const overload = await Promise.all(
    Array.from({ length: 30 }, () => relay.handle(request(payload))),
  );
  expect(overload.some((response) => response.status === 429)).toBe(true);
});
