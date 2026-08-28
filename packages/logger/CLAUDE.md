# packages/logger — @bc-solutions-coder/logger Agent Guide

Wallow's structured logging, both ends of it: a **browser core** that buffers, filters, redacts
and posts batches to a same-origin route, and the **app-server ingest handler** that guards,
stamps and forwards them to OTLP. Same record shape at both ends, so a request that starts in
the page and finishes on the server produces two records a collector joins on
`wallow.correlation_id`.

Like `packages/utils` and `packages/env` this sits at the bottom of the graph: `dependencies`
and `peerDependencies` are both `{}`, `tsconfig.json` sets `types: []`, and no shipped module
names a `node:*` builtin, `process.env` or `import.meta.env`. `src/charter.test.ts` pinned that
by reading the manifest and every module off disk; it is gone (`Wallow-xg9t.1`). `types: []` is
what still fails a build here — a `node:*` import or a `process` reference does not compile.

## Two entries

| Entry                        | Runs in | What it holds                                                                                                                                         |
| ---------------------------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.` (`src/index.ts`)         | Browser | `createLogger(options)` → `Logger` (`debug`/`info`/`warn`/`error`/`child`/`flush`/`dispose`), plus the wire contract re-exported from `log-event.ts`. |
| `./server` (`src/server.ts`) | Node    | `createLogIngestHandler(options)` → `(request) => Promise<Response>`, `createServerLogger(options)`, and the OTLP encoder / rate limiter it composes. |

The browser bundle must not carry the guards, the limiter or the OTLP encoder — they are dead
weight in a page and a map of the server's checks: `index.ts` must never mention
`createLogIngestHandler`, `createRateLimiter` or `toOtlpLogsPayload`. **The constraint is real
even though it is unheld** — the charter spec that asserted it went with the rest of the
source-reading guards (`Wallow-xg9t.1`), so treat it as load-bearing on review.

## One transport, one record format, one handler, TWO mount points

Transport is **browser → app server → OTLP**. The page never talks to a collector and never
holds a collector credential; what it holds is a same-origin path.

- **`wallow-web`** mounts the handler at `/bff/logs`. CSRF applies there because of _where the
  route lives_, and the app supplies an `authorize` verifier built on the SDK's
  `csrfTokenMatches`.
- **`wallow-auth`** mounts the same handler on its own Start server route at `/logs`, with **no**
  `authorize`: it holds no session, so there is no token to check.

Neither app reimplements a guard. If a third app appears, it mounts this handler too.

## CSRF is not the control this endpoint needs

Treating it as one is how an ingest route ends up unprotected in the app that has no session.
The controls that actually apply, in both apps:

- **An origin allowlist** — load-bearing rather than advisory. `Origin` is a forbidden header
  name, so page script cannot forge it, and it is the one guard that survives the `sendBeacon`
  path where no header can be set. An empty `allowedOrigins` rejects everything, which is the
  right failure direction for a misconfigured deployment. It also takes a **function**, resolved
  per request — which is how both apps wire it: they answer with the origin this request was
  addressed to (`resolveRequestOrigin`, so a reverse proxy's `x-forwarded-proto` counts), making
  the guard the classic Origin-versus-target check and needing no new environment variable.
- **Payload caps**, which reject rather than truncate. `maxBodyBytes` defaults to 64 KiB —
  `sendBeacon`'s own quota, so a batch the browser would refuse to queue is a batch the server
  would refuse to read.
- **A per-IP rate limit**, because the route is unauthenticated by design — keyed on the address
  the HOST supplies, never on anything inbound.
- **Server-side stamping** of receipt time, client IP, service, correlation id and tenant/user —
  every field a page could otherwise assert about itself. The browser core deliberately does
  **not** put its own `service` on the wire; `logger.test.ts` asserts the serialized batch does
  not contain it.

**The client address arrives through a callback, not a header.** `clientAddress?: (request) =>
string | undefined` is the only source of the peer for both the rate-limit key and the stamped
`clientIp`. A header cannot be that source: on an unauthenticated route it is a value the caller
chooses, so rotating it mints a fresh limiter bucket per request and forges a field that reads as
server-stamped. Both apps answer with `request.ip` — srvx puts the connection's peer address
there, the same seam `bff.server.ts` and `api-passthrough.server.ts` already use for the outbound
`CLIENT_IP_HEADER` hop. Absent, or answering `undefined`, every caller shares the one `unknown`
bucket and nothing is stamped: limiting too much and claiming nothing is the correct failure
direction, and it is also what a `sendBeacon` flush gets in a host that cannot answer.

`parseLogBatch` rebuilds each accepted event field by field rather than spreading the wire
object, so a payload carrying an extra `service` or `userId` cannot smuggle it past validation
into a record.

## Three failure rules

A logger that fails loudly is worse than none:

1. **A transport error never calls the logger.** It falls back to `console.warn` and disables
   transport for a backoff window (default 30 s). The failed events are **not requeued** — a
   requeue turns one unreachable route into a buffer that refills itself every window and never
   drains.
2. **The buffer drops OLDEST on overflow** (default ceiling 200) and prepends a
   `logger.dropped` warn carrying `{ count }` to the next batch.
3. **A valid batch answers 204 regardless of collector health.** The guards still return real
   405/403/413/429/400; only sink failure is swallowed. The page's behaviour must never change
   because telemetry is down.

## Events are NAMED, not prose

Dotted, low-cardinality, enforced on the wire by `/^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$/u`:
`form.submitted`, `bff.logout.failed`, `logger.dropped`. Free text goes in `attrs`, where it
costs one attribute rather than one metric series. A rejected name fails the whole batch with
`"event name is not a dotted low-cardinality name"`.

## `sendBeacon` cannot set headers

The terminal `pagehide` flush uses `navigator.sendBeacon` — the only transport a browser
promises to finish after the document is gone — so the CSRF token rides in the **body** there.
Normal flushes use `fetch(endpoint, { keepalive: true, credentials: "same-origin" })` with an
`x-csrf-token` header. The handler accepts the token from either place, which is why
`authorize` receives the parsed batch and not just the request.

`visibilitychange` → hidden carries most of the load (tab switch, app switch, and the mobile
discard path that never fires `pagehide` at all); `pagehide` is the last call.

## It does not depend on the SDK

Correlation rides the same `x-request-id` contract the SDK's proxy uses, but `REQUEST_ID_HEADER`
is **declared locally**. Importing it would drag an OIDC client into every consumer of a logging
package. The drift that buys is pinned instead by an app-side spec asserting the constant equals
the SDK's `REQUEST_ID_HEADER` — the apps depend on both packages already, so that is the cheap
place for the assertion to live. It is the only header this package reads a value out of; there
is deliberately no client-IP constant to mirror.

## Deviation from the bead's wording

The bead specified `handleLogIngest(request, { … })`. What shipped is
`createLogIngestHandler(options)` returning the handler, **because the rate limiter is state
that must live across requests**: a limiter constructed per call counts to one and never refuses
anything. The route module builds the handler once at module scope and the route calls it.

## Tests

`vitest.config.ts` runs the standard two-project split, keyed on file **extension**:

- `src/*.test.ts` → node. Buffering, level filter, redaction, correlation stamping, transport
  failure and backoff, batch splitting, the guard chain, stamping, OTLP encoding, and the
  limiter.
- `src/logger.test.tsx` → **real headless Chromium**, and it is the only file there. `pagehide`
  → `sendBeacon`, `visibilitychange` → hidden, and `dispose` unregistering both listeners are
  the behaviours that cannot be asserted on node, and they are exactly where "logs vanish when
  the tab closes" hides.

The browser project's `vitest.setup.ts` installs the three escape guards (console, navigation,
network) from `@bc-solutions-coder/testing`'s subpath entries. That devDependency buys only the
guards — the subpaths import nothing but `vitest`, so the project pair stays hand-rolled and the
package stays React-free. The browser spec's `vi.stubGlobal("fetch", …)` doubles sit over the
network guard's wrapper and `vi.unstubAllGlobals()` restores it, so a stubbed transport never
reads as an escape.

Outside a browser `createLogger` registers no listeners and starts no timer — which is what
makes the buffer assertable without a DOM.

Scripts: `pnpm --filter @bc-solutions-coder/logger build` (Vite lib mode + `tsc -p
tsconfig.build.json`), `test`, `test:watch`, `typecheck` (both tsconfigs).
