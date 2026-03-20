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
              ┌──────────────────────────────┐
              │  Grafana LGTM                │
              │  ┌────────┐ ┌─────┐ ┌─────┐  │
              │  │  Loki  │ │Tempo│ │Prom │  │
              │  │ (Logs) │ │(Tr.)│ │(Met)│  │
              │  └────────┘ └─────┘ └─────┘  │
              │           │                  │
              │     ┌─────▼──────┐           │
              │     │  Grafana   │           │
              │     │    UI      │           │
              │     └────────────┘           │
              └──────────────────────────────┘
```

## Structured Logging with Serilog

Wallow uses Serilog for structured logging with rich context enrichment.

### Configuration

Serilog is configured in `Program.cs`:

```csharp
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.With<ModuleEnricher>()
        .Enrich.WithProperty("Application", "Wallow")
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{Module}] {Message:lj} {Properties:j}{NewLine}{Exception}");

    // OpenTelemetry logging - enable via configuration
    if (context.Configuration.GetValue("OpenTelemetry:EnableLogging", false))
    {
        var otlpEndpoint = context.Configuration["OpenTelemetry:OtlpEndpoint"]
            ?? "http://localhost:4318";
        var serviceName = context.Configuration["OpenTelemetry:ServiceName"]
            ?? "Wallow";

        configuration.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = otlpEndpoint + "/v1/logs";
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = serviceName,
                ["service.namespace"] = "Wallow",
                ["deployment.environment"] = context.HostingEnvironment.EnvironmentName
            };
        });
    }
});
```

### Log Level Configuration

Log levels are configured in `appsettings.json` and environment-specific files:

**Development (`appsettings.Development.json`):**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "OpenTelemetry": {
    "EnableLogging": true
  }
}
```

**Production (`appsettings.Production.json`):**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Error"
    }
  }
}
```

### Module Enricher

The `ModuleEnricher` automatically tags logs with the module name based on the source context namespace:

```csharp
// src/Wallow.Api/Logging/ModuleEnricher.cs
public class ModuleEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var module = "System";

        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            var contextValue = sourceContext.ToString().Trim('"');
            var parts = contextValue.Split('.');

            // Wallow.{X}.* -> Module = X
            if (parts.Length >= 2 && parts[0] == "Wallow")
            {
                module = parts[1];
            }
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Module", module));
    }
}
```

This produces log output like:
```
[14:32:15 INF] [Billing] Invoice INV-2026-001 created for tenant acme-corp
[14:32:16 INF] [Communications] Sending invoice notification to customer@example.com
[14:32:17 INF] [Communications] Push notification queued for user usr_abc123
```

### Request Logging

Serilog request logging is configured with additional enrichment:

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});
```

### Writing Effective Log Messages

Follow these patterns for consistent, useful logs:

```csharp
// Use structured logging with named parameters
_logger.LogInformation(
    "Invoice {InvoiceId} created for tenant {TenantId} with total {Total:C}",
    invoice.Id, tenantId, invoice.Total);

// Include correlation context
_logger.LogWarning(
    "Payment retry {Attempt} of {MaxAttempts} for invoice {InvoiceId}",
    attempt, maxAttempts, invoiceId);

// Log exceptions with context
_logger.LogError(
    exception,
    "Failed to process payment for invoice {InvoiceId}. Gateway: {Gateway}",
    invoiceId, gateway);

// Use appropriate log levels
_logger.LogDebug("Starting invoice processing for {LineItemCount} items", lineItems.Count);
_logger.LogInformation("Invoice {InvoiceId} issued successfully", invoiceId);
_logger.LogWarning("Payment retry {Attempt} of {MaxAttempts} for invoice {InvoiceId}", attempt, maxAttempts, invoiceId);
_logger.LogError("Payment gateway timeout for transaction {TransactionId}", transactionId);
_logger.LogCritical("Database connection lost, entering degraded mode");
```

## OpenTelemetry Tracing

OpenTelemetry provides distributed tracing across HTTP requests, database operations, and message processing.

### Configuration

Tracing is configured in `ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddObservability(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "Wallow";
    var otlpGrpcEndpoint = configuration["OpenTelemetry:OtlpGrpcEndpoint"] ?? "http://localhost:4317";

    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: serviceName,
                serviceNamespace: "Wallow",
                serviceVersion: typeof(ServiceCollectionExtensions).Assembly
                    .GetName().Version?.ToString() ?? "1.0.0")
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", environment.EnvironmentName)
            }))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Wolverine")
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpGrpcEndpoint);
            }));

    return services;
}
```

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
- **RabbitMQ Messages**: Wolverine propagates trace context through message headers
- **SignalR**: Trace context flows through WebSocket connections

