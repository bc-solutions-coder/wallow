import assert from "node:assert/strict";
import { createHash, randomBytes, randomUUID } from "node:crypto";
import { spawn } from "node:child_process";
import { once } from "node:events";
import { initializeTelemetry } from "@bc-solutions-coder/telemetry/server";
import { sanitizeAttributes } from "@bc-solutions-coder/telemetry";

const SECRET_BYTES = 32;
const TRACE_BYTES = 16;
const SPAN_BYTES = 8;
const OK = 200;
const NO_FAILURES = 0;
const MIN_EXPORTS = 4;
const CRASH_EXIT = 1;
const EXPECTED_ERRORS = 2;
const QUERY_BUDGET_MS = 90_000;
const POLL_MS = 1000;

const registration = randomUUID();
const credentialId = randomUUID();
const secret = randomBytes(SECRET_BYTES).toString("base64url");
const options = {
  endpoint: "http://gateway:8080",
  credential: `${credentialId}.${secret}`,
  environment: "test",
  release: "external-1",
};
const provision = await fetch(`http://gateway:8081/control/v1/registrations/${registration}`, {
  method: "PUT",
  headers: {
    "content-type": "application/json",
    authorization: `Bearer ${process.env.GATEWAY_MANAGEMENT_SECRET}`,
  },
  body: JSON.stringify({
    revision: 1,
    clientId: registration,
    applicationId: registration,
    browserService: "external-browser",
    serverService: "external-node",
    environments: ["test"],
    state: "Enabled",
    credentials: [
      { id: credentialId, verifier: createHash("sha256").update(secret).digest("hex") },
    ],
    rotation: null,
  }),
});
assert.equal(provision.status, OK);
assert.deepEqual(sanitizeAttributes({ password: "secret" }), { password: "[redacted]" });
const telemetry = initializeTelemetry(options);
const traceId = randomBytes(TRACE_BYTES).toString("hex");
const parentId = randomBytes(SPAN_BYTES).toString("hex");
const handler = telemetry.instrument(
  () => {
    telemetry.logger.info("external.started", {
      count: 7,
      nested: [{ email: "PRIVACY-MARKER" }],
      url: "https://example.com/ok?secret=QUERY-MARKER",
    });
    throw new Error("EXCEPTION-MARKER");
  },
  { route: "/external" },
);
await assert.rejects(
  handler(
    new Request("http://application/external", {
      headers: { traceparent: `00-${traceId}-${parentId}-01` },
    }),
  ),
  /EXCEPTION-MARKER/u,
);
await telemetry.shutdown();
assert.equal(telemetry.stats().failed, NO_FAILURES);
assert.ok(telemetry.stats().exported >= MIN_EXPORTS);

const crash = spawn(
  process.execPath,
  [
    "--input-type=module",
    "-e",
    `
  import { initializeTelemetry } from "@bc-solutions-coder/telemetry/server";
  let input = "";
  for await (const chunk of process.stdin) input += chunk;
  const telemetry = initializeTelemetry(JSON.parse(input));
  void telemetry.trace("fatal", () => { throw new Error("CRASH-MARKER"); });
`,
  ],
  { stdio: ["pipe", "ignore", "ignore"] },
);
crash.stdin.end(JSON.stringify(options));
const [exitCode] = await once(crash, "exit");
assert.equal(exitCode, CRASH_EXIT);

/** @param {string} url */
async function json(url) {
  const response = await fetch(url);
  assert.equal(response.status, OK);
  return response.json();
}
const deadline = Date.now() + QUERY_BUDGET_MS;
async function verifyBackends() {
  try {
    const logs = await json(
      `http://loki:3100/loki/api/v1/query_range?query=${encodeURIComponent(`{service_name="external-node",service_namespace="${registration}"}`)}&limit=100`,
    );
    const serialized = JSON.stringify(logs);
    assert.ok(serialized.includes("external.started"));
    assert.ok(serialized.includes(traceId));
    assert.equal((serialized.match(/exception\.unexpected/gu) ?? []).length, EXPECTED_ERRORS);
    for (const marker of [
      "PRIVACY-MARKER",
      "QUERY-MARKER",
      "EXCEPTION-MARKER",
      "CRASH-MARKER",
      secret,
    ]) {
      assert.ok(!serialized.includes(marker));
    }
    const trace = JSON.stringify(await json(`http://tempo:3200/api/traces/${traceId}`));
    assert.ok(trace.includes("external-node"));
    assert.ok(trace.includes(registration));
    const metric = JSON.stringify(
      await json(
        `http://prometheus:9090/api/v1/query?query=${encodeURIComponent(`wallow_request_duration_milliseconds{service_name="external-node",service_namespace="${registration}"}`)}`,
      ),
    );
    assert.ok(metric.includes(registration));
    return;
  } catch (error) {
    if (Date.now() >= deadline) {
      throw error;
    }
    await new Promise((resolve) => {
      setTimeout(resolve, POLL_MS);
    });
    await verifyBackends();
  }
}
await verifyBackends();
process.stdout.write(
  `${JSON.stringify({
    node: process.version,
    registration,
    traceId,
    packed: true,
    correlated: true,
    sanitized: true,
    crashExitCode: exitCode,
  })}\n`,
);
