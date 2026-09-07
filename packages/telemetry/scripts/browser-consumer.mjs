import assert from "node:assert/strict";
import { setTimeout as delay } from "node:timers/promises";
import { chromium } from "playwright";

const FORBIDDEN = 403;
const OK = 200;
const GRAFANA_SETTLE_MS = 3000;
const QUERY_BUDGET_MS = 90_000;
const POLL_MS = 1000;
const SETTLE_MS = 250;
const IDLE_MS = 5500;
const origin = process.env.PUBLIC_ORIGIN;
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  const errors = [];
  const requests = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("request", (request) =>
    requests.push({ url: request.url(), headers: request.headers() }),
  );
  await page.goto(origin);
  await page.waitForFunction(() => globalThis.consumerReady);
  await page.evaluate(async () => {
    const FAILURE = 500;
    const MEASUREMENT = 42;
    globalThis.telemetry.logger.info("browser.anonymous", {
      email: "PRIVATE-MARKER@example.com",
      nested: { token: "PRIVATE-TOKEN-MARKER" },
      url: "https://example.com/path?secret=PRIVATE-QUERY-MARKER",
    });
    globalThis.telemetry.measure("browser.performance", MEASUREMENT);
    await globalThis.telemetry.flush();
    await fetch("/session", { method: "POST", body: "login" });
    await globalThis.telemetry.resetContext();
    globalThis.telemetry.logger.info("browser.signed_in");
    const handshake = await fetch("/telemetry");
    const context = await handshake.json();
    await fetch("/telemetry", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        context: context.context,
        logs: [
          {
            message: "browser.forged",
            context: { "user.id": "forged-user", nested: { token: "PRIVATE-FORGED-TOKEN" } },
          },
        ],
        meta: { user: { id: "forged-user" }, app: { name: "forged-service" } },
      }),
    });
    const failure = await globalThis.telemetry.fetch("/operate", {
      redirect: "error",
      headers: { baggage: "PRIVATE-BAGGAGE-MARKER" },
    });
    if (failure.status !== FAILURE) {
      throw new Error("Expected application failure");
    }
    await globalThis.telemetry.flush();
    await fetch("/session", { method: "POST", body: "switch" });
    await globalThis.telemetry.resetContext();
    globalThis.telemetry.logger.info("browser.switched");
    await globalThis.telemetry.flush();
    await fetch("/session", { method: "POST", body: "logout" });
    await globalThis.telemetry.resetContext();
    globalThis.telemetry.logger.info("browser.logged_out");
    const redirect = await globalThis.telemetry.fetch("/redirect");
    const redirectResult = await redirect.json();
    if (!redirectResult.ok) {
      throw new Error("Same-origin redirect failed");
    }
    const cross = await globalThis.telemetry.fetch("/external-redirect");
    const received = await cross.json();
    if (!received.ok || received.traceparent || received.baggage) {
      throw new Error("Cross-origin redirect leaked context");
    }
    await globalThis.telemetry.flush();
  });
  await page.click("#crash");
  await page.evaluate(() => globalThis.telemetry.flush());
  await delay(SETTLE_MS);
  await page.evaluate(() => globalThis.telemetry.flush());
  await delay(IDLE_MS);
  await page.evaluate(() => globalThis.telemetry.flush());
  const beforeIdle = requests.filter((request) => request.url === `${origin}/telemetry`).length;
  await delay(IDLE_MS);
  await page.evaluate(() => globalThis.telemetry.flush());
  assert.equal(
    requests.filter((request) => request.url === `${origin}/telemetry`).length,
    beforeIdle,
    "Idle telemetry must not recursively capture its own relay",
  );
  assert.deepEqual(errors, ["PRIVATE-ERROR-MARKER"]);
  assert.ok(requests.every((request) => !request.headers.authorization));
  assert.ok(
    requests.every((request) => !request.url.includes("gateway") && !request.url.includes("3100")),
  );
  const denied = await fetch(`${origin}/telemetry`, {
    method: "POST",
    headers: { origin: "https://attacker.example", "content-type": "application/json" },
    body: "{}",
  });
  assert.equal(denied.status, FORBIDDEN);
  const deadline = Date.now() + QUERY_BUDGET_MS;
  const readProof = async () => {
    const response = await fetch(`${origin}/proof`);
    const proof = await response.json();
    const wire = JSON.stringify(proof);
    if (
      wire.includes("browser.anonymous") &&
      wire.includes("browser.logged_out") &&
      wire.includes("Unexpected application error") &&
      wire.includes("browser.performance") &&
      wire.includes(`${proof.registration}-server`) &&
      JSON.stringify(proof.trace).includes("browser.request") &&
      JSON.stringify(proof.trace).includes(`${proof.apiApplication}-server`) &&
      JSON.stringify(proof.apiLogs).includes("exception.unexpected") &&
      JSON.stringify(proof.serverTrace).includes("background.failure")
    ) {
      return proof;
    }
    assert.ok(
      Date.now() < deadline,
      `Stored browser signals incomplete: ${JSON.stringify({ trace: proof.trace, apiLogs: proof.apiLogs, serverTrace: proof.serverTrace })}`,
    );
    await delay(POLL_MS);
    return readProof();
  };
  const proof = await readProof();
  const wire = JSON.stringify(proof);
  assert.ok(!/PRIVATE-|forged-user/u.test(wire));
  assert.ok(wire.includes("fixture-org-one") && wire.includes("fixture-org-two"));
  assert.ok(wire.includes("fixture-user"));
  assert.ok(
    wire.includes("faro.performance.navigation") || wire.includes("faro.performance.resource"),
  );
  const spans = JSON.stringify(proof.trace);
  assert.ok(
    spans.includes(`${proof.registration}-browser`) &&
      spans.includes(`${proof.registration}-server`),
  );
  assert.ok(spans.includes("fixture-user"));
  const handled = proof.apiLogs.data.result.filter((stream) =>
    stream.values.some(([, message]) => message === "application.failure.handled"),
  );
  assert.notDeepEqual(handled, []);
  for (const stream of handled) {
    assert.equal(stream.stream.severity_text, "WARNING");
    assert.equal(stream.stream.StatusCode, "400");
  }
  const background = proof.apiLogs.data.result.find(
    (stream) =>
      stream.stream.trace_id === proof.serverTraceId &&
      stream.values.some(([, message]) => message === "exception.unexpected"),
  );
  assert.equal(background?.stream.severity_text, "ERROR");
  assert.equal(proof.sourceContext.file, "operation.ts");
  assert.ok(JSON.stringify(proof.logs).includes("proof.js"));
  const grafanaOrigin = process.env.GRAFANA_ORIGIN;
  const stored = await fetch(
    `${grafanaOrigin}/api/datasources/proxy/uid/tempo/api/traces/${proof.traceId}`,
  );
  assert.equal(stored.status, OK);
  assert.ok(JSON.stringify(await stored.json()).includes(`${proof.apiApplication}-server`));
  const query = encodeURIComponent(
    `{service_namespace="${proof.apiApplication}"} | trace_id="${proof.traceId}"`,
  );
  const correlated = await fetch(
    `${grafanaOrigin}/api/datasources/proxy/uid/loki/loki/api/v1/query_range?query=${query}`,
  );
  assert.equal(correlated.status, OK);
  assert.ok(JSON.stringify(await correlated.json()).includes("exception.unexpected"));
  const grafana = await browser.newPage();
  const queryErrors = [];
  grafana.on("response", async (response) => {
    if (response.url().includes("/api/ds/query")) {
      if (!response.ok()) {
        queryErrors.push(response.status());
      } else {
        const data = await response.json();
        for (const result of Object.values(data.results ?? {})) {
          if (result.error) {
            queryErrors.push(result.error);
          }
        }
      }
    }
  });
  await grafana.goto(
    `${grafanaOrigin}/d/wallow-applications?var-application=${proof.apiApplication}&from=now-1h&to=now`,
  );
  await grafana.getByText("Server logs and trace links", { exact: true }).waitFor();
  await grafana.getByText("Server logs and trace links", { exact: true }).scrollIntoViewIfNeeded();
  await grafana.getByText("exception.unexpected", { exact: true }).first().waitFor();
  await delay(GRAFANA_SETTLE_MS);
  assert.deepEqual(queryErrors, []);
  await grafana.screenshot({ path: "/tmp/wallow-251-grafana.png", fullPage: true });
  const panes = {
    proof: {
      datasource: "tempo",
      queries: [
        {
          refId: "A",
          datasource: { uid: "tempo", type: "tempo" },
          queryType: "traceql",
          query: proof.traceId,
        },
      ],
      range: { from: "now-1h", to: "now" },
    },
  };
  await grafana.goto(
    `${grafanaOrigin}/explore?schemaVersion=1&panes=${encodeURIComponent(JSON.stringify(panes))}&orgId=1`,
  );
  await delay(GRAFANA_SETTLE_MS);
  await grafana.screenshot({ path: "/tmp/wallow-251-trace-debug.png", fullPage: true });
  await grafana.getByText("browser.request", { exact: true }).first().waitFor();
  await grafana.screenshot({ path: "/tmp/wallow-251-trace.png", fullPage: true });
  process.stdout.write(
    `${JSON.stringify({ registration: proof.registration, traceId: proof.traceId, packed: true, browser: true, correlated: true, apiApplication: proof.apiApplication, privateSourceContext: true, grafana: true, dotnet: true, serverOnly: true, sanitized: true, redirects: true, syntheticSessionTransitions: true, registrationCheckbox: true, idleRelayExcluded: true, relay: proof.relay })}\n`,
  );
} finally {
  await browser.close();
}
