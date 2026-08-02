# Logging and telemetry — decision record

**status: active**

Supersedes `2234-faro-telemetry-integration.md`. That file sketched adding Grafana Faro
alongside `@bc-solutions-coder/logger`. Validating the sketch reversed the recommendation and
turned up two defects in what already exists. **No code has been changed.** This file records
what is true, what is broken, and what to do about it.

---

## Decisions

### 1. Do not adopt Faro

The sketch argued for two pipelines joined by a correlation seam. Three things undercut it:

- **It breaks the security posture.** Faro-direct means a publicly reachable receiver, CORS,
  a collector API key in the public bundle, and a page-asserted `meta.user`. The current
  browser → app-server → OTLP transport has none of those. See "Security posture" below.
- **The redaction story has a hole.** Faro's `TraceEvent` carries no context or attributes bag,
  so traces bypass `beforeSend` entirely. Closing it needs `ignoreUrls`, an OTel span processor,
  or an Alloy-side transform — maintenance the sketch did not account for.
- **Its unique value is smaller than claimed.** The sketch's "automatic capture only comes with
  Faro" premise is wrong. Global error/rejection handlers, web-vitals (~3.3 KB gzipped) and a
  router subscription total roughly 7 KB on top of the logger core. Only **linked distributed
  traces** genuinely require OTel-web (~28 KB), and that is a separate, later decision.

What Faro would cost: ~50 KB of fast-moving third-party surface, plus a router wrapper that
sits on a real type trap (below).

### 2. Keep the hand-rolled `otlp.ts`

Server-side, bundle weight is free, so the official OpenTelemetry JS path was worth checking.
`@opentelemetry/sdk-logs` and `@opentelemetry/exporter-logs-otlp-http` are at **0.221.0** — the
JS logs SDK is still pre-1.0 and has not stabilised its API the way traces has. Trading ~240
lines of a wire format that essentially never changes for a 0.x dependency that can break
underneath is the wrong direction for a low-maintenance goal.

### 3. The inversion is possible, and the seams already exist

The proposed shape — a telemetry package that `logger` *consumes*, so one import handles
console, BFF and (optionally) Faro — works, with one correction: **`logger` must never
`import` telemetry.** Its `dependencies` and `peerDependencies` are both `{}`, pinned by
`packages/logger/src/charter.test.ts` and the root `.oxlintrc.json`. A real package edge breaks
both.

It doesn't need one. This is dependency inversion:

- `logger` **defines** the interfaces — it already does. `createLogIngestHandler` takes
  `sink?: LogSink` (`(records: ServerLogRecord[]) => void | Promise<void>`), and the browser
  core has a transport seam.
- A telemetry package **implements** them.
- The **app composes**, at `src/shared/lib/log.ts` — which already exists and already is the
  single import every screen uses.

"Import a logger, enable faro, everything is handled" therefore lands as one option on an
existing singleton, not as restructuring.

**Consequence:** given decision 1, the telemetry package does not need to exist yet. It
collapses into "logger grows a small capture layer" (~7 KB). Because the seam is already there,
that is not a lock-in — if Faro or OTel-web is wanted later, it plugs into the same `LogSink`
without touching anything upstream.

---

## Finding A — the ingest route trusts a client-controlled header

**Severity: real. Currently exploitable only against a console sink (see Finding B), but
Finding B is the thing most likely to be fixed first.**

`packages/logger/src/server.ts` derives both the rate-limit key and the stamped `clientIp` from
an inbound header:

```ts
function clientKey(request: Request, header: string): string {
  const value: string | null = request.headers.get(header);
  return value === null || value === "" ? UNKNOWN_CLIENT : value;
}
```

`x-wallow-client-ip` (`DEFAULT_CLIENT_IP_HEADER`) is an **internal seam header**.
`apps/wallow-web/src/app/lib/bff.server.ts` sets it from the host-supplied `request.ip` before
the API hop, and `packages/sdk/src/server/forwarded.ts` appends it to `x-forwarded-for` then
deletes it. Both of those protect the **outbound** path only.

Nothing strips it **inbound**:

