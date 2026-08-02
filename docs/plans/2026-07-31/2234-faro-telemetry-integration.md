# Faro telemetry integration — design sketch

**status: superseded**

Superseded by `2318-logging-telemetry-decision-record.md`, which reverses the recommendation
(Faro is not adopted) and records two defects found while validating this sketch. The spike
results in this file are still accurate and are summarised there; everything above them —
the two-streams argument and the `packages/telemetry` shape — is the part that was reversed.

A sketch for adding Grafana Faro RUM to the two Start apps, on top of the Grafana stack
`docker/docker-compose.yml` already runs, without disturbing `@bc-solutions-coder/logger`.

## What is already true

- `docker/alloy/config.alloy` runs Grafana Alloy v1.16.0, OTLP in on 4317/4318, one exporter
  out to `grafana-lgtm:4317`. Verified: both `faro.receiver` and `otelcol.receiver.faro` are
  compiled into the running binary, and Loki listens on `:3100` inside `wallow-grafana-lgtm`.
- `@bc-solutions-coder/logger` is complete and wired into both apps: browser buffer →
  `POST /bff/logs` → origin check → rate limiter → session-derived `userId`/`tenantId`
  stamping → OTLP/JSON → Alloy. Curated, named, low-cardinality domain events.
- **The logger's charter forbids Faro living inside it.** Its `package.json` declares no
  `dependencies` and no `peerDependencies`; `packages/logger/src/charter.test.ts` and the root
  `.oxlintrc.json` both pin that. Faro therefore goes in a new package that sits *above* logger.

## The two streams, and why both

| | `@bc-solutions-coder/logger` | Faro |
| --- | --- | --- |
| Carries | Deliberate domain events (`bff.logout.failed`) | Automatic RUM: errors, web vitals, route changes, traces |
| Names | Author-chosen, validated dotted pattern | SDK-chosen |
| Identity | Server-stamped from session | Page-asserted (fixed below) |
| Signal | Logs | Logs **and traces** |

The reason to adopt Faro is the **trace pipeline and sourcemaps**. If you take only its
instrumentation and push the results through `log`, you are paying a dependency for web-vitals
and could hand-roll it in ~50 lines. Traces are what light up "click → API span" in Tempo
against the spans the .NET API already emits, and that is not something the logger can grow
into — it is a logs transport by construction.

So: two pipelines, each doing what it is good at, joined by a correlation seam (below).

## Package: `packages/telemetry`

`@bc-solutions-coder/telemetry`. Sits above `logger`, mirrors its structure.

```
packages/telemetry/
  src/
    index.ts        initTelemetry() — the guarded initializeFaro
    transport.ts    WallowFaroTransport — same-origin POST + CSRF + beacon
    redact.ts       the beforeSend bridge over logger's redactAttrs
    router.ts       re-export of withFaroRouterInstrumentation
    server.ts       createFaroIngestHandler — the proxy to Alloy
    charter.test.ts
```

Entries `.`, `./router`, `./server`, matching logger's `exports` + `publishConfig` shape.

**Dependencies must be peers, not deps.** `@unpunnyfuns/faro-tanstack-router` keeps Faro's
`api` in module-level mutable state (`export let api`). Two resolved copies means the
instrumentation writes one module state and the router wrapper reads another — the wrapper then
`console.warn`s once and silently drops every route event. Same failure class as a second
`useNavStore` or a second `QueryClient`. Peers: `@grafana/faro-web-sdk`,
`@grafana/faro-web-tracing`, `@grafana/faro-react`, `@tanstack/router-core`. Its own
`charter.test.ts` should assert that, and assert no `sdk` edge.

### `src/index.ts`

```ts
export interface TelemetryOptions {
  /** "wallow-web". Becomes Faro's app.name. */
  service: string;
  version: string;
  /** Same-origin path. The page never talks to a collector. */
  endpoint: string;
  environment?: string;
  getCsrfToken?: () => string | null;
  redact?: readonly string[];
  /** Traces cost ~an extra bundle. Default true. */
  tracing?: boolean;
  /** Origins the traceparent header may be propagated to. */
  propagateTraceHeaderCorsUrls?: RegExp[];
}

export function initTelemetry(options: TelemetryOptions): Faro | undefined {
  if (typeof document === "undefined") {
    return undefined;          // SSR: same guard shape as logger's inBrowser()
  }

  return initializeFaro({
    app: { name: options.service, version: options.version, environment: options.environment },
    transports: [new WallowFaroTransport(options)],
    instrumentations: [
      ...getWebInstrumentations(),
      new ReactIntegration(),               // error boundary + profiler, NO router option
      new TanStackRouterInstrumentation(),
      ...(options.tracing === false ? [] : [new TracingInstrumentation({
        instrumentationOptions: {
          propagateTraceHeaderCorsUrls: options.propagateTraceHeaderCorsUrls ?? [],
        },
      })]),
    ],
    beforeSend: redactTransportItem(options.redact ?? DEFAULT_REDACT_KEYS),
  });
}
```

