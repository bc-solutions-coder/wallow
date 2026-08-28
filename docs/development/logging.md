# Frontend Logging

Wallow's React apps log through **`@bc-solutions-coder/logger`** (`packages/logger`), never
through `console`. The package is both ends of one path: a browser core that buffers, filters and
posts batches to a same-origin route, and the app-server ingest handler that guards, stamps and
forwards them to an OpenTelemetry collector.

The transport is **browser → app server → OTLP**. The page never talks to a collector and never
holds a collector credential — what it holds is a same-origin path. That is the same shape as the
[BFF pattern](../integrations/bff-pattern.md) for API calls, and for the same reason: a secret the
browser does not have is a secret the browser cannot leak.

```
page  ──POST /bff/logs (wallow-web)──▶  app server  ──OTLP/HTTP──▶  collector
      ──POST /logs     (wallow-auth)──▶      │
                                             └── no OTEL_EXPORTER_OTLP_ENDPOINT? ──▶ stdout JSON
```

## Recording an event from a page

Each app owns one logger, a module singleton at `src/shared/lib/log.ts`:

```ts
import { log } from "@shared/lib/log";

log.info("form.submitted", { form: "signup" });
log.error("bff.logout.failed", {}, error);
```

The four methods (`debug`, `info`, `warn`, `error`) share one signature —
`(event, attrs?, error?)`. `child(attrs)` returns a logger that stamps extra attributes on every
record while sharing the same buffer, so a child's records interleave with its parent's in one
batch rather than racing two buffers to the same route.

Nothing here throws into the page and nothing returns a value you must handle. A page's behaviour
never changes because telemetry is down.

### Events are named, not prose

`event` is a **name**: dotted, lowercase, low-cardinality, groupable.

| Good                | Bad                                    |
| ------------------- | -------------------------------------- |
| `form.submitted`    | `"User submitted the signup form"`     |
| `bff.logout.failed` | `"logout failed for user 91f2…"`       |
| `logger.dropped`    | `"dropped 14 events"`                  |

The wire format enforces it with `/^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$/u`, and a rejected name
fails the whole batch with a 400. Anything that varies per occurrence goes in `attrs`, where it
costs one attribute rather than one metric series.

### Attributes are redacted before they are buffered

Attribute keys are matched case-insensitively as a **substring** against
`DEFAULT_REDACT_KEYS` — `password`, `token`, `secret`, `authorization`, `cookie`, `email` — and
replaced with `[redacted]`. So `newPassword` and `password_confirmation` are both covered. The
pass walks nested values, and a consumer can supply its own list via `redact`.

This is a floor, not a classifier. Do not put anything you would not want in a collector into
`attrs` and rely on the list to catch it.

## What reaches the collector

The browser sends `{ ts, level, event, attrs, correlationId?, error? }`. The server discards
nothing but adds everything a page could otherwise assert about itself:

| Field                     | Where it comes from                                                   |
| ------------------------- | --------------------------------------------------------------------- |
| `ts`                      | Server receipt time                                                   |
| `clientTs`                | What the browser claimed, kept for clock-skew analysis                |
| `service`                 | The handler's own configuration — **never** the payload               |
| `clientIp`                | The host's `clientAddress` callback — **never** an inbound header     |
| `correlationId`           | The event's, or the request's `x-request-id`                          |
| `userId`, `tenantId`      | The app's `context` callback, read from the session                   |

`service`, `userId` and `tenantId` are stamped server-side on purpose: a record that names its own
service or its own user is a record any page can forge. The validator rebuilds each accepted event
field by field rather than spreading the wire object, so a payload carrying an extra `service` key
cannot smuggle it into a record.

`clientIp` is the same argument one step further out. It comes from the `clientAddress` callback
the app supplies — both apps answer with `request.ip`, the peer address off the connection — and
the handler reads no inbound header for it. A header would arrive with the payload's validation
bypassed and would let a caller both forge the field and, since the same value keys the rate
limit, mint a fresh bucket per request.

Correlation uses the same `x-request-id` header the SDK's proxy writes, so a browser record and
the API records for the same request join on it. See
[Request Correlation](../operations/request-correlation.md).

## Server-side logging

App-server code (route handlers, server-only modules) uses `createServerLogger` from the
`./server` entry rather than the browser core. `wallow-web` holds one at
`src/app/lib/log.server.ts`:

```ts
import { serverLog } from "./log.server";

serverLog.info("bff.session_store.selected", { store: "redis", stateless: false });
serverLog.error("bff.redis.error", {}, error);
```

Same record shape, same event-naming rule, same redaction — it just writes straight to the sink
instead of buffering.

## Configuration

One environment variable, and it is the standard OpenTelemetry one:

| Variable                      | Effect                                                          |
| ----------------------------- | --------------------------------------------------------------- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Where records are POSTed as OTLP/HTTP JSON — the collector's **HTTP** port, 4318. |
| _(unset)_                     | Records are written to stdout as JSON lines — what `docker logs` shows. |

