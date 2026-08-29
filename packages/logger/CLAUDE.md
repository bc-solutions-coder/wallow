# packages/logger — @bc-solutions-coder/logger Agent Guide

Wallow's structured logging, both ends of it: a **browser core** that buffers, filters,
redacts and posts batches to a same-origin route, and the **app-server ingest handler** that
guards, stamps and forwards them to OTLP. Same record shape at both ends, so a request that
starts in the page and finishes on the server produces two records a collector joins on
`wallow.correlation_id`.

Like `packages/utils` and `packages/env` this sits at the bottom of the graph:
`dependencies` and `peerDependencies` are both `{}`, `tsconfig.json` sets `types: []`, and
no shipped module names a `node:*` builtin, `process.env` or `import.meta.env` — except the
three server-owned modules `server.ts`, `otlp.ts` and `rate-limit.ts`, the allowlist of
**`wallow/logger-no-node-builtins`** (root `.oxlintrc.json`). Moving a module across that
boundary means updating the rule's allowlist and this file together. `types: []` also fails
a build on a `node:*` import or `process` reference in either entry.

## Two entries

| Entry                        | Runs in | What it holds                                                                                                                                         |
| ---------------------------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`)         | Browser | `createLogger(options)` → `Logger` (`debug`/`info`/`warn`/`error`/`child`/`flush`/`dispose`), plus the wire contract re-exported from `log-event.ts`. |
| `./server` (`src/server.ts`) | Node    | `createLogIngestHandler(options)` → `(request) => Promise<Response>`, `createServerLogger(options)`, and the OTLP encoder / rate limiter it composes. |

The browser bundle must not carry the guards, the limiter or the OTLP encoder — they are
dead weight in a page and a map of the server's checks. `index.ts` must never mention
`createLogIngestHandler`, `createRateLimiter` or `toOtlpLogsPayload`; nothing enforces this
mechanically, so treat it as load-bearing on review.

`createLogIngestHandler` returns the handler (rather than handling directly) **because the
rate limiter is state that must live across requests** — build the handler once at module
scope; a limiter constructed per call counts to one and never refuses anything.

## One transport, one record format, one handler, TWO mount points

Transport is **browser → app server → OTLP**. The page never talks to a collector and never
holds a collector credential; what it holds is a same-origin path.

- **`wallow-web`** mounts the handler at `/bff/logs`. CSRF applies there because of _where
  the route lives_; the app supplies an `authorize` verifier built on the SDK's
  `csrfTokenMatches`.
- **`wallow-auth`** mounts the same handler on its own Start server route at `/logs`, with
  **no** `authorize`: it holds no session, so there is no token to check.

Neither app reimplements a guard. A third app mounts this handler too.

## CSRF is not the control this endpoint needs

The controls that actually apply, in both apps:

- **An origin allowlist** — `Origin` is a forbidden header name page script cannot forge,
  and the one guard that survives the `sendBeacon` path where no header can be set. An empty
  `allowedOrigins` rejects everything — the right failure direction. It also takes a
  **function**, resolved per request: both apps answer with the origin this request was
  addressed to (`createRequestOriginResolver`, gated by `WALLOW_TRUSTED_PROXIES`).
- **Payload caps**, which reject rather than truncate. `maxBodyBytes` defaults to 64 KiB —
  `sendBeacon`'s own quota, so a batch the browser would refuse to queue is a batch the
  server would refuse to read.
- **A per-IP rate limit**, because the route is unauthenticated by design — keyed on the
  address the HOST supplies, never on anything inbound.
- **Server-side stamping** of receipt time, client IP, service, correlation id and
  tenant/user — every field a page could otherwise assert about itself. The browser core
  deliberately does **not** put its own `service` on the wire (`logger.test.ts` asserts it).

**The client address arrives through a callback, not a header.** `clientAddress?: (request)
=> string | undefined` is the only source of the peer for both the rate-limit key and the
stamped `clientIp` — a header on an unauthenticated route is a value the caller chooses.
Both apps answer with `request.ip` (srvx's connection peer address). Absent, or answering
`undefined`, every caller shares the one `unknown` bucket and nothing is stamped: limiting
too much and claiming nothing is the correct failure direction.

`parseLogBatch` rebuilds each accepted event field by field rather than spreading the wire
object, so a payload carrying an extra `service` or `userId` cannot smuggle it past
validation into a record.

## Three failure rules

A logger that fails loudly is worse than none:

1. **A transport error never calls the logger.** It falls back to `console.warn` and
   disables transport for a backoff window (default 30 s). Failed events are **not
   requeued** — a requeue turns one unreachable route into a buffer that refills itself
   every window and never drains.
2. **The buffer drops OLDEST on overflow** (default ceiling 200) and prepends a
   `logger.dropped` warn carrying `{ count }` to the next batch.
3. **A valid batch answers 204 regardless of collector health.** The guards still return
   real 405/403/413/429/400; only sink failure is swallowed. The page's behaviour must never
   change because telemetry is down.

## Events are NAMED, not prose

Dotted, low-cardinality, enforced on the wire by
`/^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$/u`: `form.submitted`, `bff.logout.failed`,
`logger.dropped`. Free text goes in `attrs`, where it costs one attribute rather than one
metric series. A rejected name fails the whole batch.

## `sendBeacon` cannot set headers

The terminal `pagehide` flush uses `navigator.sendBeacon` — the only transport a browser
promises to finish after the document is gone — so the CSRF token rides in the **body**
there. Normal flushes use `fetch(endpoint, { keepalive: true, credentials: "same-origin" })`
with an `x-csrf-token` header. The handler accepts the token from either place, which is why
`authorize` receives the parsed batch and not just the request.

`visibilitychange` → hidden carries most of the load (tab switch, app switch, and the mobile
discard path that never fires `pagehide` at all); `pagehide` is the last call.

## It does not depend on the SDK

Correlation rides the same `x-request-id` contract the SDK's proxy uses, but
`REQUEST_ID_HEADER` is **declared locally** — importing it would drag an OIDC client into
every consumer. An app-side spec pins the constant equal to the SDK's. It is the only header
this package reads a value out of; there is deliberately no client-IP constant to mirror.

## Tests

`vitest.config.ts` runs the standard two-project split, keyed on file **extension**:

- `src/*.test.ts` → node: buffering, level filter, redaction, correlation stamping,
  transport failure and backoff, batch splitting, the guard chain, stamping, OTLP encoding,
  the limiter.
- `src/logger.test.tsx` → **real headless Chromium**, the only file there: `pagehide` →
  `sendBeacon`, `visibilitychange` → hidden, and `dispose` unregistering both listeners —
  exactly where "logs vanish when the tab closes" hides.

The browser project's `vitest.setup.ts` installs the three escape guards (console,
navigation, network) from `@bc-solutions-coder/testing`'s subpath entries — those subpaths
import nothing but `vitest`, so the project pair stays hand-rolled and the package stays
React-free. Stub `fetch` with `vi.stubGlobal` and restore with `vi.unstubAllGlobals()` so a
stubbed transport never reads as an escape. Outside a browser `createLogger` registers no
listeners and starts no timer — which is what makes the buffer assertable without a DOM.

Scripts: `pnpm --filter @bc-solutions-coder/logger build` (Vite lib mode + `tsc -p
tsconfig.build.json`), `test`, `test:watch`, `typecheck` (both tsconfigs).
