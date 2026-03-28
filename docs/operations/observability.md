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

The `ModuleEnricher` (at `src/Wallow.Api/Logging/ModuleEnricher.cs`) automatically tags logs with the module name extracted from the `SourceContext` namespace using the pattern `{NamespacePrefix}.{ModuleName}.*`. The prefix defaults to `"Wallow"` but can be overridden via `Logging:NamespacePrefix` configuration, supporting fork customization.

This produces log output like:
```
[14:32:15 INF] [Billing] Invoice INV-2026-001 created for tenant acme-corp
[14:32:16 INF] [Notifications] Sending invoice notification to customer@example.com
[14:32:17 INF] [Notifications] Push notification queued for user usr_abc123
```

### Request Logging

Serilog request logging is configured in `Program.cs` with a custom message template (`{RequestPath} in {Elapsed:0.0000} ms`) and enrichment for `RequestHost` and `UserAgent`.

### Writing Effective Log Messages

Wallow uses the `[LoggerMessage]` source generator pattern for all logging. Never call `_logger.LogInformation(...)` directly. See `.claude/rules/LOGGING.md` for the full pattern.

```csharp
// Define as private partial void methods at the bottom of a partial class
[LoggerMessage(Level = LogLevel.Information, Message = "Invoice {InvoiceId} created for tenant {TenantId}")]
private partial void LogInvoiceCreated(Guid invoiceId, Guid tenantId);

[LoggerMessage(Level = LogLevel.Warning, Message = "Payment retry {Attempt} of {MaxAttempts} for invoice {InvoiceId}")]
private partial void LogPaymentRetry(int attempt, int maxAttempts, Guid invoiceId);
```

## OpenTelemetry Tracing

OpenTelemetry provides distributed tracing across HTTP requests, database operations, and message processing.

### Configuration

Tracing is configured via `AddObservability()` in `src/Wallow.Api/Extensions/ServiceCollectionExtensions.cs`. It reads `OpenTelemetry:ServiceName` and `OpenTelemetry:OtlpGrpcEndpoint` from configuration. In non-Development environments, `OtlpGrpcEndpoint` is required.

Key aspects of the tracing configuration:
- **Sampling**: Uses `ParentBasedSampler` with `TraceIdRatioBasedSampler`. The ratio defaults to 1.0 (100%) in Development and 0.1 (10%) in production, configurable via `Observability:TraceSamplingRatio`.
- **Instrumentation**: ASP.NET Core, EF Core, HttpClient, and Wolverine are all auto-instrumented.
- **Source registration**: Uses `.AddSource("Wallow.*")` wildcard to capture all module activity sources.
- **Export**: OTLP gRPC exporter sends to the configured endpoint.

### Auto-Instrumentation

The following are automatically instrumented:

| Component | What's Traced |
|-----------|--------------|
| **ASP.NET Core** | HTTP requests, responses, status codes, route patterns |
| **Entity Framework Core** | Database queries, execution time, SQL commands |
| **HttpClient** | Outbound HTTP requests to external services |
| **Wolverine** | Message handling, command/query execution |

### Trace Propagation

Traces are automatically propagated through:

- **HTTP Headers**: W3C Trace Context (`traceparent`, `tracestate`)
- **Wolverine Messages**: Wolverine propagates trace context through in-memory message headers
- **SignalR**: Trace context flows through WebSocket connections

### Adding Custom Spans

Use `Diagnostics.CreateActivitySource()` from `Wallow.Shared.Kernel` to create module-scoped activity sources. All `Wallow.*` sources are captured automatically by the wildcard registration in `AddObservability()`.