`ReactIntegration` is registered with **no** `router` option, and
`createReactRouterV6Options` / faro-react's own `withFaroRouterInstrumentation` are never used —
the TanStack package replaces the router half only.

### `src/transport.ts`

Faro's `FetchTransport` takes static `requestOptions.headers`, but the BFF's CSRF token is
dynamic and the terminal flush needs `sendBeacon` (which cannot set headers at all). That is
~30 lines Faro does not provide, and `packages/logger/src/index.ts` has already proved the
shape:

```ts
export class WallowFaroTransport extends BaseTransport {
  readonly name = "@bc-solutions-coder/telemetry";
  readonly version = VERSION;

  override isBatched(): boolean { return true; }

  async send(items: TransportItem[]): Promise<void> {
    const token = this.options.getCsrfToken?.() ?? null;
    // header when we can, body when we cannot — the ingest handler reads either,
    // exactly as createLogIngestHandler already does for LogBatch.
  }
}
```

Register a `pagehide` listener that switches to `navigator.sendBeacon`, and put the token in the
payload on that path.

### `src/redact.ts`

`beforeSend` is the client-side half of the two-pass redaction the logger already documents.
Reuse the real function rather than a second copy:

```ts
import { redactAttrs } from "@bc-solutions-coder/logger";   // see note below
```

**One-line change needed in logger**: `redactAttrs` is defined in `log-event.ts` but is *not* in
`index.ts`'s re-export block. Add it. Depending on `logger` from `telemetry` is legal — the
charter constrains what logger imports, not who imports logger.

The hook walks by item type — log/exception context, event attributes, measurement context —
and returns `null` to drop an item entirely. Verify the `TransportItem` payload union against
the v2 types; it is the one shape here I have not read.

### `src/server.ts`

```ts
export interface FaroIngestOptions {
  /** Alloy's Faro receiver, server-to-server. Never reaches the browser. */
  receiverUrl: string;
  /** Stamped here, so it is never in the bundle. */
  apiKey?: string;
  allowedOrigins: (request: Request) => string[];
  authorize?: (request: Request) => Promise<boolean>;
  /** Overwrites the page-asserted meta.user. This is what restores server-owned identity. */
  identify?: (request: Request) => Promise<{ userId?: string; tenantId?: string }>;
  limiter?: RateLimiter;
}
```

