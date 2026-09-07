# @bc-solutions-coder/logger

Private workspace structured logging for browser apps and their servers. Add
`"@bc-solutions-coder/logger": "workspace:*"` to a consuming workspace package.
This package is not installed from a registry. Browser code imports the root entry;
server ingestion and OTLP helpers use `./server`.

Create a reusable browser logger pointed at an app-owned same-origin route:

```ts
import { createLogger } from "@bc-solutions-coder/logger";

const logger = createLogger({ service: "example-web", endpoint: "/logs" });
logger.info("form.submitted", { form: "contact" });
logger.child({ feature: "settings" }).warn("save.failed", { attempt: 1 });
await logger.flush();
logger.dispose();
```

The default minimum level is `info`. A buffer holds at most 200 events, flushes at 20,
and uses a five-second timer in the browser. Overflow drops the oldest events and
reports a `logger.dropped` count. Children share the buffer, timer, and lifecycle
listeners; `dispose()` stops those listeners without flushing or preventing later calls.

Fetch delivery uses same-origin credentials. Failures are logged to the console,
discard the failed batch, and pause transport for 30 seconds. `pagehide` attempts a
beacon send, which confirms queueing rather than delivery. For a CSRF-protected route,
supply `getCsrfToken`; fetch uses the header and beacon uses the batch body.

Mount one ingest handler and retain it across requests so its rate limiter persists:

```ts
import { createLogIngestHandler } from "@bc-solutions-coder/logger/server";

export const handleLogs = createLogIngestHandler({
  service: "example-web",
  allowedOrigins: ["https://example.com"],
  sink: (records) => {
    for (const record of records) console.log(JSON.stringify(record));
  },
});
```

Pass a trusted runtime address resolver as `clientAddress` to separate rate-limit
buckets. Without it, all callers share the `unknown` bucket. The default limit is
60 accepted requests per minute per client per handler instance. The origin allowlist
is required; an empty list rejects all requests. Supply `authorize(request, batch)`
when the app requires session or CSRF validation, and `context(request)` for trusted
user and tenant IDs. The handler does not infer authentication from its route name.

Ingestion rejects invalid methods, origins, rates, and payloads with 400, 403, 405, 413,
or 429 and a JSON reason. Valid batches return 204 even if the sink fails. By default,
records go to the console, or to OTLP when `otlpEndpoint` is configured. The server
stamps service and receipt time; an event's correlation ID takes precedence over the
request header fallback.

For direct server events:

```ts
import { createServerLogger } from "@bc-solutions-coder/logger/server";

const logger = createServerLogger({ service: "example-server" });
logger.info("worker.started", { queue: "notifications" });
```

Direct server logging is immediate and does not expose a flush operation. Console
output defaults to enabled, including when an OTLP endpoint or custom sink is supplied.
The server entry also exports `createRateLimiter`, `parseLogBatch`, `redactAttrs`,
`otlpLogsUrl`, `toOtlpLogsPayload`, and `emitOtlp` for custom integrations.

Attribute redaction matches key substrings ignoring case and descends through object
values only, to a bounded depth. Arrays, deeper values, and error messages or stacks
are not scrubbed. Keep attributes serializable and avoid secrets in those locations.
Custom callbacks can throw; asynchronous sink and fetch failures are handled, but this
is not a blanket guarantee that every logger call is nonthrowing.