- `apps/wallow-web/src/app/routes/bff/logs.ts` passes `request` through untouched.
- `docker/caddy/Caddyfile.example` has no `header_up` directives — correctly noting they are
  redundant for `X-Forwarded-*`, which leaves the custom header alone.

So anything that can produce a valid `Origin` — including page script on a same-origin
`fetch` — can set `x-wallow-client-ip` freely and:

1. **Defeat the rate limit.** Rotate the value per request; every request gets a fresh bucket.
   The limiter's key is entirely attacker-chosen. The route is unauthenticated by design, so
   this limiter is the control that matters.
2. **Forge `client.address`.** `packages/logger/src/otlp.ts` maps `clientIp` onto
   `client.address`, in a record that reads as server-stamped. `parseLogBatch`'s field-by-field
   rebuild does not defend it, because the value arrives as a header rather than in the body.

**Fix:** the ingest handler must take the peer address from the host and overwrite, never read
it off the wire — the pattern `bff.server.ts` already uses (`request.ip`, srvx's `PeerRequest`).
Applies to the handler plus both mount points (`/bff/logs` in wallow-web, `/logs` in
wallow-auth).

**Secondary benefit.** `sendBeacon` can set no headers at all, so today every terminal
`pagehide` flush falls into the single shared `UNKNOWN_CLIENT` bucket — one noisy client can
exhaust the tab-close budget for everyone. A host-supplied peer address keys beacons correctly
and cures this at the same time.

---

## Finding B — the logger is not connected to Grafana in any environment

Three independent breaks. All silent, because `emitOtlp` never throws and a valid batch answers
204 regardless of collector health (documented failure rule 3 in `packages/logger/CLAUDE.md`).

| Where | Break | Effect |
| --- | --- | --- |
| `api/src/Wallow.AppHost/Program.cs` | Aspire's `AddJavaScriptApp` **already** injects `OTEL_EXPORTER_OTLP_ENDPOINT`, pointing at the dashboard's **HTTPS gRPC** endpoint | Every batch dropped — see correction below |
| `docker/docker-compose.production.yml` (`wallow-auth`, declared ~437) | No endpoint set at all | console sink |
| `docker/docker-compose.production.yml:533` (`wallow-web`) | Points at `alloy:4317` — **gRPC** | `emitOtlp` POSTs OTLP/JSON, which needs **4318** |

**Correction to break 1.** The first diagnosis here — "the variable is unset, so dev falls back
to `consoleSink`" — was wrong, and the truth is worse. Aspire injects the variable into every
managed resource from `DOTNET_DASHBOARD_OTLP_ENDPOINT_URL`, so the logger had an endpoint all
along and it was wrong twice over: the logger POSTs OTLP/JSON over HTTP and has no gRPC
transport, and Aspire configures no certificate trust for JavaScript apps, so even the HTTPS
handshake would fail. Nothing fell back to the console; batches were built, POSTed and dropped.
The fix is therefore an explicit **override registered after `AddJavaScriptApp`**, not a missing
variable — and it carries `OTEL_EXPORTER_OTLP_PROTOCOL=http/json` with it, because Aspire sets
`grpc` beside the endpoint and leaving that naming an HTTP port re-lays the same trap.

`packages/logger/src/server.ts` falls back to `consoleSink` when `otlpEndpoint` is undefined, so
the first two produce working-looking logs that reach no collector; the third produces requests
Alloy's gRPC listener will not accept.

**Contributing cause.** The comment block at `docker/docker-compose.yml:66-76` documents 4317 as
"gRPC endpoint exposed by Alloy collector" inside a section scoped to "The Wallow API." That is
correct for .NET's gRPC exporter and misleading for the Node logger, which is HTTP/JSON only.

**Fix:** point wallow-web at `http://alloy:4318`; add the same to wallow-auth; add
`.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4318")` to both
`AddJavaScriptApp` calls; amend the compose comment. Then verify a record actually lands in
Loki rather than assuming a 204 means delivery.

---

## Provenance — the logger was not built blind to the telemetry stack

Worth recording because the question keeps recurring. `packages/logger/src/otlp.ts` is a
purpose-built OTLP/HTTP-JSON encoder: it emits OTel semantic conventions (`service.name`,
`enduser.id`, `exception.type`/`message`/`stacktrace`), maps levels onto OTel severity numbers,
and `otlpLogsUrl` honours the standard `OTEL_EXPORTER_OTLP_ENDPOINT` /
`OTEL_EXPORTER_OTLP_LOGS_ENDPOINT` split. It was written to hook into this collector.

The gap is configuration (Finding B), not intent or ignorance.

---

## Security posture — why the current transport is the one to keep

Recorded so the Faro question does not get relitigated from scratch:

- Alloy binds `127.0.0.1` in dev (`docker/docker-compose.yml:80`) and sits on the internal
  compose network in prod. It is never reachable from a browser.
- The page holds no collector credential — it holds a same-origin path.
- No CORS anywhere.
- The load-bearing guard is the **origin-versus-target check**: `Origin` is a forbidden header
  name so page script cannot forge it, and it is the one guard that survives the `sendBeacon`
  path where no header can be set. Both apps resolve it per request via `resolveRequestOrigin`.
- `service`, `userId`, `tenantId` and receipt time are **server-stamped**, and `parseLogBatch`
  rebuilds each event field-by-field so a payload carrying an extra `service` cannot smuggle it
  into a record.

Finding A is the one place this posture is not held.

---

## Spike results worth keeping

Carried forward from the superseded file. Still accurate; relevant only if Faro is
reconsidered.

- **The router wrapper trap is `Register`, not `subscribe`.** The flagged `subscribe`
  assignability is fine — `SubscribeFn` is a single generic that instantiates at `"onResolved"`.
  The real problem is the `declare module` `Register` block in `apps/wallow-web/src/app/router.tsx`:
  returning the wrapper's result adds exactly two errors (TS7023 + TS2502), degrading `getRouter`
  to `any` and erasing every route-tree type app-wide. Calling the wrapper for side effect and
  discarding the return is **+0 errors over baseline**, and is safe because every return path in
  the wrapper returns the same router object.
- **`item.type` does not narrow `item.payload`.** Faro's `TransportItem` is not a discriminated
  union in the way it reads; narrow structurally on `"context" in payload` / `"attributes" in
  payload` instead.
- **Bag types don't line up.** Faro's bags are `Record<string, string>`; `redactAttrs` answers
  `Record<string, unknown>`, so the bridge needs a narrowing step — and `redactAttrs`'
  depth-limited recursion is dead code on that path.
- **`redactAttrs` is not exported.** It is imported inside `packages/logger/src/index.ts` but
  absent from the export block. Any redaction bridge needs a one-line addition.
- **Traces bypass `beforeSend` redaction entirely** (see decision 1).

---

## Status

- **Finding A — done.** `Wallow-dayd`. `clientIpHeader` replaced by a `clientAddress` seam; the
  handler reads no inbound header for the IP. Both apps supply `request.ip`.
  `DEFAULT_CLIENT_IP_HEADER` deleted as dead surface and the app-side parity spec narrowed to
  `REQUEST_ID_HEADER`. Five regression tests, including the two that are the actual security
  assertions: a rotating header mints no fresh rate-limit bucket, and an inbound header is
  ignored for the stamp.
- **Finding B — done.** `Wallow-bzdy`. All three breaks fixed plus the misleading compose
  comment. Delivery verified live: the real `createLogIngestHandler` → Alloy `:4318` → records
  returned from Loki inside `wallow-grafana-lgtm`; `:4317` reproduced the failure. The one leg
  not exercised live is Aspire actually launching the Node processes with the new environment —
  that rests on an Aspire source read plus a clean build.
- **Remaining: the capture layer.** Decide whether to grow `logger`'s ~7 KB browser capture
  (global error/rejection handlers, web-vitals, router subscription). Not urgent, not blocked.

The coupling between A and B, now historical: until an OTLP endpoint worked, a forged
`client.address` landed in a console sink; once it worked, it would have landed in Loki looking
server-stamped. Both shipped together, so the window never opened.

## Not doing

- Faro (decision 1). Revisit only if linked distributed traces become a requirement, and cost
  the trace-redaction hole into that decision.
- Replacing `otlp.ts` with the OpenTelemetry JS logs SDK (decision 2). Revisit at 1.0.
- A `packages/telemetry` package (decision 3) — the seam it would need already exists.