Handler: origin check → rate limit (`packages/logger`'s `RateLimiter` is reusable as-is) →
parse → **overwrite `payload.meta.user`** from `identify` → POST to `receiverUrl` with
`X-API-Key` → always answer 204. A page's behaviour never changes because telemetry is down,
same rule the logger states.

That overwrite is the whole reason for proxying rather than letting the browser hit Alloy: Faro's
`meta.user` is otherwise whatever the page claims, which is exactly what `otlp.ts` refuses to
accept for log records.

## App wiring

### `src/app/router.tsx` (both apps)

```ts
import { withFaroRouterInstrumentation } from "@bc-solutions-coder/telemetry/router";

const router = withFaroRouterInstrumentation(
  createTanStackRouter({ routeTree, context: { queryClient, sdk }, scrollRestoration: true }),
);
```

`getRouter()` runs once per request on the server; the wrapper returns the router untouched when
`router.isServer`, so nothing subscribes and nothing is sent server-side. The signature is a
generic pass-through (`<TRouter extends InstrumentableRouter>(r: TRouter): TRouter`), so the
inferred route-tree types the existing comment protects survive.

**Spiked — and the naive form above is wrong.** See "Spike results" below: returning the
wrapper's result from `getRouter` erases the route-tree types across the whole app. Call it for
its side effect instead:

```ts
setupRouterSsrQueryIntegration({ router, queryClient });

withFaroRouterInstrumentation(router);   // side-effecting; returns the same object

return router;
```

### `src/shared/lib/telemetry.ts` (mirrors `log.ts`)

```ts
export const faro = initTelemetry({
  service: "wallow-web",
  version: __APP_VERSION__,
  endpoint: "/bff/telemetry",
  getCsrfToken: () => getCsrfToken() ?? readCsrfCookie(),
  propagateTraceHeaderCorsUrls: [/^\/api\//u],
});
```

Imported from `__root.tsx`. The SSR guard lives inside `initTelemetry`, so the module-level call
is safe in a file that renders on the server.

### The correlation seam

The one thing that makes two streams behave like one. Give the logger Faro's session/trace
context:

```ts
export const log: Logger = createLogger({
  service: "wallow-web",
  endpoint: "/bff/logs",
  getCsrfToken: () => getCsrfToken() ?? readCsrfCookie(),
  getCorrelationId: () => faro?.api.getSession()?.id,   // verify accessor name against v2
});
```

`otlp.ts` already maps `correlationId` to the `wallow.correlation_id` attribute, so a Grafana
query joins a curated domain event to the RUM session and its traces with no further work.

### Routes

`src/app/routes/bff/telemetry.ts` + `src/app/lib/telemetry-ingest.server.ts` for wallow-web,
mirroring `bff/logs.ts` + `log-ingest.server.ts` line for line. wallow-auth gets the passthrough
equivalent under `shared/lib/`. Server-only modules stay `*.server.*` per
`server-only-naming.test.ts`.

## Alloy

```alloy
otelcol.receiver.faro "frontend" {
  endpoint = "0.0.0.0:8080"
  output {
    logs   = [otelcol.exporter.otlp.backend.input]
    traces = [otelcol.exporter.otlp.backend.input]
  }
}
```

Chosen over `faro.receiver` because it feeds the **existing** exporter, so frontend and backend
logs land in Loki through one ingestion path with consistent resource attributes.
`faro.receiver` would need its own `loki.write` straight to `grafana-lgtm:3100`, bypassing
otel-lgtm's collector and giving frontend logs different labels than the API's Serilog output —
which quietly breaks the correlated-log story.

Costs of that choice: it is marked **Experimental**, so `docker-compose.yml` needs
`command: run --stability.level=experimental /etc/alloy/config.alloy`, and that flag relaxes
stability enforcement for other components too. It also has no sourcemap support — if
unminified stack traces matter more than label consistency, take `faro.receiver` instead and
accept the split ingestion path.

**No CORS block and no published port are required**, because the app server proxies: the
receiver only ever sees same-network server-to-server requests. Dev is the exception — the apps
run on the host under `pnpm dev`/Aspire, so publish `127.0.0.1:8080:8080` (or move the receiver
to 12347 to dodge a common port clash). Still not public.

## Spike results

Both open risks were spiked against the real tree. Faro **2.9.0**, `@tanstack/react-router`
**1.170.18** / `router-core` **1.171.15**. `tsconfig.base.json` sets `strict: true`, so
`strictFunctionTypes` was on and every assignability result below is contravariant, not
bivariant. `exactOptionalPropertyTypes` is off.

Method: patch, run `tsc --noEmit`, `comm` the sorted error set against a captured baseline. The
baseline is **42 errors, not zero** — `@shared/testing/*` helpers are missing mid-refactor — so
raw counts prove nothing and only the delta is attributable.

### 1. The router wrapper — a real trap, with a one-line fix

The structural fit is fine. Copying `faro-tanstack-router/src/types.ts` verbatim and assigning
the real router to it, every member passes: `isServer`, `state.matches`, `state.location`, and
**`subscribe`** — the one I flagged. `SubscribeFn` is a single generic
(`<TType extends keyof RouterEvents>(eventType: TType, fn: ListenerFn<RouterEvents[TType]>)`),
not an overload set, so it instantiates at `"onResolved"` and satisfies the concrete signature.
No cast needed.

The trap is somewhere else. `router.tsx` ends with

```ts
declare module "@tanstack/react-router" {
  interface Register { router: ReturnType<typeof getRouter> }
}
```

so `getRouter`'s return type feeds back into the router type that the generic's constraint must
be checked against. Returning `withFaroRouterInstrumentation(router)` closes that loop:

```
src/app/router.tsx(66,17): error TS7023: 'getRouter' implicitly has return type 'any' because it
  does not have a return type annotation and is referenced directly or indirectly in one of its
  return expressions.
src/app/router.tsx(83,5): error TS2502: 'router' is referenced directly or indirectly in its own
  type annotation.
```

Exactly +2 errors over baseline, nothing else. **This is the failure mode the existing comment
at `router.tsx:57` warns about, arriving by a different door** — `getRouter` degrades to `any`,
and with it every `Link`, `useParams` and `useSearch` in the app. Two errors in one file; the
blast radius is the entire route-tree type surface.

The fix is free, because the wrapper is purely side-effecting — every return path in
`withFaroRouterInstrumentation.ts` is `return router`, the same object. Discard the return
value and the loop never closes: **+0 errors over baseline.** That variant is what the wiring
section above now shows.

Worth stating in the eventual code comment, because "why is this call's result thrown away" is
precisely the thing a later reader will tidy up and re-break.

### 2. The `beforeSend` bridge — two type facts that change the code

`BeforeSendHook<P = APIEvent> = (item: TransportItem<P>) => TransportItem<P> | null` — per item,
return `null` to drop, as sketched. But:

- **`item.type` does not narrow `item.payload`.** `TransportItem` declares
  `type: TransportItemType` and `payload: P` as *independent* fields, with `P` defaulting to the
  whole `APIEvent` union. A `switch (item.type)` reads naturally and narrows nothing. Compiled
  under `@ts-expect-error` to pin it. Narrow structurally on the payload instead —
  `"context" in payload`, `"attributes" in payload` — which also collapses the walk to two
  branches, since `LogEvent`/`ExceptionEvent`/`MeasurementEvent` all key their bag `context` and
  only `EventEvent` uses `attributes`.
- **Every bag is `Record<string, string>`**, not `Record<string, unknown>`: `LogContext`,
  `ExceptionContext`, `EventAttributes`, `MeasurementContext`. `redactAttrs` answers
  `Record<string, unknown>`, which will not assign back. Needs a narrowing step on the way out.
  Two consequences: `redactAttrs`'s nested-object recursion and `MAX_REDACT_DEPTH` are dead code
  on this path (values are always strings), so only the key matching does any work — and the
  reuse is worth it for the shared key list and identical semantics, not for the walk.

The bridge compiles clean with those two accommodations.

### 3. Traces bypass `beforeSend` redaction entirely

`TraceEvent` is `{ resourceSpans?: IResourceSpans[] }` — no context or attributes bag. There is
nothing for a key-based scrub to reach, so **span attributes are not redacted by this design**,
and `TracingInstrumentation` records full request URLs. Query strings carrying a token or an
email would ship unredacted.

Not a blocker, but it is a hole in the "redaction runs twice" story the logger's docs tell, and
it must be closed somewhere else: `ignoreUrls`, an OTel span processor, or an Alloy-side
`otelcol.processor.transform`. Decide before enabling tracing, not after.

### 4. Smaller findings

- **Name collisions.** Faro exports `LogEvent` and `LogLevel`; so does
  `@bc-solutions-coder/logger`, with different shapes (Faro's `LogEvent` is
  `{context, level, message, timestamp}`). The telemetry package must alias on import. This is
  the kind of collision that produces a baffling error three files away.
- **The correlation seam checks out**, with a better option than sketched.
  `faro.api.getSession()` exists on `MetaAPI` returning `MetaSession { id?: string }`, so
  `getCorrelationId: () => faro?.api.getSession()?.id` types. But `TracesAPI.getTraceContext()`
  returns `{ trace_id, span_id }` — using `trace_id` joins a Wallow log record to an actual
  **span** in Tempo rather than merely to a session. Prefer it, falling back to the session id.
- **`BaseTransport`, `TransportItemType`, `getTransportBody` and `FetchTransport` are all
  exported from `@grafana/faro-web-sdk`**, so the custom transport needs no `@grafana/faro-core`
  import and the peer list drops to three.
- **`MetaUser` carries `email?: string`**, which the browser asserts. The server proxy's
  `identify` overwrite handles it, but the `beforeSend` hook should strip it too so it never
  sits in a beacon body — same two-pass reasoning as `redactAttrs`.

## Open decisions

1. **Depend on `@unpunnyfuns/faro-tanstack-router`, or vendor it?** Recommend depend. It is
   MIT, published, 22 KB, ~250 lines, actively released. Depending also sidesteps its jsdom +
   `@testing-library/react` test setup, which `.claude/rules/TESTING.md` bans — only `dist`
   ships. Vendor later if it goes stale; the facade means no app-layer code changes if so.
   - Caveat A: no `LICENSE` file in the repo (MIT is asserted in `package.json`/README only).
     Fine to depend on, awkward to vendor — there is no copyright line to reproduce. Worth an
     upstream issue, and relevant to the MIT prior-art-harvest work.
   - Caveat B: it detects replace-navigations by reading `toLocation.state.__TSR_index`, a
     private TanStack field. If that is renamed, every `navigate({ replace: true })` starts
     emitting a `route_change` — noisy, not broken, and it will not fail a build.
   - Caveat C: `.npmrc` scopes `@bc-solutions-coder` to GitHub Packages; confirm `@unpunnyfuns`
     still resolves from public npm.
2. **Does `tracing` default on?** It is the reason to do this at all, but it is the bulk of the
   bundle. Suggest on, measured.
3. **Lint enforcement.** Mirror the react-query rule: a root `.oxlintrc.json`
   `no-restricted-imports` entry banning direct `@grafana/faro-*` imports outside
   `packages/telemetry`, so Faro enters the workspace in exactly one place.
4. **Catalog.** Pin the Faro packages in a `pnpm-workspace.yaml` catalog, as `start`/`react` do.

## Not doing

- Faro direct to Alloy. It needs a publicly reachable collector, CORS, and an API key in the
  bundle, and it makes `meta.user` page-asserted — three things this repo's BFF posture exists
  to prevent.
- Replacing `logger` with Faro. The two carry different signals; the logger's server-stamped
  identity and validated low-cardinality event names are not something Faro offers.
