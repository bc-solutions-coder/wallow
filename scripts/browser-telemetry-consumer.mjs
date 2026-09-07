import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { createBrowserRelay, initializeTelemetry } from "@bc-solutions-coder/telemetry/server";

// Disposable external application. Session transitions here are synthetic server-owned fixtures.
const configured = JSON.parse(
  await readFile(new URL("configuration.json", import.meta.url), "utf8"),
);
const registration = configured.application;
const options = {
  endpoint: configured.endpoint,
  credential: configured.credential,
  environment: configured.environment,
  release: configured.release,
};
const secret = configured.credential;
let session;
const relay = createBrowserRelay({
  ...options,
  origin: process.env.PUBLIC_ORIGIN,
  resolveSession: async () => session,
});
const telemetry = initializeTelemetry(options);
let traceId;
const operation = telemetry.instrument(
  (request) => {
    traceId = request.headers.get("traceparent")?.split("-")[1];
    telemetry.logger.info("external.operation", { email: "PRIVATE-MARKER@example.com" });
    return new Response("expected fixture failure", { status: 500 });
  },
  { route: "/operate" },
);
const root = new URL("node_modules/@bc-solutions-coder/telemetry/dist/", import.meta.url);
const html = `<!doctype html><title>External telemetry consumer</title><button id="crash">Unexpected error</button>
<script type="module">
import { initializeBrowserTelemetry } from '/dist/index.js';
window.telemetry = initializeBrowserTelemetry({ relayPath: '/telemetry' });
await window.telemetry.ready;
document.querySelector('#crash').onclick = () => { throw new Error('PRIVATE-ERROR-MARKER'); };
window.consumerReady = true;
</script>`;
async function query(url) {
  const result = await fetch(url);
  if (!result.ok) {return { status: result.status };}
  return result.json();
}
const server = createServer(async (incoming, outgoing) => {
  try {
    const url = new URL(incoming.url, process.env.PUBLIC_ORIGIN);
    const chunks = [];
    for await (const chunk of incoming) {chunks.push(chunk);}
    const body = Buffer.concat(chunks);
    const request = new Request(url, {
      method: incoming.method,
      headers: incoming.headers,
      ...(body.length > 0 ? { body } : {}),
    });
    let response;
    if (url.pathname === "/telemetry") {response = await relay.handle(request);}
    else if (url.pathname === "/operate") {response = await operation(request);}
    else if (url.pathname === "/session" && request.method === "POST") {
      const action = await request.text();
      session =
        action === "logout"
          ? undefined
          : {
              userId: "fixture-user",
              organizationId: action === "switch" ? "fixture-org-two" : "fixture-org-one",
              sessionId: "fixture-session",
            };
      response = new Response(null, { status: 204 });
    } else if (url.pathname === "/redirect") {response = Response.redirect(`${url.origin}/ok`);}
    else if (url.pathname === "/external-redirect")
      {response = Response.redirect(`${process.env.OTHER_ORIGIN}/ok`);}
    else if (url.pathname === "/ok") {response = Response.json({ ok: true });}
    else if (url.pathname === "/proof") {
      await telemetry.flush();
      const selector = `{app_namespace="${registration}"}`;
      const logs = await query(
        `http://loki:3100/loki/api/v1/query_range?query=${encodeURIComponent(selector)}&limit=1000`,
      );
      const serverLogs = await query(
        `http://loki:3100/loki/api/v1/query_range?query=${encodeURIComponent(`{service_namespace="${registration}"}`)}&limit=1000`,
      );
      const trace = traceId ? await query(`http://tempo:3200/api/traces/${traceId}`) : {};
      const serialized = JSON.stringify({ logs, serverLogs, trace });
      assert.ok(!serialized.includes(secret));
      response = Response.json({
        registration,
        traceId,
        logs,
        serverLogs,
        trace,
        relay: relay.stats(),
      });
    } else if (/^\/dist\/(?:index|faro-privacy-[A-Za-z0-9_-]+)\.js$/u.test(url.pathname)) {
      const artifact = await readFile(new URL(url.pathname.slice("/dist/".length), root), "utf8");
      assert.ok(!artifact.includes(secret));
      response = new Response(artifact, { headers: { "content-type": "text/javascript" } });
    } else if (url.pathname === "/")
      {response = new Response(html, { headers: { "content-type": "text/html" } });}
    else {response = new Response(null, { status: 404 });}
    outgoing.writeHead(response.status, Object.fromEntries(response.headers));
    outgoing.end(Buffer.from(await response.arrayBuffer()));
  } catch (error) {
    process.stderr.write(`${error.name}: external consumer request failed\n`);
    outgoing.writeHead(500).end();
  }
});
createServer((request, response) => {
  response.writeHead(200, {
    "access-control-allow-origin": process.env.PUBLIC_ORIGIN,
    "content-type": "application/json",
  });
  response.end(
    JSON.stringify({
      ok: true,
      traceparent: request.headers.traceparent,
      baggage: request.headers.baggage,
    }),
  );
}).listen(8081, "0.0.0.0");
server.listen(8080, "0.0.0.0", () => process.stdout.write("consumer ready\n"));
