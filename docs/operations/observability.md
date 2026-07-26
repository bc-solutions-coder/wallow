# Observability Guide

This guide covers logging, tracing, and metrics in Wallow using the observability stack: Serilog, OpenTelemetry, and Grafana LGTM.

## Overview

Wallow uses a modern observability stack that provides three pillars of observability:

| Pillar | Technology | Backend |
|--------|------------|---------|
| **Logging** | Serilog | Loki (via OTLP) |
| **Tracing** | OpenTelemetry | Tempo |
| **Metrics** | OpenTelemetry | Prometheus |

All telemetry is exported via OpenTelemetry Protocol (OTLP) to a Grafana LGTM (Loki, Grafana, Tempo, Mimir/Prometheus) stack, providing unified observability with correlated logs, traces, and metrics.

### Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  Wallow API                                                        │
│  ┌─────────────┐  ┌─────────────────┐  ┌────────────────────────┐  │
│  │  Serilog    │  │  OpenTelemetry  │  │  OpenTelemetry         │  │
│  │  (Logging)  │  │  (Tracing)      │  │  (Metrics)             │  │
│  └──────┬──────┘  └────────┬────────┘  └───────────┬────────────┘  │
│         │                  │                       │                │
│         └──────────────────┴───────────────────────┘                │
│                            │                                        │
│                      OTLP Export                                    │
└────────────────────────────┼────────────────────────────────────────┘
                             │
                             ▼
              ┌──────────────────────────┐
              │  Grafana Alloy           │
              │  (OTLP Collector)        │
              │  gRPC :4317 / HTTP :4318 │
              └────────────┬─────────────┘
                           │
                           ▼
              ┌──────────────────────────────┐
              │  Grafana LGTM                │
              │  ┌────────┐ ┌─────┐ ┌─────┐  │
              │  │  Loki  │ │Tempo│ │Prom │  │
              │  │ (Logs) │ │(Tr.)│ │(Met)│  │
              │  └────────┘ └─────┘ └─────┘  │
              │           │                  │
              │     ┌─────▼──────┐           │
              │     │  Grafana   │           │
              │     │  UI :3001  │           │
              │     └────────────┘           │
              └──────────────────────────────┘
```

## Structured Logging with Serilog

Wallow uses Serilog for structured logging with rich context enrichment.

### Configuration

Serilog is configured in `Program.cs` with:

- **Enrichment**: `FromLogContext()`, `ModuleEnricher` (extracts module name from namespace), `PiiDestructuringPolicy`, and an `Application` property from `Logging:NamespacePrefix`.
- **Console output**: Uses `ExpressionTemplate` with color-coded output showing module, tenant, user, client, HTTP method/status, and request protocol.
- **OTLP export**: When `OpenTelemetry:EnableLogging` is `true`, logs are shipped via OTLP HTTP to `{OtlpEndpoint}/v1/logs` with `service.name`, `service.namespace`, and `deployment.environment` resource attributes.

### Log Level Configuration

Log levels are configured in `appsettings.json` (base) and `appsettings.Development.json` (overrides for local development). The base configuration uses Serilog overrides:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Wolverine": "Warning"
      }
    }
  }
}
```

In Development, the `Logging:LogLevel:Default` is set to `Debug` and OpenTelemetry logging is enabled by default. For production, configure log levels via environment variables (e.g., `Serilog__MinimumLevel__Default=Warning`).

### Module Enricher

The `ModuleEnricher` (at `api/src/Wallow.Api/Logging/ModuleEnricher.cs`) automatically tags logs with the module name extracted from the `SourceContext` namespace using the pattern `{NamespacePrefix}.{ModuleName}.*`. The prefix defaults to `"Wallow"` but can be overridden via `Logging:NamespacePrefix` configuration, supporting fork customization.

This produces log output like:
```
[14:32:15 INF] [Storage] File document.pdf uploaded for tenant acme-corp
[14:32:16 INF] [Notifications] Sending upload notification to customer@example.com
[14:32:17 INF] [Notifications] Push notification queued for user usr_abc123
```

