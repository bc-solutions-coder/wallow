# packages/logger — @bc-solutions-coder/logger Agent Guide

Structured logging, both ends: a browser core (`.`) that buffers, filters, redacts and posts
batches to a same-origin route, and the app-server ingest handler (`./server`) that guards,
stamps and forwards to OTLP.

- **`server.ts`, `otlp.ts` and `rate-limit.ts` are the ONLY modules allowed `node:*` /
  `process.env`** — the allowlist of `wallow/logger-no-node-builtins` (root
  `.oxlintrc.json`). Moving a module across that boundary means updating the rule's
  allowlist and this file together.
- `index.ts` must never mention `createLogIngestHandler`, `createRateLimiter` or
  `toOtlpLogsPayload` — nothing enforces this mechanically; treat it as load-bearing on
  review.
- **Build the ingest handler ONCE at module scope** — the rate limiter is state that must
  live across requests; a handler built per call counts to one and never refuses anything.
- Both apps mount the same handler; CSRF applies because of where a route lives, not what
  the endpoint needs.

## The controls that actually apply

- **The origin allowlist is THE control** — `Origin` is a forbidden header page script
  cannot forge, and the one guard that survives the `sendBeacon` path where no header can
  be set. An empty `allowedOrigins` rejects everything, by design.
- `authorize` receives the parsed batch, not just the request, because the beacon path
  carries the CSRF token in the **body**.
- **The client address arrives through the `clientAddress` callback, never a header** — the
  sole source for both the rate-limit key and the stamped `clientIp`; a header on an
  unauthenticated route is a value the caller chooses. Absent, or answering `undefined`,
  every caller shares the one `unknown` bucket and nothing is stamped — limiting too much
  and claiming nothing is the correct failure direction.
- The browser core never puts its own `service` on the wire; the server stamps everything a
  page could otherwise assert about itself.
- `parseLogBatch` rebuilds each event field by field, so an extra wire field cannot smuggle
  past validation into a record.
- Event names, not prose (`form.submitted`) — a rejected name fails the whole batch.

## Three failure rules — do not "fix" these

1. **A transport error never calls the logger** — `console.warn` fallback plus a backoff
   window; failed events are NOT requeued (a requeue turns one unreachable route into a
   buffer that refills itself every window and never drains).
2. **The buffer drops OLDEST on overflow** and prepends a `logger.dropped` warn carrying
   `{ count }` to the next batch.
3. **A valid batch answers 204 regardless of collector health.** The guards still return
   real 405/403/413/429/400; only sink failure is swallowed — the page's behaviour must
   never change because telemetry is down.

## No SDK dependency

`REQUEST_ID_HEADER` is declared locally on purpose — importing the SDK's would drag an OIDC
client into every consumer; an app-side spec pins the constant equal to the SDK's.

## Tests

- `src/logger.test.tsx` is the only browser spec — the `pagehide`/`sendBeacon`/`dispose`
  surface, exactly where "logs vanish when the tab closes" hides.
- Outside a browser `createLogger` registers no listeners and starts no timer — which is
  what makes the buffer assertable without a DOM.