```csharp
using System.Diagnostics;
using Wallow.Shared.Kernel;

public class PaymentService
{
    private static readonly ActivitySource ActivitySource =
        Diagnostics.CreateActivitySource("Billing"); // creates "Wallow.Billing"

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        using Activity? activity = ActivitySource.StartActivity("ProcessPayment");
        activity?.SetTag("payment.amount", request.Amount);
        activity?.SetTag("payment.currency", request.Currency);

        try
        {
            PaymentResult result = await _gateway.ChargeAsync(request);
            activity?.SetTag("payment.status", result.Status);
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

The `GlobalExceptionHandler` (at `src/Wallow.Api/Middleware/GlobalExceptionHandler.cs`) includes trace IDs in all error responses. It extracts the trace ID from the current `Activity` or falls back to `HttpContext.TraceIdentifier`, then includes it in the Problem Details response:


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

The following metrics are automatically collected:

**ASP.NET Core Metrics:**
- `http.server.request.duration` - Request duration histogram
- `http.server.active_requests` - Current active requests
- `http.server.request.body.size` - Request body size

**Runtime Metrics:**
- `process.runtime.dotnet.gc.collections.count` - GC collection count
- `process.runtime.dotnet.gc.heap.size` - GC heap size
- `process.runtime.dotnet.threadpool.threads.count` - Thread pool size
- `process.runtime.dotnet.assemblies.count` - Loaded assemblies

### Custom Metrics

Wallow provides a shared `Meter` for custom metrics:

The `Diagnostics` class (at `src/Shared/Wallow.Shared.Kernel/Diagnostics.cs`) provides:
- `Diagnostics.Meter` -- the shared `Meter` instance (name defaults to `"Wallow"`)
- `Diagnostics.CreateActivitySource(moduleName)` -- creates `"Wallow.{moduleName}"`
- `Diagnostics.CreateMeter(moduleName)` -- creates a module-scoped meter
- `Diagnostics.Initialize(prefix)` -- allows forks to change the prefix from `"Wallow"`

Create custom metrics:

```csharp
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;

public class InvoiceService
{
    private static readonly Counter<long> InvoicesCreated =
        Diagnostics.Meter.CreateCounter<long>(
            "wallow.invoices.created",
            description: "Number of invoices created");

    private static readonly Histogram<double> InvoiceTotal =
        Diagnostics.Meter.CreateHistogram<double>(
            "wallow.invoices.total",
            unit: "USD",
            description: "Invoice total amounts");

    public async Task<Invoice> CreateInvoiceAsync(CreateInvoiceCommand command)
    {
        var invoice = // ... create invoice

        InvoicesCreated.Add(1,
            new KeyValuePair<string, object?>("tenant", command.TenantId),
            new KeyValuePair<string, object?>("currency", command.Currency));

        InvoiceTotal.Record(invoice.Total,
            new KeyValuePair<string, object?>("tenant", command.TenantId));

        return invoice;
    }
}
```

The `Wallow` meter and all module-scoped meters are registered in the OpenTelemetry configuration via `.AddMeter("Wallow")` and `.AddMeter("Wallow.*")`.

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

Wallow includes pre-configured dashboards in `docker/grafana/dashboards/`:

| Dashboard | Description |
|-----------|-------------|
| **ASP.NET Core OTel** | HTTP request metrics, latencies, error rates |
| **.NET Runtime** | GC metrics, thread pool, memory usage |
| **Module Overview** | Per-module metrics overview |
| **Billing Dashboard** | Billing-specific metrics |
| **Messaging Dashboard** | Message processing metrics |
| **Sales Dashboard** | Sales-related metrics |
| **SLO Monitoring** | Service level objective tracking |
| **Multi-Region Overview** | Cross-region metrics |

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
# All logs from Billing module
{service_name="Wallow"} | json | Module="Billing"

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

Configure OTLP endpoints via environment variables:

```bash
OpenTelemetry__EnableLogging=true
OpenTelemetry__ServiceName=Wallow
OpenTelemetry__OtlpEndpoint=https://otel-collector.yourcompany.com
OpenTelemetry__OtlpGrpcEndpoint=https://otel-collector.yourcompany.com:4317
```

`OtlpGrpcEndpoint` is required in non-Development environments. The API will throw on startup if it is not configured.

`OtlpEndpoint` is used for Serilog log shipping (HTTP) when `EnableLogging` is `true`. `OtlpGrpcEndpoint` is used for traces and metrics.

### Performance Considerations

1. **Sampling**: Trace sampling is already configured in `AddObservability()`. It defaults to 10% in production. Override via `Observability:TraceSamplingRatio` configuration key.

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

Wolverine messages carry trace context automatically. To trace a message:

1. Find the trace ID in the producer logs
2. Search for that trace in Tempo
3. The trace shows: HTTP request -> Message publish -> Message consume -> Handler execution

### Common Debugging Scenarios

**Slow API Response:**
1. Get trace ID from response headers or logs
2. View trace in Tempo
3. Check span durations for:
   - Database queries (EF Core spans)
   - External HTTP calls (HttpClient spans)
   - Message handling (Wolverine spans)

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

- **Business-critical operations**: Payment processing, order completion
- **External integrations**: Third-party API calls, webhook processing
- **Long-running operations**: Batch processing, data migrations
- **Resource-intensive operations**: Report generation, file processing

### Naming Conventions

**Activity Sources (Tracing):**
```csharp
// Use Diagnostics.CreateActivitySource() — produces "Wallow.{ModuleName}"
private static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Billing");
private static readonly ActivitySource ActivitySource = Diagnostics.CreateActivitySource("Storage");
```

**Metrics:**
```csharp
// Format: wallow.{module}.{metric_name}
// Use snake_case, be descriptive
Diagnostics.Meter.CreateCounter<long>("wallow.billing.invoices_created");
Diagnostics.Meter.CreateHistogram<double>("wallow.billing.payment_duration_seconds");
Diagnostics.Meter.CreateCounter<long>("wallow.storage.files_uploaded");
```

**Log Message Properties:**
Use PascalCase for property names. Include IDs, counts, durations, and statuses.

```csharp
[LoggerMessage(Level = LogLevel.Information,
    Message = "Order {OrderId} processed in {DurationMs}ms with {ItemCount} items. Status: {Status}")]