### Request Logging

Serilog request logging is configured in `Program.cs` with a custom message template (`{RequestPath} in {Elapsed:0.0000} ms`) and enrichment for `RequestHost` and `UserAgent`.

### Writing Effective Log Messages

Wallow uses the `[LoggerMessage]` source generator pattern for all logging. Never call `logger.LogInformation(...)` or the other `ILogger` extension methods directly — they allocate on every call and trigger the CA1848/CA1873 analyzers.

The pattern:

- Mark the class `partial` so the generator can emit the logging implementations.
- Inject `ILogger<T>` via the primary constructor.
- Add `using Microsoft.Extensions.Logging;`.
- Define `private partial void` methods decorated with `[LoggerMessage]` at the bottom of the class.

```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "Something happened for {EntityId} by user {UserId}")]
private partial void LogSomethingHappened(Guid entityId, string? userId);
```

Applied to real messages:

```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "File {FileId} uploaded for tenant {TenantId}")]
private partial void LogFileUploaded(Guid fileId, Guid tenantId);

[LoggerMessage(Level = LogLevel.Warning, Message = "Upload retry {Attempt} of {MaxAttempts} for file {FileId}")]
private partial void LogUploadRetry(int attempt, int maxAttempts, Guid fileId);
```

## OpenTelemetry Tracing

OpenTelemetry provides distributed tracing across HTTP requests, database operations, and message processing.

### Configuration

Tracing and metrics are configured in one place: `ConfigureOpenTelemetry` in
`api/src/Wallow.ServiceDefaults/Extensions.cs`, called from `AddServiceDefaults()` in the API's
`Program.cs`.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddProcessInstrumentation()
            .AddRuntimeInstrumentation();
    });
```

Key aspects:
- **Export**: `UseOtlpExporter()` is added only when the standard `OTEL_EXPORTER_OTLP_ENDPOINT`
  environment variable is set. Aspire sets it automatically for `pnpm backend`; the production
  compose file sets it to `http://alloy:4317`. With no endpoint configured, traces and metrics
  are collected in-process and never exported.
- **Sampling**: `ConfigureOpenTelemetry` installs a `ParentBased(TraceIdRatioBased(ratio))` sampler.
  The ratio comes from `OpenTelemetry:TraceSamplingRatio` and defaults to `1.0`, so local
  development keeps full-fidelity traces. Wrapping the ratio sampler in `ParentBased` means a trace
  already sampled upstream stays sampled through our services, so a request is never traced in
  fragments.
