# Wallow telemetry

Optional structured logs and request telemetry for Node 24 applications connected to Wallow.
Install the package and initialize its server entry after loading server configuration:

```ts
import { initializeTelemetry } from "@bc-solutions-coder/telemetry/server";

const telemetry = initializeTelemetry({
  ownedOrigins: ["https://api.example.com"],
});

export const handle = telemetry.instrument(
  async (request) => {
    telemetry.logger.info("work.started", { attempt: 1 });
    const response = await telemetry.fetch("https://api.example.com/work");
    return response;
  },
  { route: "/work" },
);

// During the host's graceful shutdown:
await telemetry.shutdown();
```

Supply `WALLOW_TELEMETRY_ENDPOINT`, `WALLOW_TELEMETRY_CREDENTIAL`,
`WALLOW_TELEMETRY_ENVIRONMENT` and `WALLOW_TELEMETRY_RELEASE` on the server. Environment
and release default to `production` and `unknown`. Explicit initialization options override
them. Credentials never belong in browser configuration. The gateway authenticates and
stamps the registered application and service identities; the producer cannot choose them.

`instrument` wraps a WHATWG Request handler. It captures thrown failures, marks returned
5xx responses as failed spans, and records duration. Pass a fixed route template, never an
actual URL containing user input. `trace(name, operation)` covers background work.
`telemetry.fetch` forwards trace context only to configured exact origins, checking every
redirect. Streaming Request bodies are sent once; redirects requiring their replay fail,
matching the non-replayable stream boundary. Explicit string, URLSearchParams and Blob
request-init bodies can be replayed. It removes baggage and does not patch the host's global fetch implementation.
Wrap each application handler and use this fetch method on calls that need correlation.

The logger supports `debug`, `info`, `warn`, `error` and `child`. Use stable event names and
structured values. `captureException(error, true)` records an expected handled failure as a
warning; the default records an unexpected error. Repeated capture of one Error object is
suppressed. The Node uncaught-exception monitor makes a bounded final export without
changing Node's crash exit behavior. Confirmed delivery suppresses a duplicate fatal record;
a lost acknowledgement can still produce a duplicate, as with other network delivery.

Exports share an 8 MiB buffer, a 1 MiB request ceiling and a five-second flush interval.
Requests and graceful shutdown have five-second budgets. Overflow drops new batches;
failed exports are counted and discarded. `stats()` reports queued bytes, exported batches,
failed batches and overflow/shutdown drops. Telemetry sink failures do not reject application
operations. Abrupt process termination can lose buffered telemetry. Crash export has its
own bounded five-second child process because Node will not wait for asynchronous work
from its exception monitor.

The package bundles the maintained private Wallow logger. Its public declarations and
runtime dependencies require no private workspace installation. The root entry bundles Faro
and shared sanitizers without Node imports.

Sanitization runs recursively with limits on depth, visited values, attributes and ordinary
string length. It removes sensitive keys, email and IP values, URL credentials/query strings,
bearer values and JWTs. Exception messages are omitted; only bounded file/line locations
and standard exception types are retained. This is not permission to log personal content:
use event names and technical attributes, and exclude application content at the call site.
There is no blanket console capture, source-map publication or import-time initialization.

From the repository, `pnpm check:telemetry-consumer` builds and packs this package, installs
it outside the workspace, typechecks with NodeNext resolution, then runs a Node 24 container
against the independent observability proof network. It provisions a disposable registration
through the private control API and queries Loki, Tempo and Prometheus, including a fatal
child process. It needs the local stack and generated management credential described in
`docker/observability/README.md`; this proof command is not a product provisioning workflow.

## Browser and same-origin server relay

Mount the server relay at a same-origin route. Pass the validated server session returned
by your application's authentication layer. Do not construct it from browser payloads,
unsigned cookies or caller identity headers.

```ts
import { createBrowserRelay } from "@bc-solutions-coder/telemetry/server";

const relay = createBrowserRelay({
  origin: "https://app.example.com",
  resolveSession: async (request) => {
    const session = await readValidatedSession(request);
    return (
      session && {
        userId: session.userId,
        organizationId: session.organizationId,
        sessionId: session.id,
      }
    );
  },
});
// Route both GET and POST /telemetry to relay.handle(request).
```

The relay reads the same server environment configuration as the Node exporter. Only a
same-origin path goes into the browser bundle:

```ts
import { initializeBrowserTelemetry } from "@bc-solutions-coder/telemetry";

const telemetry = initializeBrowserTelemetry({ relayPath: "/telemetry" });
await telemetry.ready;
telemetry.logger.info("checkout.started", { itemCount: 2 });
const response = await telemetry.fetch("/checkout", { method: "POST", redirect: "error" });
// After the server completes login, logout, or organization selection:
await telemetry.resetContext();
```

Browser fetch preserves the caller's redirect policy. Trace context is added only for an
owned exact origin when the caller selects `redirect: "error"`. Default-follow requests
still work but do not carry trace headers, because browsers cannot inspect each redirect
before forwarding headers. Baggage and caller trace headers are removed. Add explicit
`ownedOrigins` only for destinations you control. The Node wrapper can inspect redirects
and supports correlation through configured Wallow API destinations.

Faro captures navigation/resource timing, web vitals and unexpected browser errors.
`measure(name, value)` adds a performance measurement. There is no console capture.
The relay rejects foreign origins and compressed or oversized bodies, re-sanitizes all
signals and replaces browser identity with validated server context. Anonymous capture
uses a random, signed, one-hour HttpOnly cookie without fingerprinting. Context handshakes
bind buffered data to the current anonymous/user/organization/session combination. A stale
batch is discarded; callers must reset context after changing the server session.

Browser queues hold at most 256 KiB including in-flight data, with 48 KiB batches, 8 KiB
events, 32 attributes and 1 KiB ordinary values. Flushes have a five-second budget.
The relay has 16 concurrent slots and a shared ten-request-per-second bucket with burst 20.
`stats()` reports loss. Collector failures are discarded and do not fail application actions.
Abrupt tab closure can lose buffered telemetry; delivery is not lossless.

One instance may be active in a document. `dispose()` pauses capture, removes application
error/page listeners, flushes and clears its queue. Faro's performance observers have
browser-document lifetime and are initialized once. Reinitializing after disposal reuses
them and requires the same relay path. Repeated disposal is safe. Relay requests are
excluded from resource capture, so idle export does not generate more telemetry.

`./scripts/check-browser-telemetry-consumer.sh` uses the real Wallow registration HTTP
workflow with synthetic test authentication, waits for collector acknowledgement, and
hands its one-time configuration to an isolated packed Node/browser consumer. Playwright
checks session transitions, errors, redirect behavior and idle export, then queries actual
Loki and Tempo data. This local fixture does not prove a deployed OIDC or Pangolin session.