private partial void LogOrderProcessed(Guid orderId, double durationMs, int itemCount, string status);
```

### Complete Example: Adding Observability to a Service

This example shows the three pillars (traces, metrics, logging) combined in a single service. Note the use of `[LoggerMessage]` source generator for logging and `Diagnostics.CreateActivitySource()` for tracing.

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;

public sealed partial class PaymentService(
    ILogger<PaymentService> logger,
    IPaymentRepository repository)
{
    private static readonly ActivitySource ActivitySource =
        Diagnostics.CreateActivitySource("Billing");

    private static readonly Counter<long> PaymentsProcessed =
        Diagnostics.Meter.CreateCounter<long>(
            "wallow.billing.payments_processed_total",
            description: "Number of payments processed");

    public async Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request, CancellationToken ct)
    {
        using Activity? activity = ActivitySource.StartActivity("ProcessPayment");
        activity?.SetTag("payment.invoice_id", request.InvoiceId.ToString());
        activity?.SetTag("payment.amount", request.Amount);

        try
        {
            PaymentResult result = await repository.ProcessAsync(request, ct);
            activity?.SetTag("payment.success", result.IsSuccess);

            if (result.IsSuccess)
            {
                PaymentsProcessed.Add(1,
                    new KeyValuePair<string, object?>("method", request.Method));
                LogPaymentProcessed(result.PaymentId, request.InvoiceId);
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            LogPaymentError(request.InvoiceId, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Payment {PaymentId} processed for invoice {InvoiceId}")]
    private partial void LogPaymentProcessed(Guid paymentId, Guid invoiceId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing payment for invoice {InvoiceId}")]
    private partial void LogPaymentError(Guid invoiceId, Exception ex);
}
```

Activity sources using `Diagnostics.CreateActivitySource()` are automatically captured by the `Wallow.*` wildcard in the tracing configuration.

## Troubleshooting

### Logs Not Appearing in Loki

1. Verify `OpenTelemetry:EnableLogging` is `true`
2. Check the OTLP endpoint is reachable
3. Verify Grafana LGTM container is running: `docker ps | grep lgtm`
4. Check container logs: `docker logs wallow-grafana-lgtm`

### Traces Not Appearing in Tempo

1. Verify the OTLP gRPC endpoint (port 4317) is correct
2. Check if the service is generating traces (enable debug logging)
3. Ensure ActivitySources are registered in OpenTelemetry config

### Metrics Not Appearing in Prometheus

1. Verify the OTLP endpoint configuration
2. Check if the Meter is registered: `.AddMeter("Wallow")`
3. Ensure metrics are being recorded (add temporary logging)

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