- **Source registration**: the SDK collects only what is explicitly registered. There is currently
  **no `AddSource(...)` call**, so the `Wallow.*` activity sources described below are created but
  their spans are not exported until a source is registered. See
  [Exporting custom instruments](#exporting-custom-instruments).

### Auto-Instrumentation

The following are automatically instrumented:

| Component | What's Traced |
|-----------|--------------|
| **ASP.NET Core** | HTTP requests, responses, status codes, route patterns |
| **HttpClient** | Outbound HTTP requests to external services |

Entity Framework Core and Wolverine instrumentation are **not** registered. Database calls appear
inside the ASP.NET Core server span rather than as their own spans, and Wolverine message handling
produces no dedicated spans (see [Message flow](#tracing-message-flow)).

### Trace Propagation

Traces propagate across HTTP boundaries via W3C Trace Context (`traceparent`, `tracestate`), which
the ASP.NET Core and HttpClient instrumentation handle on both ends.

Wolverine messaging is in-memory, so there is no wire format to propagate. What Wallow does instead
is enrich whatever activity is already current when a message is handled:
`WolverineModuleTaggingMiddleware` (at
`api/src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/WolverineModuleTaggingMiddleware.cs`)
tags `Activity.Current` with `wallow.module` (derived from the message's namespace) and
`wallow.tenant_id` (from the `X-Tenant-Id` envelope header). When a message is handled inline on the
request thread those tags land on the ASP.NET Core server span; when it is handled off a background
queue there may be no current activity, and the tags are simply skipped.

### Adding Custom Spans

Use `Diagnostics.CreateActivitySource()` from `Wallow.Shared.Kernel` to create module-scoped activity
sources. Creating the source is only half the job — register it with the tracer provider as well, or
its spans go nowhere (see [Exporting custom instruments](#exporting-custom-instruments)).

```csharp
using System.Diagnostics;
using Wallow.Shared.Kernel;

public class NotificationDispatcher
{
    private static readonly ActivitySource ActivitySource =
        Diagnostics.CreateActivitySource("Notifications"); // creates "Wallow.Notifications"

    public async Task<DispatchResult> DispatchAsync(NotificationRequest request)
    {
        using Activity? activity = ActivitySource.StartActivity("DispatchNotification");
        activity?.SetTag("notification.channel", request.Channel);
        activity?.SetTag("notification.recipient", request.RecipientId);

        try
        {
            DispatchResult result = await _sender.SendAsync(request);
            activity?.SetTag("notification.status", result.Status);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

### Correlating Errors with Traces

The `GlobalExceptionHandler` (at `api/src/Wallow.Api/Middleware/GlobalExceptionHandler.cs`) includes trace IDs in all error responses. It extracts the trace ID from the current `Activity` or falls back to `HttpContext.TraceIdentifier`, then includes it in the Problem Details response:


```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "traceId": "00-abc123def456...-01"
}
```

## Metrics

OpenTelemetry collects runtime and application metrics.

### Built-in Metrics

These come from the four metrics instrumentations registered in `ConfigureOpenTelemetry` (ASP.NET
Core, HttpClient, process, and runtime) and are the only metrics the pre-configured dashboards query.
Names are shown in OpenTelemetry form; Prometheus replaces the dots with underscores and appends a
unit and type suffix, so `dotnet.gc.collections` is queried as `dotnet_gc_collections_total`.

**ASP.NET Core / HttpClient:**
- `http.server.request.duration` — server request duration histogram
- `http.server.active_requests` — in-flight server requests
- `http.client.request.duration` — outbound HttpClient request duration

**Runtime and process:**
- `dotnet.gc.collections` — GC collections by generation
- `dotnet.gc.pause.time` — total GC pause time
- `dotnet.thread_pool.thread.count` — thread pool size
- `dotnet.exceptions` — exceptions thrown
- `dotnet.assembly.count` — loaded assemblies
- `dotnet.process.cpu.time` — process CPU time

### Custom Instruments in the Codebase

Instrumentation primitives live in the static `Diagnostics` class at
`api/src/Shared/Wallow.Shared.Kernel/Diagnostics.cs`:

- `Diagnostics.Meter` — the shared `Meter` (name defaults to `"Wallow"`)
- `Diagnostics.CreateMeter(moduleName)` — creates `"Wallow.{moduleName}"`
- `Diagnostics.CreateActivitySource(moduleName)` — creates `"Wallow.{moduleName}"`
- `Diagnostics.Initialize(prefix)` — lets a fork swap `"Wallow"` for its own prefix; must be called
  before any telemetry is emitted, and it rebuilds the messaging instruments under the new prefix

Every custom instrument that exists in `api/src` today:

| Instrument | Kind | Meter | Recorded by |
|-----------|------|-------|-------------|
| `wallow.messaging.messages_total` | Counter | `Wallow.Messaging` | `WolverineModuleTaggingMiddleware` |
| `wallow.messaging.message_duration` | Histogram (ms) | `Wallow.Messaging` | `WolverineModuleTaggingMiddleware` |
| `wallow.messaging.domain_events_published_total` | Counter | `Wallow.Messaging` | `WolverineModuleTaggingMiddleware` |
| `wallow.cache.hits_total` | Counter | `Wallow.Cache` | `InstrumentedDistributedCache` |
| `wallow.cache.misses_total` | Counter | `Wallow.Cache` | `InstrumentedDistributedCache` |
| `wallow.requests_authenticated_total` | Counter | `Wallow.Identity` | `IdentityModuleTelemetry` |

The messaging trio is the richest of these. `WolverineModuleTaggingMiddleware` stamps a start
timestamp into the envelope headers on the way in and, on the way out, records the count and duration
with three tags — `message_type`, `module`, and `status` — plus a separate counter for any message
implementing `IDomainEvent`, tagged with `event_type`.

### Exporting Custom Instruments

`ConfigureOpenTelemetry` registers instrumentation libraries but **no meters or activity sources**,
and the OpenTelemetry SDK only collects from instruments it has been told about. The instruments in
the table above are therefore recorded in-process but never exported — they will not appear in
Prometheus until the meters are registered.

To export them, add the meters (and any activity sources you care about) in
`api/src/Wallow.ServiceDefaults/Extensions.cs`:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Wallow.Identity")
            .AddSource("Wallow.Notifications.Email");
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddProcessInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Wallow.Messaging")
            .AddMeter("Wallow.Cache")
            .AddMeter("Wallow.Identity");
    });
```

Meter and source names must match exactly — `AddMeter` and `AddSource` accept a `*` wildcard suffix,
so `AddMeter("Wallow.*")` covers every module-scoped meter at once, but note that it does **not**
match the bare `"Wallow"` meter returned by `Diagnostics.Meter`. Register that one by name if you use
it. A fork that calls `Diagnostics.Initialize("Contoso")` must register `"Contoso.*"` instead.

## Local Development

### Starting the Observability Stack

The Grafana LGTM container provides a complete observability backend:

```bash
cd docker
docker compose up -d grafana-lgtm
```

This starts the Grafana LGTM container. The full observability stack also requires the Alloy collector (`alloy` service), which starts automatically as a dependency.

- **Grafana** on http://localhost:3001 (password from `GF_ADMIN_PASSWORD` in `docker/.env`)
- **OTLP gRPC receiver** on port 4317 (via Alloy)
- **OTLP HTTP receiver** on port 4318 (via Alloy)
- **Loki** for logs
- **Tempo** for traces
- **Prometheus** for metrics

### Configuration for Local Development

OpenTelemetry is already enabled in `appsettings.Development.json` with endpoints pointing to `localhost:4317` (gRPC) and `localhost:4318` (HTTP). No additional configuration is needed.

### Accessing Grafana

1. Open http://localhost:3001
2. Login with the password from `docker/.env` (`GF_ADMIN_PASSWORD`)
3. Navigate to **Explore** to query data sources:
   - **Loki** - Log queries using LogQL
   - **Tempo** - Trace searches by trace ID or service
   - **Prometheus** - Metric queries using PromQL

### Pre-configured Dashboards

Wallow includes four pre-configured dashboards in `docker/grafana/dashboards/`, mounted into the
`grafana-lgtm` container by `docker/grafana/provisioning/dashboards/dashboards.yml`:

| File | Dashboard | What it shows |
|------|-----------|---------------|
| `aspnetcore-otel.json` | ASP.NET Core OTel | Request rate and duration, error rate, active connections, unhandled exceptions, HTTP protocol and scheme breakdowns |
| `dotnet-runtime.json` | .NET Runtime | GC collections, pause time, heap size and fragmentation, thread pool, JIT, CPU, loaded assemblies |
| `module-overview.json` | Module Overview | Request rate, 5xx rate, and P95 latency grouped per module, plus a recent-traces panel |
| `slo-monitoring.json` | SLO Monitoring | Availability and latency objectives (P99 < 500ms API, P95 < 2000ms transactions, P99 < 1000ms SSO login) |

All four query only the built-in ASP.NET Core and .NET runtime metrics — none depends on a custom
Wallow instrument. Module Overview in particular does not read the `wallow.module` span tag; it
derives its `module` label with a Prometheus `label_replace` over the `http_route` label of
`http_server_request_duration_seconds`, matching `/v[0-9]+/([^/]+)/.*`. Only routes shaped like
`/v1/{module}/...` are attributed to a module; anything else falls outside the grouping.

Access dashboards at: **Dashboards** > **Browse** > Select dashboard

### Viewing Traces in Tempo

1. Go to **Explore** > Select **Tempo**
2. Search options:
   - **By Trace ID**: Paste a trace ID from logs or error responses
   - **By Service**: Filter by `service.name = "Wallow"`
   - **By Span Name**: Search for specific operations

Example TraceQL query:
```
{ resource.service.name = "Wallow" && span.http.status_code >= 500 }
```

### Exploring Logs in Loki

1. Go to **Explore** > Select **Loki**
2. Use LogQL queries:

```logql
# All logs from Storage module
{service_name="Wallow"} | json | Module="Storage"

# Errors only
{service_name="Wallow"} |= "error" or |= "Error"

# Logs for a specific trace
{service_name="Wallow"} | json | TraceId="00-abc123..."
```

### Correlating Logs and Traces

1. Find an error in logs with a trace ID
2. Copy the trace ID
3. Switch to Tempo and search by trace ID
4. View the full request flow with timing

## Production Configuration

### OTLP Endpoints

Logs and traces/metrics are exported through two independent paths, configured separately.

**Traces and metrics** use the standard OpenTelemetry environment variables, read by the SDK itself.
`UseOtlpExporter()` is only registered when `OTEL_EXPORTER_OTLP_ENDPOINT` has a value, so leaving it
unset disables trace and metric export entirely:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://alloy:4317
OTEL_SERVICE_NAME=wallow-api
```

`docker/docker-compose.production.yml` sets exactly these for the API and web containers, pointing at
the bundled Alloy collector.

**Logs** are shipped by Serilog, gated on the `OpenTelemetry` configuration section:

```bash
OpenTelemetry__EnableLogging=true
OpenTelemetry__ServiceName=Wallow
OpenTelemetry__OtlpEndpoint=https://otel-collector.yourcompany.com
```

When `EnableLogging` is `true`, Serilog posts to `{OtlpEndpoint}/v1/logs` over HTTP. `EnableLogging`
defaults to `false` in `appsettings.json` and is turned on in `appsettings.Development.json`.

> **Note:** the `OpenTelemetry:OtlpGrpcEndpoint` key still present in `appsettings.json` is not read
> by any code. Trace and metric export is driven solely by `OTEL_EXPORTER_OTLP_ENDPOINT`.

### Performance Considerations

1. **Sampling**: `OpenTelemetry:TraceSamplingRatio` sets the fraction of traces recorded and
   exported, through a `ParentBased`/`TraceIdRatioBased` sampler. It defaults to `1.0` (every trace)
   for local development; `docker-compose.production.yml` lowers it to `0.1` via
   `OpenTelemetry__TraceSamplingRatio`, tunable with the `OTEL_TRACE_SAMPLING_RATIO` variable in
   `.env.production`. On a busy deployment this is the first knob to reach for. A value that is
   unparseable or outside `[0,1]` falls back to full sampling or is clamped rather than failing
   startup, so an env-var typo cannot take the host down.

2. **Log Levels**: Use Warning or higher in production to reduce log volume.

3. **Batch Export**: OTLP exporters batch by default. The defaults are suitable for most workloads.

## Debugging with Traces

### Finding Slow Requests

1. In Grafana, go to **Explore** > **Tempo**
2. Query for slow requests:
   ```
   { resource.service.name = "Wallow" && duration > 1s }
   ```
3. Click on a trace to see the span breakdown
4. Identify slow spans (database queries, external calls)

### Tracing Message Flow

Wolverine handling produces **no spans of its own** — there is no Wolverine instrumentation and no
Wolverine `ActivitySource` registered. A message handled inline during a request is folded into that
request's ASP.NET Core server span, carrying the `wallow.module` and `wallow.tenant_id` tags that
`WolverineModuleTaggingMiddleware` adds; a message handled off a background queue produces no trace
at all.

The metrics are the reliable signal for message flow. `wallow.messaging.messages_total` and
`wallow.messaging.message_duration` are recorded for every message with `message_type`, `module`, and
`status` tags, and `wallow.messaging.domain_events_published_total` counts domain events by
`event_type`. Both require the `Wallow.Messaging` meter to be registered before they reach Prometheus
— see [Exporting custom instruments](#exporting-custom-instruments).

To follow a message end to end today:

1. Find the trace ID in the producer logs
2. Search for that trace in Tempo to see the originating HTTP request
3. Correlate handler-side logs by trace ID and message type, since the handler has no span

For the messaging model itself, see the [Messaging Guide](../architecture/messaging.md).

### Common Debugging Scenarios

**Slow API Response:**
1. Get trace ID from response headers or logs
2. View trace in Tempo
3. Check span durations for the ASP.NET Core server span and any HttpClient child spans. Database
   and message-handling time is not broken out — it is absorbed into the server span, so a server
   span that is slow with no slow HTTP child usually points at a query or a handler. Fall back to
   Serilog's request log and the EF Core logs to narrow it down.

**Failed Background Job:**
1. Check Hangfire dashboard for failed job ID
2. Search logs for that job ID
3. Use trace ID from logs to view full execution trace

**Message Processing Issues:**
1. Search logs for the message type
2. Check the Wolverine envelope tables for stuck or errored messages
3. Use trace ID from logs to view full execution trace

## Adding Observability to New Code

### When to Add Custom Telemetry

Add custom telemetry for:

- **Business-critical operations**: Inquiry submission, announcement publishing
- **External integrations**: Third-party API calls, webhook processing
- **Long-running operations**: Batch processing, data migrations
- **Resource-intensive operations**: Report generation, file processing

### Naming Conventions

**Activity Sources (Tracing):**
```csharp
// Use Diagnostics.CreateActivitySource() — produces "Wallow.{ModuleName}"
private static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Notifications");
private static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Storage");
```

**Metrics:**

Format is `wallow.{module}.{metric_name}` in snake_case, with `_total` on counters and an explicit
`unit` on histograms. The instruments already in the codebase follow it:

```csharp
// Wallow.Cache meter — InstrumentedDistributedCache
Meter cacheMeter = Diagnostics.CreateMeter("Cache");
Counter<long> hits = cacheMeter.CreateCounter<long>(
    "wallow.cache.hits_total",
    description: "Total number of cache hits");

// Wallow.Messaging meter — Diagnostics
Histogram<double> duration = messagingMeter.CreateHistogram<double>(
    "wallow.messaging.message_duration",
    unit: "ms",
    description: "Duration of message processing in milliseconds");
```

**Log Message Properties:**
Use PascalCase for property names. Include IDs, counts, durations, and statuses.

```csharp
[LoggerMessage(Level = LogLevel.Information,
    Message = "Order {OrderId} processed in {DurationMs}ms with {ItemCount} items. Status: {Status}")]
private partial void LogOrderProcessed(Guid orderId, double durationMs, int itemCount, string status);
```

### Complete Example: Adding Observability to a Service

This example shows the three pillars (traces, metrics, logging) combined in a single service. Note the
use of the `[LoggerMessage]` source generator for logging and `Diagnostics.CreateActivitySource()` for
tracing.

> The service and the `wallow.notifications.dispatched_total` counter below are **illustrative** — a
> sketch of code you would write, not something that exists in `api/src`. For the instruments that do
> exist, see [Custom instruments in the codebase](#custom-instruments-in-the-codebase).

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;

public sealed partial class NotificationDispatchService(
    ILogger<NotificationDispatchService> logger,
    INotificationRepository repository)
{
    private static readonly ActivitySource ActivitySource =
        Diagnostics.CreateActivitySource("Notifications");

    private static readonly Counter<long> NotificationsDispatched =
        Diagnostics.Meter.CreateCounter<long>(
            "wallow.notifications.dispatched_total",
            description: "Number of notifications dispatched");

    public async Task<DispatchResult> DispatchNotificationAsync(
        NotificationRequest request, CancellationToken ct)
    {
        using Activity? activity = ActivitySource.StartActivity("DispatchNotification");
        activity?.SetTag("notification.recipient_id", request.RecipientId.ToString());
        activity?.SetTag("notification.channel", request.Channel);

        try
        {
            DispatchResult result = await repository.DispatchAsync(request, ct);
            activity?.SetTag("notification.success", result.IsSuccess);

            if (result.IsSuccess)
            {
                NotificationsDispatched.Add(1,
                    new KeyValuePair<string, object?>("channel", request.Channel));
                LogNotificationDispatched(result.NotificationId, request.RecipientId);
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            LogNotificationError(request.RecipientId, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification {NotificationId} dispatched to recipient {RecipientId}")]
    private partial void LogNotificationDispatched(Guid notificationId, Guid recipientId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error dispatching notification for recipient {RecipientId}")]
    private partial void LogNotificationError(Guid recipientId, Exception ex);
}
```

Remember to register `Wallow.Notifications` with `AddSource` and the meter behind
`Diagnostics.Meter` with `AddMeter`, or neither the spans nor the counter will leave the process.

## Troubleshooting

### Logs Not Appearing in Loki

1. Verify `OpenTelemetry:EnableLogging` is `true`
2. Check the OTLP endpoint is reachable
3. Verify Grafana LGTM container is running: `docker ps | grep lgtm`
4. Check container logs: `docker logs wallow-grafana-lgtm`

### Traces Not Appearing in Tempo

1. Confirm `OTEL_EXPORTER_OTLP_ENDPOINT` is set — without it `UseOtlpExporter()` is never
   registered and nothing is exported, however healthy the collector is
2. Verify the collector is reachable on the gRPC port (4317)
3. For spans from a `Wallow.*` activity source, confirm the source is registered with `AddSource` in
   `ConfigureOpenTelemetry` — unregistered sources produce no spans at all

### Metrics Not Appearing in Prometheus

1. Confirm `OTEL_EXPORTER_OTLP_ENDPOINT` is set
2. Confirm the meter is registered with `AddMeter` — this is the usual cause for custom instruments,
   since `ConfigureOpenTelemetry` ships with no `AddMeter` calls. See
   [Exporting custom instruments](#exporting-custom-instruments)
3. Remember the name translation: `wallow.cache.hits_total` is queried as `wallow_cache_hits_total`
4. Ensure the code path recording the metric actually ran

### Correlation Issues

If logs and traces aren't correlating:

1. Ensure `Enrich.FromLogContext()` is configured
2. Verify the request has an active `Activity` (trace context)
3. Check that trace ID property is being logged

## Adding Custom Business Metrics

Wallow centralizes instrumentation primitives in `Wallow.Shared.Kernel.Diagnostics`. Use this to add metrics and traces without creating your own `Meter` or `ActivitySource` instances.

### Available Primitives

```csharp
using Wallow.Shared.Kernel;

// Shared meter, named "Wallow"
Diagnostics.Meter

// Module-scoped meter and activity source
Diagnostics.CreateMeter("MyModule")            // creates "Wallow.MyModule"
Diagnostics.CreateActivitySource("MyModule")   // creates "Wallow.MyModule"
```

Prefer a module-scoped meter over the shared `Diagnostics.Meter`: it keeps a fork's
`Diagnostics.Initialize(prefix)` rename coherent and lets a single `AddMeter("Wallow.*")` pick up
every module at once.

### Naming Convention

| Type | Format | Example (real) |
|------|--------|----------------|
| **Metrics** | `wallow.{module}.{metric_name}` | `wallow.messaging.messages_total` |
| **Activity Sources** | `Wallow.{Module}` | `Wallow.Identity` |

Use snake_case for metric names. Append `_total` to counters and include a `unit` parameter on
histograms where applicable.

### Adding Metrics to a Handler or Service

Declare instruments as `static readonly` fields on a module telemetry class, then record values where
the work happens. `IdentityModuleTelemetry` (at
`api/src/Modules/Identity/Wallow.Identity.Application/Telemetry/IdentityModuleTelemetry.cs`) is the
smallest real example:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;

namespace Wallow.Identity.Application.Telemetry;

public static class IdentityModuleTelemetry
{
    public static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Identity");
    private static readonly Meter _meter = Diagnostics.CreateMeter("Identity");

    public static readonly Counter<long> RequestsAuthenticatedTotal =
        _meter.CreateCounter<long>("wallow.requests_authenticated_total",
            description: "Total authenticated requests");
}
```

For a tagged counter and histogram recorded together, `WolverineModuleTaggingMiddleware` builds one
tag array and reuses it for both instruments:

```csharp
KeyValuePair<string, object?>[] tags =
[
    new("message_type", messageType.Name),
    new("module", module),
    new("status", status)
];

Diagnostics.MessagesTotal.Add(1, tags);

double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
Diagnostics.MessageDuration.Record(elapsedMs, tags);
```

Keep tag cardinality low. `message_type`, `module`, and `status` are all bounded sets; a tenant ID or
user ID is not, and will multiply your time series.

### Adding Custom Traces

Use `Diagnostics.CreateActivitySource` for module-scoped tracing. From `SmtpEmailProvider`, the one
place in the codebase that opens a custom span:

```csharp
using Activity? activity = EmailModuleTelemetry.ActivitySource.StartActivity();
```

`StartActivity()` with no arguments names the span after the calling method. Pass an explicit name
when you want something stable:

```csharp
private static readonly ActivitySource StorageActivitySource =
    Diagnostics.CreateActivitySource("Storage");

// Inside the method:
using Activity? activity = StorageActivitySource.StartActivity("Storage.GetFiles");
activity?.SetTag("storage.tenant_id", query.TenantId.ToString());
// ... perform work ...
activity?.SetTag("storage.file_count", fileList.Count);
```

`StartActivity` returns `null` when no listener is subscribed to the source — which is the case for
every `Wallow.*` source today, since none is registered with `AddSource`. The null-conditional calls
are not optional defensive style; they are the normal path until you register the source.

### Creating Grafana Dashboard Panels

Once the meters are registered and metrics are flowing to Prometheus, build panels with PromQL.
Prometheus converts dots to underscores, so `wallow.messaging.messages_total` is queried as
`wallow_messaging_messages_total`, and a histogram named `wallow.messaging.message_duration` exposes
`wallow_messaging_message_duration_bucket`, `_sum`, and `_count` series.

| Panel | PromQL |
|-------|--------|
| Message throughput by module | `sum by (module) (rate(wallow_messaging_messages_total[5m]))` |
| Message failure rate | `sum(rate(wallow_messaging_messages_total{status!="success"}[5m])) / sum(rate(wallow_messaging_messages_total[5m]))` |
| Message duration P95 | `histogram_quantile(0.95, sum by (le, module) (rate(wallow_messaging_message_duration_bucket[5m])))` |
| Domain events published | `sum by (event_type) (rate(wallow_messaging_domain_events_published_total[5m]))` |
| Cache hit ratio | `sum(rate(wallow_cache_hits_total[5m])) / (sum(rate(wallow_cache_hits_total[5m])) + sum(rate(wallow_cache_misses_total[5m])))` |
| Authenticated request rate | `rate(wallow_requests_authenticated_total[5m])` |

`wallow.messaging.message_duration` is recorded in **milliseconds** (`unit: "ms"`), so the P95 panel
above is already in milliseconds — do not multiply by 1000 the way the HTTP-duration panels in
`slo-monitoring.json` do, since those source metrics are in seconds.

To add a panel: **Grafana** > **Dashboards** > **New Dashboard** > **Add visualization** > select the
**Prometheus** data source > enter the PromQL query > choose a visualization (Stat for counters, Time
Series for rates, Heatmap for histograms).

> **Current modules:** Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding.
> Use these module names in your metrics and traces.

## Related Documentation

- [Developer Guide](../getting-started/developer-guide.md) - General development practices
- [Deployment Guide](deployment.md) - Production deployment including observability
- [Messaging Guide](../architecture/messaging.md) - Wolverine messaging model and module tagging
