# Request Correlation

A user reports "it failed when I clicked save". This guide turns that into a specific
backend request you can open in Grafana.

Every error the BFF surfaces to the browser is a `WallowError` carrying two correlation
members. They come from different places and answer different questions:

| Member      | Minted by                | Answers                                            |
| ----------- | ------------------------ | -------------------------------------------------- |
| `requestId` | the BFF (`x-request-id`) | *which request* — including ones that never reached the API |
| `traceId`   | the API (OpenTelemetry)  | *which trace* — the span tree Tempo exports        |

Quote both in a bug report. `traceId` takes you straight to a trace; `requestId` is the one
that still exists when there is no trace to take you to.

## The `x-request-id` header

The BFF proxy (`createApiProxy` in `@bc-solutions-coder/sdk/server`) mints a request id
before it does anything else with an incoming request:

- If the caller sent an `x-request-id` the SDK considers usable, it is kept, so an upstream
  gateway's own correlation id survives the trip.
- Otherwise the BFF **generates** one (a UUID).
- A caller-supplied id that fails validation — too long, or carrying whitespace, control
  characters, CR/LF, or markup — is replaced rather than echoed. An id is copied into an
  outbound header and a log line, so a forged one is a header- and log-injection primitive.

The id then travels in both directions:

- **Upstream**: set on the request forwarded to the API. A reactive-401 replay reuses the
  same id, so one logical request stays one correlation key rather than splitting in two.
- **Downstream**: set on the response the browser receives — on *every* exit, including the
  ones the proxy answers itself (a rejected path, an unauthenticated session, a failed CSRF
  check). Those never reach the API, so the request id is the only thing that names them.

Where the BFF synthesizes the response body itself (a CSRF rejection, or a `503` because the
API was unreachable), the id also appears as a `requestId` member inside the problem details
body — such a body has no upstream `traceId` to fall back on.

## Reading the ids in the browser

Both members are on the error every SDK operation rejects with:

```ts
import { isWallowError } from "@bc-solutions-coder/sdk";

try {
  await inquiriesCreate({ client, body });
} catch (error) {
  if (isWallowError(error)) {
    console.error(error.code, { requestId: error.requestId, traceId: error.traceId });
  }
}
```

`requestId` is read off the response header — the parsed body never carries it — and
`traceId` out of the API's problem details body, which the shared problem customizer
(`ProblemContract.Customize`) stamps on every error response:

```json
{
  "type": "about:blank",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Something went wrong. Try again later.",
  "code": "Server.Error",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

It appears at the **top level**, not nested under `extensions`. The customizer sets
`ProblemDetails.Extensions["traceId"]`, and ASP.NET Core carries `Extensions` as
`[JsonExtensionData]`, so it serializes flattened into the object. The SDK reads
`extensions.traceId` first and falls back to the flattened member, so it works against either
shape — which is why a fork whose problem-details serializer nests extensions needs no SDK change.

Surface both in whatever the user can copy — an error boundary, a toast, a support form.
An id nobody can read is an id nobody will quote.

## Finding the trace in Grafana

Local Grafana runs at <http://localhost:3001> (admin password from `GF_ADMIN_PASSWORD` in
`docker/.env`). See [Observability](observability.md) for the stack as a whole.

**With a `traceId`** — the direct route:

1. Open <http://localhost:3001> and go to **Explore**.
2. Select the **Tempo** data source.
3. Search by **Trace ID** and paste the `traceId` verbatim.
4. The span tree is the whole request: the endpoint, the handler, every database call and
   outbound dependency, with timings and the exception that ended it.

To find the log lines for the same request, switch to the **Loki** data source:

```logql
{service_name="Wallow"} | json | TraceId="00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
```

**With only a `requestId`** — the failure never produced a backend trace, which is itself
the finding. A request id without a trace id means the request died in the BFF: the CSRF
check rejected it, the session could not authenticate, or the API was unreachable. Look in
the **BFF process logs**, not Tempo; the proxy logs the forwarded headers, the status, and
the error code under `wallow-bff: forward failed`. The request id is in those headers.

If a request id *is* accompanied by a trace id, prefer the trace id — it is what Tempo
indexes.

## Extending correlation on the backend

Wallow's API does not index `x-request-id` itself: the header arrives, but nothing reads it,
and `traceId` is the id OTel exports traces under. A fork that wants to search Tempo by the
BFF's request id can add middleware that reads the header and tags the current activity, for
example `Activity.Current?.SetTag("wallow.request_id", requestId)`, which makes it queryable
in TraceQL:

```
{ span.wallow.request_id = "3f2504e0-4f89-11d3-9a0c-0305e82c3301" }
```

That is a fork-level choice, not a requirement — the `traceId` route above needs no backend
change at all.

## See also

- [Observability](observability.md) — the Serilog/OpenTelemetry/Grafana LGTM stack
- [Troubleshooting](troubleshooting.md) — common failures and their symptoms
- [BFF Pattern](../integrations/bff-pattern.md) — how the tunnel is put together