// Shared meter (already registered with OpenTelemetry as "Wallow")
Diagnostics.Meter

// Module-scoped activity source for tracing
Diagnostics.CreateActivitySource("MyModule")  // creates "Wallow.MyModule"
```

### Naming Convention

| Type | Format | Example |
|------|--------|---------|
| **Metrics** | `wallow.{module}.{metric_name}` | `wallow.billing.invoices_created_total` |
| **Activity Sources** | `Wallow.{Module}` | `Wallow.Billing` |

Use snake_case for metric names. Append `_total` to counters and include a `unit` parameter on histograms where applicable.

### Adding Metrics to a Handler or Service

Declare instruments as `static readonly` fields, then record values inside your handler:

```csharp
// From Billing — InvoiceCreatedDomainEventHandler.cs
private static readonly Counter<long> InvoicesCreatedCounter =
    Diagnostics.Meter.CreateCounter<long>("wallow.billing.invoices_created_total");

private static readonly Histogram<double> InvoiceAmountHistogram =
    Diagnostics.Meter.CreateHistogram<double>("wallow.billing.invoice_amount");

// Inside the handler method:
InvoicesCreatedCounter.Add(1,
    new KeyValuePair<string, object?>("status", status),
    new KeyValuePair<string, object?>("currency", currency));

InvoiceAmountHistogram.Record((double)domainEvent.TotalAmount,
    new KeyValuePair<string, object?>("status", status),
    new KeyValuePair<string, object?>("currency", currency));
```

```csharp
// From Storage — StorageModuleTelemetry.cs
private static readonly Counter<long> FilesUploaded =
    Diagnostics.Meter.CreateCounter<long>("wallow.storage.files_uploaded_total");

private static readonly Histogram<double> FileSizeBytes =
    Diagnostics.Meter.CreateHistogram<double>("wallow.storage.file_size_bytes");

// Inside handler methods:
FilesUploaded.Add(1);
FileSizeBytes.Record((double)file.SizeBytes);
```

### Adding Custom Traces

Use `Diagnostics.CreateActivitySource` for module-scoped tracing:

```csharp
private static readonly ActivitySource StorageActivitySource =
    Diagnostics.CreateActivitySource("Storage");

// Inside the method:
using var activity = StorageActivitySource.StartActivity("Storage.GetFiles");
activity?.SetTag("storage.tenant_id", query.TenantId.ToString());
// ... perform work ...
activity?.SetTag("storage.file_count", fileList.Count);
```

All `Wallow.*` activity sources are registered via a wildcard in `ServiceCollectionExtensions.cs`:

```csharp
.WithTracing(tracing => tracing
    .AddSource("Wallow.*")
    // ... other configuration
```

### Creating Grafana Dashboard Panels

With metrics flowing to Prometheus, create dashboard panels using PromQL:

| Panel | PromQL |
|-------|--------|
| Invoices created (rate) | `rate(wallow_billing_invoices_created_total[5m])` |
| Invoice amount P95 | `histogram_quantile(0.95, rate(wallow_billing_invoice_amount_bucket[5m]))` |
| Files uploaded per minute | `rate(wallow_storage_files_uploaded_total[1m]) * 60` |
| File size distribution | `histogram_quantile(0.5, rate(wallow_storage_file_size_bytes_bucket[5m]))` |

> **Note:** Prometheus converts dots to underscores, so `wallow.billing.invoices_created_total` becomes `wallow_billing_invoices_created_total`.

> **Current modules:** Identity, Billing, Storage, Notifications, Messaging, Announcements, Inquiries, ApiKeys, Branding. Use these module names in your metrics and traces.

To add a panel: **Grafana** > **Dashboards** > **New Dashboard** > **Add visualization** > select **Prometheus** data source > enter the PromQL query > choose an appropriate visualization (Stat for counters, Time Series for rates, Heatmap for histograms).

## Related Documentation

- [Developer Guide](../getting-started/developer-guide.md) - General development practices
- [Deployment Guide](deployment.md) - Production deployment including observability
- [Messaging Guide](../architecture/messaging.md) - Message tracing with Wolverine