### Adding Custom Spans

For custom operations that need tracing:

```csharp
using System.Diagnostics;

public class PaymentService
{
    private static readonly ActivitySource ActivitySource = new("Wallow.Billing");

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
    {
        using var activity = ActivitySource.StartActivity("ProcessPayment");
        activity?.SetTag("payment.amount", request.Amount);
        activity?.SetTag("payment.currency", request.Currency);
        activity?.SetTag("payment.gateway", request.Gateway);

        try
        {
            var result = await _gateway.ChargeAsync(request);
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

Register the ActivitySource in OpenTelemetry configuration:

```csharp
.WithTracing(tracing => tracing
    .AddSource("Wallow.Billing")  // Add your custom source
    // ... other configuration
```

### Correlating Errors with Traces

The `GlobalExceptionHandler` includes trace IDs in error responses:

```csharp
var traceId = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier;

_logger.LogError(
    exception,
    "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}",
    traceId,
    httpContext.Request.Path);

var problemDetails = new ProblemDetails
{
    // ...
    Extensions =
    {
        ["traceId"] = traceId
    }
};
```

Error responses include the trace ID:
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

```csharp
// src/Shared/Wallow.Shared.Kernel/Diagnostics.cs
public static class Diagnostics
{
    public static readonly Meter Meter = new("Wallow");
}
```

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

The `Wallow` meter is already registered in the OpenTelemetry configuration:

```csharp
.WithMetrics(metrics => metrics
    .AddMeter("Wallow")  // Custom Wallow metrics
    // ... other configuration
```

## Local Development

### Starting the Observability Stack

The Grafana LGTM container provides a complete observability backend:

```bash
cd docker
docker compose up -d grafana-lgtm
```

This starts:
- **Grafana** on http://localhost:3001 (admin/admin)
- **OTLP gRPC receiver** on port 4317
- **OTLP HTTP receiver** on port 4318
- **Loki** for logs
- **Tempo** for traces
- **Prometheus** for metrics

### Configuration for Local Development

Enable OpenTelemetry logging in `appsettings.Development.json`:

```json
{
  "OpenTelemetry": {
    "EnableLogging": true,
    "ServiceName": "Wallow",
    "OtlpEndpoint": "http://localhost:4318",
    "OtlpGrpcEndpoint": "http://localhost:4317"
  }
}
```

### Accessing Grafana

1. Open http://localhost:3000
2. Login with admin/admin
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

Configure OTLP endpoints via environment variables or appsettings:

```json
{
  "OpenTelemetry": {
    "EnableLogging": true,
    "ServiceName": "Wallow",
    "OtlpEndpoint": "https://otel-collector.yourcompany.com",
    "OtlpGrpcEndpoint": "https://otel-collector.yourcompany.com:4317"
  }
}
```

Or via environment variables:
```bash
export OpenTelemetry__ServiceName="Wallow-Production"
export OpenTelemetry__OtlpEndpoint="https://otel-collector.yourcompany.com"
export OpenTelemetry__OtlpGrpcEndpoint="https://otel-collector.yourcompany.com:4317"
```

### Log Shipping

For production, logs can be shipped to:

1. **OTLP Collector**: Enable `OpenTelemetry:EnableLogging` to send logs via OTLP
2. **Managed Services**: Configure Serilog sinks for services like Datadog, Seq, or Elastic

Example adding a Seq sink:
```csharp
configuration.WriteTo.Seq("https://seq.yourcompany.com");
```

### Performance Considerations

1. **Sampling**: For high-traffic production systems, configure trace sampling:
   ```csharp
   .WithTracing(tracing => tracing
       .SetSampler(new TraceIdRatioBasedSampler(0.1))  // 10% sampling
       // ...
   ```

2. **Log Levels**: Use Warning or higher in production to reduce log volume

3. **Batch Export**: OTLP exporters batch by default. Adjust if needed:
   ```csharp
   .AddOtlpExporter(options =>
   {
       options.BatchExportProcessorOptions.MaxQueueSize = 2048;
       options.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 5000;
   })
   ```

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
1. Check RabbitMQ management UI for queue depth
2. Search logs for message type
3. Look for error patterns in Wolverine dead letter queues

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
// Format: Wallow.{ModuleName}
private static readonly ActivitySource ActivitySource = new("Wallow.Billing");
private static readonly ActivitySource ActivitySource = new("Wallow.Storage");
```

**Metrics:**
```csharp
// Format: wallow.{module}.{metric_name}
// Use snake_case, be descriptive
Diagnostics.Meter.CreateCounter<long>("wallow.billing.invoices_created");
Diagnostics.Meter.CreateHistogram<double>("wallow.billing.payment_duration_seconds");
Diagnostics.Meter.CreateCounter<long>("wallow.storage.files_uploaded");
```

**Log Properties:**
```csharp
// Use PascalCase for property names
// Include IDs, counts, durations, statuses
_logger.LogInformation(
    "Order {OrderId} processed in {DurationMs}ms with {ItemCount} items. Status: {Status}",
    orderId, duration.TotalMilliseconds, itemCount, status);
```

### Complete Example: Adding Observability to a Service

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;

public class PaymentService
{
    private static readonly ActivitySource ActivitySource =
        Diagnostics.CreateActivitySource("Billing");

    private static readonly Counter<long> PaymentsProcessed =
        Diagnostics.Meter.CreateCounter<long>(
            "wallow.billing.payments_processed",
            description: "Number of payments processed");

    private static readonly Histogram<double> PaymentDuration =
        Diagnostics.Meter.CreateHistogram<double>(
            "wallow.billing.payment_duration_seconds",
            unit: "s",
            description: "Time to process a payment");

    private readonly ILogger<PaymentService> _logger;
    private readonly IPaymentRepository _repository;

    public PaymentService(
        ILogger<PaymentService> logger,
        IPaymentRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("ProcessPayment");
        activity?.SetTag("payment.invoice_id", request.InvoiceId.ToString());
        activity?.SetTag("payment.amount", request.Amount);
        activity?.SetTag("payment.tenant_id", request.TenantId.ToString());

        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug(
            "Processing payment of {Amount} for invoice {InvoiceId}",
            request.Amount, request.InvoiceId);

        try
        {
            var result = await _repository.ProcessAsync(request, cancellationToken);

            stopwatch.Stop();
            activity?.SetTag("payment.success", result.IsSuccess);

            if (result.IsSuccess)
            {
                PaymentsProcessed.Add(1,
                    new KeyValuePair<string, object?>("tenant_id", request.TenantId.ToString()),
                    new KeyValuePair<string, object?>("method", request.Method));

                PaymentDuration.Record(
                    stopwatch.Elapsed.TotalSeconds,
                    new KeyValuePair<string, object?>("tenant_id", request.TenantId.ToString()));

                _logger.LogInformation(
                    "Payment {PaymentId} processed for invoice {InvoiceId}. " +
                    "Amount: {Amount}, Duration: {DurationMs}ms",
                    result.PaymentId, request.InvoiceId,
                    request.Amount, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Payment failed for invoice {InvoiceId}: {Reason}",
                    request.InvoiceId, result.FailureReason);
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            _logger.LogError(ex,
                "Error processing payment for invoice {InvoiceId}",
                request.InvoiceId);
            throw;
        }
    }
}
```

Register the custom ActivitySource:

```csharp
// In ServiceCollectionExtensions.cs
.WithTracing(tracing => tracing
    .AddSource("Wallow.Billing")  // Add this line
    // ... existing configuration
```

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
// From Storage — StorageReadService.cs
private static readonly ActivitySource StorageActivitySource =
    Diagnostics.CreateActivitySource("Storage");

// Inside the method:
using var activity = StorageActivitySource.StartActivity("Storage.GetFiles");
activity?.SetTag("storage.tenant_id", query.TenantId.ToString());
// ... perform work ...
activity?.SetTag("storage.file_count", fileList.Count);
```

Register each module's `ActivitySource` in `ServiceCollectionExtensions.cs`:

```csharp
.WithTracing(tracing => tracing
    .AddSource("Wallow.Storage")
    // ... other sources
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

> **Current modules:** Identity, Storage, Communications, Billing, Configuration. Use these module names in your metrics and traces.

To add a panel: **Grafana** > **Dashboards** > **New Dashboard** > **Add visualization** > select **Prometheus** data source > enter the PromQL query > choose an appropriate visualization (Stat for counters, Time Series for rates, Heatmap for histograms).

## Related Documentation

- [Developer Guide](DEVELOPER_GUIDE.md) - General development practices
- [Deployment Guide](DEPLOYMENT_GUIDE.md) - Production deployment including observability
- [Messaging Guide](MESSAGING_GUIDE.md) - Message tracing with Wolverine