The port matters and the variable name hides it. The .NET API exports over OTLP/**gRPC** and reads
the same variable pointed at **4317**; this package has no gRPC transport at all. Given 4317 the
POST simply fails, and because a valid batch answers `204` regardless of collector health, nothing
in the app surfaces the loss.

There is no Wallow-specific logging variable to set, and no allowlist to configure: the ingest
route derives the origin it accepts from the request it was handed (see below).

## The ingest route

Both apps mount the **same** handler; neither reimplements a guard.

| App           | Path        | CSRF                                                      |
| ------------- | ----------- | --------------------------------------------------------- |
| `wallow-web`  | `/bff/logs` | Yes — it holds a session, so it verifies the session token |
| `wallow-auth` | `/logs`     | No — it holds no session, so there is no token to check    |

**CSRF is not the control this endpoint needs**, and treating it as one is how an ingest route
ends up unprotected in the app that has no session. The controls that actually apply are the same
in both apps:

- **An origin allowlist**, and it is load-bearing rather than advisory. `Origin` is a forbidden
  header name, so page script cannot forge it, and it is the one guard that survives the
  `sendBeacon` path where no header can be set. Both apps resolve it per request to the origin
  _this request was addressed to_ — the classic Origin-versus-target check, which needs no
  logging-specific configuration and honours a trusted reverse proxy's `x-forwarded-proto`
  (gated by `WALLOW_TRUSTED_PROXIES`, the same list that gates `x-forwarded-for`).
- **Payload caps that reject rather than truncate.** The default is 64 KiB, which is
  `sendBeacon`'s own quota: a batch the browser would refuse to queue is a batch the server
  refuses to read.
- **A per-IP rate limit**, because the route is unauthenticated by design. Its key is the address
  the host supplies, never one off the wire — a caller who can choose the key has no limit.

A valid batch answers `204` regardless of collector health. The guards still return real
`405`/`403`/`413`/`429`/`400`; only a sink failure is swallowed.

## When logs are sent

| Trigger                   | Transport                                             |
| ------------------------- | ----------------------------------------------------- |
| Buffer reaches 20 events  | `fetch(..., { keepalive: true })`                     |
| Every 5 s                 | `fetch(..., { keepalive: true })`                     |
| `visibilitychange` hidden | `fetch(..., { keepalive: true })`                     |
| `pagehide`                | `navigator.sendBeacon` — the terminal flush           |
| `log.flush()`             | `fetch(..., { keepalive: true })`                     |

`visibilitychange` → hidden carries most of the load: it fires on tab switch, app switch, and the
mobile discard path that never fires `pagehide` at all. `pagehide` is the last call and takes the
beacon, which is the only transport a browser promises to finish after the document is gone.
`sendBeacon` cannot set headers, so on that path a CSRF token rides in the body instead; the
handler accepts it from either place.

## Three failure rules

A logger that fails loudly is worse than no logger.

1. **A transport error never calls the logger.** It falls back to `console.warn` and disables
   transport for a backoff window (30 s by default). The failed events are deliberately **not**
   requeued — a requeue turns one unreachable route into a buffer that refills itself every window
   and never drains, and the records are not lost to the person who can act on them: they are on
   the console, which is where a broken telemetry path belongs.
2. **The buffer drops oldest on overflow.** The ceiling is 200 events; past it the oldest are
   discarded and the next batch is prepended with a `logger.dropped` warn carrying `{ count }`.
   A long-lived tab with a dead ingest route never grows without bound.
3. **Nothing throws into the page.** Every method returns `void` or `Promise<void>`.

## Where `console` is still correct

`console` is not banned outright — it is the right call in two places, both outside the app
runtime:

- **Inside the logger itself**, for the transport-failure fallback. Rule 1 above.
- **In Playwright setup and other build/test tooling**, which has no app server to post to and
  whose output belongs on the terminal.

Everything rendered or served by an app goes through the logger.

## How this fits the platform's observability

The [Observability Guide](../operations/observability.md) documents the .NET API's three signals —
structured logs, metrics and traces, all exported over OTLP/gRPC to the same collector. This page is
the other half of that picture, and the two halves are **not** symmetric:

| | Backend (`Wallow.Api`) | Frontend (React apps) |
| --- | --- | --- |
| Logs | Serilog → OTLP | `@bc-solutions-coder/logger` → app server → OTLP |
| Metrics | OpenTelemetry instruments | None — the browser emits no metrics |
| Traces | OpenTelemetry activities, sampled by `OpenTelemetry:TraceSamplingRatio` | None — the browser starts no spans |
| Transport | OTLP/**gRPC**, port 4317 | OTLP/**HTTP**, port 4318 |
| Credential | Held by the API process | Held by the app server; **never** by the page |

So "frontend observability" in Wallow means **logs only**, joined to the backend's three signals by
the shared `correlationId` (the `x-request-id` the SDK's proxy writes). Both ends land in the same
collector and the same Grafana stack, so a browser record and the API records for the same request
sit in one query — which is the whole reason the correlation ID is stamped rather than generated
independently at each end.

Two practical consequences:

- **The endpoint variable is shared but the port is not.** `OTEL_EXPORTER_OTLP_ENDPOINT` is read by
  both the API and the app servers; the API wants `:4317` and the app servers want `:4318`. In a
  deployment where the two run as separate processes this is fine. Where they share an environment,
  the app server's value has to be set per process — see the Configuration table above.
- **There is nothing to instrument in the page.** If you want a frontend signal that is not a log
  record, it does not exist yet; add the log event and derive the metric in the collector.
