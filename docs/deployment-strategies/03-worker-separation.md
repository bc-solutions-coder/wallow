# Worker Separation: Scaling API and Background Processing Independently

This guide covers separating Wallow's API instances from background workers so they can scale independently based on their distinct workload characteristics.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Understanding Wallow's Background Processing](#2-understanding-wallows-background-processing)
3. [Architecture Diagram](#3-architecture-diagram)
4. [Implementation](#4-implementation)
5. [Docker Compose Configuration](#5-docker-compose-configuration)
6. [Kubernetes Deployment](#6-kubernetes-deployment-optional)
7. [Scaling Strategies](#7-scaling-strategies)
8. [Monitoring](#8-monitoring)
9. [Graceful Shutdown](#9-graceful-shutdown)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Overview

### Why Separate API from Workers

In a production environment, HTTP API traffic and background message processing have fundamentally different characteristics:

| Characteristic | API Instances | Worker Instances |
|----------------|---------------|------------------|
| **Trigger** | HTTP requests from users | Messages in queues |
| **Scaling signal** | Request rate, latency | Queue depth, processing time |
| **Resource profile** | Low memory, high concurrency | Higher memory, batch processing |
| **Failure impact** | User-facing errors | Delayed processing |
| **Startup time** | Fast (affects user experience) | Can be slower |

### Different Scaling Needs

**API instances** scale based on:
- Incoming request rate (requests per second)
- Response latency (P95, P99)
- Connection count
- CPU utilization from request handling

**Worker instances** scale based on:
- Queue depth (messages waiting)
- Processing throughput (messages per second)
- Memory consumption during batch operations
- Event sourcing projection lag

### Resource Contention Issues

Running both workloads in a single process causes problems:

1. **CPU contention**: A CPU-intensive batch job starves HTTP request handling
2. **Memory pressure**: Large projection rebuilds consume memory needed for request processing
3. **Thread pool exhaustion**: Background tasks consume threads needed for HTTP connections
4. **Deployment risk**: Deploying for API changes disrupts ongoing message processing
5. **Scaling mismatch**: Cannot add workers without adding unnecessary HTTP capacity

Separating concerns allows:
- API instances to remain responsive during heavy background processing
- Workers to use full machine resources without affecting latency
- Independent scaling based on actual workload
- Zero-downtime deployments for API changes while workers continue processing

---

## 2. Understanding Wallow's Background Processing

Wallow uses three distinct background processing systems, each with different characteristics.

### 2.1 Wolverine Message Handlers

Wolverine is Wallow's unified mediator and message bus. It handles both in-process commands/queries and asynchronous messages from RabbitMQ.

**How Wolverine consumes RabbitMQ messages:**

```
┌──────────────────────────────────────────────────────────────────┐
│                        RabbitMQ                                  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  │
│  │ identity-events │  │ billing-events  │  │ comms-events    │  │
│  │   (exchange)    │  │   (exchange)    │  │   (exchange)    │  │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘  │
│           │                    │                    │           │
│  ┌────────▼────────┐  ┌────────▼────────┐  ┌────────▼────────┐  │
│  │ communications- │  │  billing-inbox  │  │  storage-inbox  │  │
│  │     inbox       │  │                 │  │                 │  │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘  │
└───────────┼──────────────────────┼────────────────────┼─────────┘
            │                      │                    │
            ▼                      ▼                    ▼
    ┌───────────────────────────────────────────────────────────┐
    │                    Wolverine Runtime                       │
    │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
    │  │  Handler    │  │  Handler    │  │  Handler    │  ...   │
    │  │  Discovery  │  │  Execution  │  │  Retry      │        │
    │  └─────────────┘  └─────────────┘  └─────────────┘        │
    └───────────────────────────────────────────────────────────┘
```

**Event handlers** react to domain events published by other modules:

```csharp
// Example: Communications module handles billing events
public class InvoiceOverdueHandler : IWolverineHandler
{
    public async Task Handle(InvoiceOverdueEvent @event, IDocumentSession session)
    {
        // Create notification when invoice becomes overdue
        var notification = new Notification(
            userId: @event.CustomerId,
            title: "Invoice Overdue",
            message: $"Invoice {@event.InvoiceNumber} is overdue"
        );
        session.Store(notification);
    }
}
```

**Command handlers** process commands sent from the API or other handlers:

```csharp
public class SendEmailHandler : IWolverineHandler
{
    public async Task Handle(SendEmailCommand command, IEmailService emailService)
    {
        await emailService.SendAsync(command.To, command.Subject, command.Body);
    }
}
```

**Sagas and durable workflows** manage long-running processes with Wolverine's saga support:

```csharp
public class OrderFulfillmentSaga : Saga
{
    public Guid OrderId { get; set; }
    public OrderState State { get; set; }

    public void Start(OrderPlacedEvent @event)
    {
        OrderId = @event.OrderId;
        State = OrderState.PaymentPending;
    }

    public OutgoingMessages Handle(PaymentReceivedEvent @event)
    {
        State = OrderState.Fulfilling;
        return new OutgoingMessages { new ShipOrderCommand(OrderId) };
    }
}
```

### 2.2 Hangfire Jobs

Hangfire handles scheduled and recurring background jobs that are not event-driven.

**Scheduled jobs** run at a specific time:

```csharp
// Schedule a job to run in 1 hour
BackgroundJob.Schedule<SendReminderJob>(
    job => job.Execute(invoiceId),
    TimeSpan.FromHours(1));
```

**Recurring jobs** run on a cron schedule:

```csharp
// Defined in module registration
RecurringJob.AddOrUpdate<ExpireReservationsJob>(
    "expire-reservations",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *"); // Every 5 minutes

RecurringJob.AddOrUpdate<SystemHeartbeatJob>(
    "system-heartbeat",
    job => job.ExecuteAsync(),
    "*/5 * * * *");
```

**Fire-and-forget jobs** run immediately in the background:

```csharp
// Enqueue a job to run as soon as possible
BackgroundJob.Enqueue<GenerateReportJob>(
    job => job.Execute(reportId));
```

**Hangfire storage**: Wallow uses PostgreSQL for Hangfire job persistence:

```csharp
config.UsePostgreSqlStorage(opts =>
    opts.UseNpgsqlConnection(connectionString),
    new PostgreSqlStorageOptions
    {
        SchemaName = "hangfire"
    });
```

### 2.3 Additional Background Processing

Wallow's current modules (Identity, Billing, Communications, Storage, Configuration) use EF Core for persistence rather than event sourcing. If you add event-sourced modules using Marten in the future, note that Marten's Async Daemon for projections is an additional background process that should run in worker mode. Marten supports leader election via PostgreSQL advisory locks (`DaemonMode.HotCold`) to ensure only one worker processes async projections.

---

## 3. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Load Balancer                                   │
│                        (Caddy, Nginx, or Cloud LB)                          │
└─────────────────────────────────┬───────────────────────────────────────────┘
                                  │
                                  │ HTTP/HTTPS
                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           API Instances                                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐             │
│  │   API Pod 1     │  │   API Pod 2     │  │   API Pod 3     │  ...        │
│  │                 │  │                 │  │                 │             │
│  │ • HTTP endpoints│  │ • HTTP endpoints│  │ • HTTP endpoints│             │
│  │ • Wolverine     │  │ • Wolverine     │  │ • Wolverine     │             │
│  │   mediator only │  │   mediator only │  │   mediator only │             │
│  │ • Hangfire      │  │ • Hangfire      │  │ • Hangfire      │             │
│  │   enqueue only  │  │   enqueue only  │  │   enqueue only  │             │
│  │ • SignalR hub   │  │ • SignalR hub   │  │ • SignalR hub   │             │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘             │
└───────────┼──────────────────────┼────────────────────┼─────────────────────┘
            │                      │                    │
            │    Publish messages  │                    │    Enqueue jobs
            ▼                      ▼                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Shared Infrastructure                               │
│                                                                              │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐             │
│  │    RabbitMQ     │  │   PostgreSQL    │  │  Valkey/Redis   │             │
│  │                 │  │                 │  │                 │             │
│  │ • Event queues  │  │ • Application   │  │ • SignalR       │             │
│  │ • Command queues│  │   data          │  │   backplane     │             │
│  │ • DLQ           │  │ • Hangfire jobs │  │ • Distributed   │             │
│  │                 │  │ • Wolverine     │  │   cache         │             │
│  │                 │  │   outbox        │  │ • Presence      │             │
│  │                 │  │ • Marten events │  │                 │             │
│  └────────┬────────┘  └────────┬────────┘  └─────────────────┘             │
└───────────┼──────────────────────┼──────────────────────────────────────────┘
            │                      │
            │  Consume messages    │  Process jobs
            ▼                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Worker Instances                                    │
│  ┌─────────────────┐  ┌─────────────────┐                                   │
│  │  Worker Pod 1   │  │  Worker Pod 2   │                                   │
│  │                 │  │                 │                                   │
│  │ • Wolverine     │  │ • Wolverine     │                                   │
│  │   consumers     │  │   consumers     │                                   │
│  │ • Hangfire      │  │ • Hangfire      │                                   │
│  │   server        │  │   server        │                                   │
│  │ • Marten async  │  │ • Health check  │                                   │
│  │   daemon        │  │   endpoint      │                                   │
│  │ • Health check  │  │                 │                                   │
│  │   endpoint      │  │ (daemon on      │                                   │
│  │                 │  │  leader only)   │                                   │
│  └─────────────────┘  └─────────────────┘                                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Key points:**
- API instances handle HTTP traffic, use Wolverine as an in-process mediator, and publish messages
- Workers consume messages from RabbitMQ, run Hangfire jobs, and process Marten async projections
- Both connect to the same PostgreSQL, RabbitMQ, and Redis instances
- SignalR uses Redis backplane so real-time updates work across API instances
- Wolverine's durable outbox ensures messages are not lost during API instance failures

---

## 4. Implementation

### 4.1 Creating a Startup Configuration Flag

Modify `Program.cs` to support different operational modes via environment variable or command-line argument.

**Add the WallowMode enum and helper:**

Create a new file `src/Wallow.Api/Configuration/WallowMode.cs`:

```csharp
namespace Wallow.Api.Configuration;

/// <summary>
/// Determines which components are enabled at startup.
/// </summary>
public enum WallowMode
{
    /// <summary>
    /// Full mode - runs both HTTP endpoints and background workers.
    /// Use for development or single-instance deployments.
    /// </summary>
    Full,

    /// <summary>
    /// API mode - runs HTTP endpoints only.
    /// Wolverine mediator enabled, RabbitMQ consumers disabled.
    /// Hangfire enqueue only, no server.
    /// </summary>
    Api,

    /// <summary>
    /// Worker mode - runs background workers only.
    /// No HTTP endpoints (except health check).
    /// Wolverine consumers enabled, Hangfire server enabled.
    /// </summary>
    Worker
}

public static class WallowModeExtensions
{
    public static WallowMode GetWallowMode(this IConfiguration configuration)
    {
        // Check command-line argument first (--mode api)
        var modeArg = configuration["mode"];

        // Then check environment variable (WALLOW_MODE=api)
        if (string.IsNullOrEmpty(modeArg))
        {
            modeArg = configuration["WALLOW_MODE"];
        }

        // Default to Full for development
        if (string.IsNullOrEmpty(modeArg))
        {
            return WallowMode.Full;
        }

        return modeArg.ToLowerInvariant() switch
        {
            "api" => WallowMode.Api,
            "worker" => WallowMode.Worker,
            "full" => WallowMode.Full,
            _ => throw new ArgumentException($"Invalid WALLOW_MODE: {modeArg}. Valid values: api, worker, full")
        };
    }

    public static bool ShouldRunHttp(this WallowMode mode) => mode != WallowMode.Worker;
    public static bool ShouldRunConsumers(this WallowMode mode) => mode != WallowMode.Api;
    public static bool ShouldRunHangfireServer(this WallowMode mode) => mode != WallowMode.Api;
    public static bool ShouldRunMartenDaemon(this WallowMode mode) => mode != WallowMode.Api;
}
```

**Update Program.cs to use the mode:**

```csharp
using Wallow.Api.Configuration;

// ... existing using statements ...

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Determine operational mode
    var wallowMode = builder.Configuration.GetWallowMode();
    Log.Information("Starting Wallow in {Mode} mode", wallowMode);

    // Store mode for later use
    builder.Services.AddSingleton(wallowMode);

    // Serilog configuration (unchanged)
    builder.Host.UseSerilog((context, services, configuration) => /* ... */);

    // Wolverine configuration - conditional based on mode
    builder.Host.UseWolverine(opts =>
    {
        // PostgreSQL persistence (always enabled for outbox)
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Database connection string not configured");
            opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");
        }

        // EF Core transaction integration (always enabled)
        opts.UseEntityFrameworkCoreTransactions();

        // Error handling and logging (always enabled)
        opts.ConfigureStandardErrorHandling();
        opts.ConfigureMessageLogging();

        // FluentValidation middleware (always enabled)
        opts.UseFluentValidation();

        // Handler discovery (always enabled - handlers are needed for local invocation)
        opts.Discovery.IncludeAssembly(typeof(Wallow.Storage.Application.Commands.UploadFile.UploadFileCommand).Assembly);
        // ... other assembly inclusions ...

        // RabbitMQ transport configuration
        var rabbitMqConnection = builder.Configuration.GetConnectionString("RabbitMq");
        if (!string.IsNullOrEmpty(rabbitMqConnection))
        {
            var rabbitMq = opts.UseRabbitMq(new Uri(rabbitMqConnection))
                .AutoProvision();

            if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
            {
                rabbitMq.AutoPurgeOnStartup();
            }

            // Publishing - always enabled (API needs to publish, workers might too)
            opts.PublishMessage<Wallow.Shared.Contracts.Identity.Events.UserRegisteredEvent>()
                .ToRabbitExchange("identity-events");
            // ... other publish configurations ...

            // Consumer queues - only enabled in Worker or Full mode
            if (wallowMode.ShouldRunConsumers())
            {
                opts.ListenToRabbitQueue("communications-inbox");
                opts.ListenToRabbitQueue("billing-inbox");
                opts.ListenToRabbitQueue("storage-inbox");
                opts.ListenToRabbitQueue("configuration-inbox");

                if (builder.Environment.IsEnvironment("Testing"))
                {
                    opts.ListenToRabbitQueue("test-inbox");
                }
            }
        }

        // Durable outbox (always enabled for reliability)
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        }
    });

    // Redis and SignalR (always enabled - needed for cache and real-time in all modes)
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp => /* ... */);
    // ... SignalR configuration ...

    // Core services (always enabled)
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSharedKernel();
    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddObservability(builder.Configuration, builder.Environment);

    // Hangfire - conditional server registration
    builder.Services.AddHangfireServices(builder.Configuration, builder.Environment, wallowMode);

    // Controllers - only in API or Full mode
    if (wallowMode.ShouldRunHttp())
    {
        builder.Services.AddControllers();
    }

    // Register modules (always - they contain domain logic used by both modes)
    builder.Services.AddIdentityModule(builder.Configuration);
    // ... other module registrations ...

    var app = builder.Build();

    // Module initialization (always)
    await app.UseIdentityModuleAsync();
    // ... other module initializations ...

    // Middleware pipeline - conditional based on mode
    if (wallowMode.ShouldRunHttp())
    {
        // Full HTTP middleware pipeline
        app.UseExceptionHandler("/error");
        app.UseSerilogRequestLogging(/* ... */);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(/* ... */);
        }

        // CORS
        if (app.Environment.IsDevelopment())
        {
            app.UseCors("Development");
        }
        else
        {
            app.UseCors();
        }
    }

    // Health checks - always enabled (for orchestrator probes)
    app.MapHealthChecks("/health", new HealthCheckOptions { /* ... */ });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { /* ... */ });
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

    if (wallowMode.ShouldRunHttp())
    {
        // API endpoints and middleware
        app.MapGet("/", () => Results.Ok(new { /* ... */ })).ExcludeFromDescription();

        app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseMiddleware<ScimAuthenticationMiddleware>();
        app.UseMiddleware<PermissionExpansionMiddleware>();
        app.UseAuthorization();
        app.UseServiceAccountTracking();

        app.UseHangfireDashboard(); // Dashboard available in API mode
        app.MapControllers();
        app.MapHub<RealtimeHub>("/hubs/realtime");
    }

    // Recurring jobs - only in Worker or Full mode
    if (wallowMode.ShouldRunHangfireServer())
    {
        app.RegisterRecurringJobs();

        RecurringJob.AddOrUpdate<SystemHeartbeatJob>(
            "system-heartbeat",
            job => job.ExecuteAsync(),
            "*/5 * * * *");
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
```

### 4.2 Disabling Wolverine Consumers in API Mode

The key configuration is in the `UseWolverine` section. Here's the complete pattern:

```csharp
builder.Host.UseWolverine(opts =>
{
    // === ALWAYS ENABLED ===

    // PostgreSQL persistence for durable outbox
    // Required for reliable message publishing from API instances
    var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");

    // EF Core integration - needed for transactional outbox
    opts.UseEntityFrameworkCoreTransactions();

    // Error handling (applies to local handlers too)
    opts.ConfigureStandardErrorHandling();
    opts.ConfigureMessageLogging();

    // FluentValidation for command validation
    opts.UseFluentValidation();

    // Handler discovery - include all assemblies
    // Local invocation (IMessageBus.InvokeAsync) needs handlers
    opts.Discovery.IncludeAssembly(typeof(Wallow.Billing.Application.Commands.CreateInvoice.CreateInvoiceCommand).Assembly);
    opts.Discovery.IncludeAssembly(typeof(Wallow.Communications.Application.Commands.SendNotification.SendNotificationCommand).Assembly);
    // ... include all module assemblies ...

    // === RABBITMQ TRANSPORT ===

    var rabbitMqConnection = builder.Configuration.GetConnectionString("RabbitMq");
    if (!string.IsNullOrEmpty(rabbitMqConnection))
    {
        var rabbitMq = opts.UseRabbitMq(new Uri(rabbitMqConnection))
            .AutoProvision();

        // === PUBLISHING (ALWAYS ENABLED) ===
        // API instances need to publish events/commands to queues

        opts.PublishMessage<UserRegisteredEvent>().ToRabbitExchange("identity-events");
        opts.PublishMessage<InvoiceCreatedEvent>().ToRabbitExchange("billing-events");
        opts.PublishMessage<PaymentReceivedEvent>().ToRabbitExchange("billing-events");
        opts.PublishMessage<NotificationCreatedEvent>().ToRabbitExchange("communications-events");
        // ... other publish configurations ...

        // === CONSUMING (CONDITIONAL) ===
        // Only workers should consume from queues

        if (wallowMode.ShouldRunConsumers())
        {
            Log.Information("Enabling Wolverine RabbitMQ consumers");

            opts.ListenToRabbitQueue("communications-inbox")
                .ProcessInline(); // Or configure parallelism

            opts.ListenToRabbitQueue("billing-inbox")
                .Sequential(); // For ordered processing

            opts.ListenToRabbitQueue("storage-inbox");
            opts.ListenToRabbitQueue("configuration-inbox");

            // Configure consumer parallelism for workers
            opts.Policies.ConfigureConventionalLocalRouting()
                .CustomizeQueues((_, queue) =>
                {
                    queue.MaximumParallelMessages = 10;
                });
        }
        else
        {
            Log.Information("Wolverine RabbitMQ consumers disabled (API mode)");
        }
    }

    // Durable outbox - always enabled for reliable publishing
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
});
```

**How this works:**

1. **API mode**: Wolverine connects to RabbitMQ for publishing only. No `ListenToRabbitQueue` calls means no consumers are started. The mediator functionality (`IMessageBus.InvokeAsync`) still works for local command/query handling.

2. **Worker mode**: Wolverine starts consumers for all configured queues. Messages are processed by the discovered handlers.

3. **Full mode**: Both publishing and consuming are enabled (development default).

### 4.3 Disabling HTTP in Worker Mode

For worker mode, we want to disable HTTP endpoints while keeping health checks for orchestrator probes.

**Option 1: Minimal HTTP (Recommended)**

Keep Kestrel but only expose health endpoints:

```csharp
if (wallowMode.ShouldRunHttp())
{
    // Full API middleware and endpoints
    builder.Services.AddControllers();
    // ... full pipeline ...
}
else
{
    // Worker mode - minimal endpoints
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Listen on a different port for health checks
        options.ListenAnyIP(8081);
    });
}

var app = builder.Build();

// Health checks always available
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = WriteHealthCheckResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

if (wallowMode.ShouldRunHttp())
{
    // Map all API routes, controllers, SignalR, etc.
    app.MapControllers();
    app.MapHub<RealtimeHub>("/hubs/realtime");
    // ... etc ...
}
```

**Option 2: IHostedService for Worker (No HTTP)**

For a truly HTTP-less worker, create a separate worker project or use `Host.CreateDefaultBuilder`:

```csharp
// Program.cs for a dedicated worker project
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .UseWolverine(opts =>
            {
                // Full consumer configuration
                opts.UseRabbitMq(/* ... */).AutoProvision();
                opts.ListenToRabbitQueue("communications-inbox");
                // ... etc ...
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHangfireServer(); // Only server, no dashboard
                // Register module services
            })
            .Build();

        await host.RunAsync();
    }
}
```

**Health check endpoint for workers:**

Even in worker mode, expose a health endpoint for Kubernetes probes:

```csharp
// In worker-only mode, add a minimal health endpoint
if (!wallowMode.ShouldRunHttp())
{
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        mode = "Worker",
        timestamp = DateTimeOffset.UtcNow
    }));
}
```

### 4.4 Hangfire Configuration

Update `HangfireExtensions.cs` to support mode-based configuration:

```csharp
using Wallow.Api.Configuration;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Hosting;

namespace Wallow.Api.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        WallowMode mode)
    {
        // Hangfire core configuration (always needed for job scheduling)
        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();

            if (environment.IsEnvironment("Testing"))
            {
                config.UseInMemoryStorage();
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")!;
                config.UsePostgreSqlStorage(opts =>
                    opts.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = "hangfire",
                        // Adjust based on mode
                        PrepareSchemaIfNecessary = mode.ShouldRunHangfireServer(),
                        // Visibility timeout - how long before a job is retried
                        InvisibilityTimeout = TimeSpan.FromMinutes(30)
                    });
            }
        });

        // Hangfire server - only in Worker or Full mode
        if (mode.ShouldRunHangfireServer())
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = Environment.ProcessorCount * 2;
                options.Queues = new[] { "default", "critical", "low" };
                options.ServerName = $"wallow-worker-{Environment.MachineName}";

                // Graceful shutdown timeout
                options.ShutdownTimeout = TimeSpan.FromSeconds(30);
            });
        }

        return services;
    }

    public static WebApplication UseHangfireDashboard(
        this WebApplication app,
        WallowMode mode)
    {
        // Dashboard only available in API or Full mode
        // Workers don't need the dashboard
        if (mode.ShouldRunHttp())
        {
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = [new HangfireDashboardAuthFilter(app.Environment)],
                DashboardTitle = "Wallow Jobs",
                DisplayStorageConnectionString = false,
                IsReadOnlyFunc = _ => false // Allow job operations from dashboard
            });
        }

        return app;
    }

    public static void RegisterRecurringJobs(this WebApplication app, WallowMode mode)
    {
        // Skip if not running Hangfire server
        if (!mode.ShouldRunHangfireServer())
        {
            return;
        }

        if (app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var registrations = scope.ServiceProvider.GetServices<IRecurringJobRegistration>();

        foreach (var registration in registrations)
        {
            registration.RegisterJobs();
        }
    }
}
```

**Queue prioritization for workers:**

Configure different workers to handle different queues:

```csharp
// High-priority worker
services.AddHangfireServer(options =>
{
    options.Queues = new[] { "critical", "default" };
    options.WorkerCount = 4;
});

// Bulk processing worker
services.AddHangfireServer(options =>
{
    options.Queues = new[] { "bulk", "low" };
    options.WorkerCount = 2;
});
```

### 4.5 Module-Specific Worker Configuration

For modules with significant background processing needs, the mode can be passed to module registration:

```csharp
public static class CommunicationsModuleExtensions
{
    public static IServiceCollection AddCommunicationsModule(
        this IServiceCollection services,
        IConfiguration config,
        WallowMode mode)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")!;

        services.AddDbContext<CommunicationsDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "communications");
            });
        });

        // Register services
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IEmailMessageRepository, EmailMessageRepository>();

        // Email delivery service - needed in both API and Worker modes
        services.AddScoped<IEmailService, SmtpEmailService>();

        return services;
    }
}
```

This pattern allows each module to conditionally register background-processing-specific services based on the operational mode.

---

## 5. Docker Compose Configuration

### 5.1 Separate Services

Create a `docker-compose.separated.yml` that defines API and Worker as separate services:

```yaml
# docker-compose.separated.yml
# Production configuration with separated API and Worker services

services:
  # ============================================
  # API INSTANCES
  # ============================================
  api:
    image: ${APP_IMAGE}:${APP_TAG}
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      WALLOW_MODE: api

      # Database
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}

      # RabbitMQ (for publishing)
      ConnectionStrings__RabbitMq: amqp://${RABBITMQ_USER}:${RABBITMQ_PASSWORD}@rabbitmq:5672/

      # Redis (for SignalR backplane and cache)
      ConnectionStrings__Redis: valkey:6379

      # Keycloak
      Keycloak__Authority: ${KEYCLOAK_URL}/realms/wallow
      Keycloak__AdminUrl: ${KEYCLOAK_URL}

      # OpenTelemetry
      OpenTelemetry__OtlpEndpoint: http://grafana-lgtm:4318
      OpenTelemetry__ServiceName: wallow-api
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
      valkey:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/health/ready || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    deploy:
      replicas: 4
      resources:
        limits:
          cpus: '1.0'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
      update_config:
        parallelism: 2
        delay: 10s
        failure_action: rollback
        order: start-first
    restart: unless-stopped
    networks:
      - wallow
    logging:
      driver: json-file
      options:
        max-size: "50m"
        max-file: "5"

  # ============================================
  # WORKER INSTANCES
  # ============================================
  worker:
    image: ${APP_IMAGE}:${APP_TAG}
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      WALLOW_MODE: worker
      ASPNETCORE_URLS: http://+:8081

      # Database
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}

      # RabbitMQ (for consuming)
      ConnectionStrings__RabbitMq: amqp://${RABBITMQ_USER}:${RABBITMQ_PASSWORD}@rabbitmq:5672/

      # Redis (for cache)
      ConnectionStrings__Redis: valkey:6379

      # Keycloak (for token validation in jobs if needed)
      Keycloak__Authority: ${KEYCLOAK_URL}/realms/wallow

      # Hangfire worker configuration
      Hangfire__WorkerCount: "8"
      Hangfire__Queues__0: critical
      Hangfire__Queues__1: default
      Hangfire__Queues__2: bulk

      # OpenTelemetry
      OpenTelemetry__OtlpEndpoint: http://grafana-lgtm:4318
      OpenTelemetry__ServiceName: wallow-worker
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
      valkey:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8081/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    deploy:
      replicas: 2
      resources:
        limits:
          cpus: '2.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
      update_config:
        parallelism: 1
        delay: 30s
        failure_action: rollback
        order: stop-first
    restart: unless-stopped
    networks:
      - wallow
    logging:
      driver: json-file
      options:
        max-size: "50m"
        max-file: "5"

  # ============================================
  # DATABASE
  # ============================================
  postgres:
    image: postgres:18-alpine
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-postgres
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-db.sql:/docker-entrypoint-initdb.d/01-init-db.sql:ro
      - ./init-keycloak-db.sql:/docker-entrypoint-initdb.d/02-init-keycloak-db.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
    deploy:
      resources:
        limits:
          memory: 2G
    restart: unless-stopped
    networks:
      - wallow

  # ============================================
  # MESSAGE BROKER
  # ============================================
  rabbitmq:
    image: rabbitmq:4.2-management-alpine
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
      - ./rabbitmq/rabbitmq.conf:/etc/rabbitmq/rabbitmq.conf:ro
    healthcheck:
      test: rabbitmq-diagnostics -q ping
      interval: 10s
      timeout: 5s
      retries: 5
    deploy:
      resources:
        limits:
          memory: 512M
    restart: unless-stopped
    networks:
      - wallow

  # ============================================
  # CACHE & BACKPLANE
  # ============================================
  valkey:
    image: valkey/valkey:8-alpine
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-valkey
    command: valkey-server --appendonly yes
    volumes:
      - valkey_data:/data
    healthcheck:
      test: ["CMD", "valkey-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    deploy:
      resources:
        limits:
          memory: 256M
    restart: unless-stopped
    networks:
      - wallow

  # ============================================
  # IDENTITY PROVIDER
  # ============================================
  keycloak:
    image: quay.io/keycloak/keycloak:26.0
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-keycloak
    command: start --import-realm --optimized
    environment:
      KC_DB: postgres
      KC_DB_URL: jdbc:postgresql://postgres:5432/keycloak_db
      KC_DB_USERNAME: ${POSTGRES_USER}
      KC_DB_PASSWORD: ${POSTGRES_PASSWORD}
      KC_HEALTH_ENABLED: "true"
      KC_HOSTNAME: ${KEYCLOAK_HOSTNAME}
      KC_HTTP_ENABLED: "true"
      KC_PROXY_HEADERS: xforwarded
    ports:
      - "8081:8080"
    volumes:
      - ./keycloak/realm-export.json:/opt/keycloak/data/import/realm-export.json:ro
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "exec 3<>/dev/tcp/localhost/9000 && echo -e 'GET /health/ready HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3 && cat <&3 | grep -q '200'"]
      interval: 10s
      timeout: 5s
      retries: 15
      start_period: 30s
    restart: unless-stopped
    networks:
      - wallow

  # ============================================
  # OBSERVABILITY
  # ============================================
  grafana-lgtm:
    image: grafana/otel-lgtm:latest
    container_name: ${COMPOSE_PROJECT_NAME:-wallow}-grafana-lgtm
    ports:
      - "3000:3000"
      - "4318:4318"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASSWORD:-admin}
    volumes:
      - ./grafana/provisioning/dashboards/dashboards.yml:/otel-lgtm/grafana/conf/provisioning/dashboards/wallow-dashboards.yaml:ro
      - ./grafana/dashboards:/var/lib/grafana/dashboards:ro
    healthcheck:
      test: ["CMD", "wget", "--spider", "-q", "http://localhost:3000/api/health"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - wallow

volumes:
  postgres_data:
  rabbitmq_data:
  valkey_data:

networks:
  wallow:
    driver: bridge
```

### 5.2 Environment Variables

Create `.env.separated`:

```ini
# Project identification
COMPOSE_PROJECT_NAME=wallow-prod

# Environment
ASPNETCORE_ENVIRONMENT=Production

# Docker image
APP_IMAGE=ghcr.io/your-org/wallow
APP_TAG=latest

# Database
POSTGRES_USER=wallow
POSTGRES_PASSWORD=<strong-password>
POSTGRES_DB=wallow

# RabbitMQ
RABBITMQ_USER=wallow
RABBITMQ_PASSWORD=<strong-password>

# Keycloak
KEYCLOAK_URL=http://keycloak:8080
KEYCLOAK_HOSTNAME=auth.yourdomain.com

# Grafana
GRAFANA_PASSWORD=<strong-password>

# API-specific
API_REPLICAS=4

# Worker-specific
WORKER_REPLICAS=2
HANGFIRE_WORKER_COUNT=8
```

### 5.3 Resource Limits

The configuration above already includes resource limits. Here's the rationale:

**API instances:**
```yaml
resources:
  limits:
    cpus: '1.0'
    memory: 512M
  reservations:
    cpus: '0.25'
    memory: 256M
```

- **Lower memory**: API handlers are typically stateless and short-lived
- **Moderate CPU**: Enough for request handling and serialization
- **Many replicas**: Handle concurrent requests through horizontal scaling

**Worker instances:**
```yaml
resources:
  limits:
    cpus: '2.0'
    memory: 1G
  reservations:
    cpus: '0.5'
    memory: 512M
```

- **Higher memory**: Batch processing, projection rebuilds, large message payloads
- **Higher CPU**: Background jobs can be CPU-intensive
- **Fewer replicas**: Each worker can handle many concurrent messages

---

## 6. Kubernetes Deployment (Optional)

### 6.1 Separate Deployments

**ConfigMap for shared configuration:**

```yaml
# wallow-config.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: wallow-config
  namespace: wallow
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  ConnectionStrings__Redis: "valkey-master.wallow.svc.cluster.local:6379"
  Keycloak__Authority: "https://auth.yourdomain.com/realms/wallow"
  OpenTelemetry__OtlpEndpoint: "http://grafana-lgtm.observability.svc.cluster.local:4318"
```

**Secret for sensitive data:**

```yaml
# wallow-secrets.yaml
apiVersion: v1
kind: Secret
metadata:
  name: wallow-secrets
  namespace: wallow
type: Opaque
stringData:
  POSTGRES_PASSWORD: "<strong-password>"
  RABBITMQ_PASSWORD: "<strong-password>"
  ConnectionStrings__DefaultConnection: "Host=postgresql.wallow.svc.cluster.local;Port=5432;Database=wallow;Username=wallow;Password=<password>"
  ConnectionStrings__RabbitMq: "amqp://wallow:<password>@rabbitmq.wallow.svc.cluster.local:5672/"
```

**API Deployment:**

```yaml
# api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: wallow-api
  namespace: wallow
  labels:
    app: wallow
    component: api
spec:
  replicas: 4
  selector:
    matchLabels:
      app: wallow
      component: api
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 2
      maxUnavailable: 1
  template:
    metadata:
      labels:
        app: wallow
        component: api
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      containers:
        - name: api
          image: ghcr.io/your-org/wallow:latest
          ports:
            - containerPort: 8080
              name: http
          env:
            - name: WALLOW_MODE
              value: "api"
            - name: OpenTelemetry__ServiceName
              value: "wallow-api"
          envFrom:
            - configMapRef:
                name: wallow-config
            - secretRef:
                name: wallow-secrets
          resources:
            requests:
              memory: "256Mi"
              cpu: "250m"
            limits:
              memory: "512Mi"
              cpu: "1000m"
          readinessProbe:
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          livenessProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 30
            periodSeconds: 30
            timeoutSeconds: 5
            failureThreshold: 3
          startupProbe:
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 5
            timeoutSeconds: 5
            failureThreshold: 30
      terminationGracePeriodSeconds: 30
---
apiVersion: v1
kind: Service
metadata:
  name: wallow-api
  namespace: wallow
spec:
  selector:
    app: wallow
    component: api
  ports:
    - port: 80
      targetPort: 8080
      name: http
  type: ClusterIP
---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: wallow-api
  namespace: wallow
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  tls:
    - hosts:
        - api.yourdomain.com
      secretName: wallow-api-tls
  rules:
    - host: api.yourdomain.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: wallow-api
                port:
                  number: 80
```

**Worker Deployment:**

```yaml
# worker-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: wallow-worker
  namespace: wallow
  labels:
    app: wallow
    component: worker
spec:
  replicas: 2
  selector:
    matchLabels:
      app: wallow
      component: worker
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0  # Ensure no lost messages during deploy
  template:
    metadata:
      labels:
        app: wallow
        component: worker
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8081"
        prometheus.io/path: "/metrics"
    spec:
      containers:
        - name: worker
          image: ghcr.io/your-org/wallow:latest
          ports:
            - containerPort: 8081
              name: health
          env:
            - name: WALLOW_MODE
              value: "worker"
            - name: ASPNETCORE_URLS
              value: "http://+:8081"
            - name: OpenTelemetry__ServiceName
              value: "wallow-worker"
            - name: Hangfire__WorkerCount
              value: "8"
          envFrom:
            - configMapRef:
                name: wallow-config
            - secretRef:
                name: wallow-secrets
          resources:
            requests:
              memory: "512Mi"
              cpu: "500m"
            limits:
              memory: "1Gi"
              cpu: "2000m"
          readinessProbe:
            httpGet:
              path: /health
              port: 8081
            initialDelaySeconds: 10
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
          livenessProbe:
            httpGet:
              path: /health
              port: 8081
            initialDelaySeconds: 30
            periodSeconds: 30
            timeoutSeconds: 5
            failureThreshold: 3
      # Longer termination grace period for workers
      terminationGracePeriodSeconds: 60
```

### 6.2 Horizontal Pod Autoscaler

**API HPA - Scale on request rate:**

```yaml
# api-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: wallow-api-hpa
  namespace: wallow
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: wallow-api
  minReplicas: 2
  maxReplicas: 10
  metrics:
    # Scale on CPU utilization
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    # Scale on request rate (requires Prometheus adapter)
    - type: Pods
      pods:
        metric:
          name: http_requests_per_second
        target:
          type: AverageValue
          averageValue: "100"
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
        - type: Pods
          value: 2
          periodSeconds: 60
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
        - type: Pods
          value: 1
          periodSeconds: 120
```

**Worker HPA - Scale on queue depth:**

```yaml
# worker-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: wallow-worker-hpa
  namespace: wallow
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: wallow-worker
  minReplicas: 1
  maxReplicas: 8
  metrics:
    # Scale on CPU utilization
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 80
    # Scale on RabbitMQ queue depth (requires custom metrics)
    - type: External
      external:
        metric:
          name: rabbitmq_queue_messages
          selector:
            matchLabels:
              queue: communications-inbox
        target:
          type: AverageValue
          averageValue: "50"
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 30
      policies:
        - type: Pods
          value: 2
          periodSeconds: 30
    scaleDown:
      # Slower scale-down to avoid losing workers during burst processing
      stabilizationWindowSeconds: 600
      policies:
        - type: Pods
          value: 1
          periodSeconds: 300
```

**KEDA for queue-based scaling (recommended):**

```yaml
# worker-keda.yaml
apiVersion: keda.sh/v1alpha1
kind: ScaledObject
metadata:
  name: wallow-worker-scaler
  namespace: wallow
spec:
  scaleTargetRef:
    name: wallow-worker
  minReplicaCount: 1
  maxReplicaCount: 8
  pollingInterval: 15
  cooldownPeriod: 300
  triggers:
    - type: rabbitmq
      metadata:
        protocol: amqp
        queueName: communications-inbox
        mode: QueueLength
        value: "50"
      authenticationRef:
        name: rabbitmq-auth
    - type: rabbitmq
      metadata:
        protocol: amqp
        queueName: billing-inbox
        mode: QueueLength
        value: "50"
      authenticationRef:
        name: rabbitmq-auth
    - type: postgresql
      metadata:
        query: "SELECT COUNT(*) FROM hangfire.job WHERE state_name = 'Enqueued'"
        targetQueryValue: "100"
      authenticationRef:
        name: postgres-auth
---
apiVersion: keda.sh/v1alpha1
kind: TriggerAuthentication
metadata:
  name: rabbitmq-auth
  namespace: wallow
spec:
  secretTargetRef:
    - parameter: host
      name: wallow-secrets
      key: ConnectionStrings__RabbitMq
```

---

## 7. Scaling Strategies

### 7.1 When to Scale API vs Workers

**Scale API instances when:**

| Symptom | Metric | Action |
|---------|--------|--------|
| High response latency | P95 latency > 500ms | Add API replicas |
| Connection timeouts | Connection errors > 1% | Add API replicas |
| High CPU on API pods | CPU > 70% sustained | Add API replicas |
| Request queue buildup | Pending connections > 100 | Add API replicas |

**Scale Worker instances when:**

| Symptom | Metric | Action |
|---------|--------|--------|
| Queue depth growing | Messages in queue > 1000 | Add worker replicas |
| Processing lag | Message age > 30s | Add worker replicas |
| High CPU on workers | CPU > 80% sustained | Add worker replicas |
| Hangfire job backlog | Enqueued jobs > 500 | Add worker replicas |
| Projection lag | Events behind > 1000 | Add worker replicas |

### 7.2 Message Priority

Configure separate queues for different priority levels:

**Wolverine queue configuration:**

```csharp
// High priority messages
opts.PublishMessage<PaymentReceivedEvent>()
    .ToRabbitQueue("critical-inbox");

// Normal priority
opts.PublishMessage<UserRegisteredEvent>()
    .ToRabbitQueue("default-inbox");

// Bulk/low priority
opts.PublishMessage<AnalyticsEvent>()
    .ToRabbitQueue("bulk-inbox");

// Worker configuration - process critical first
opts.ListenToRabbitQueue("critical-inbox")
    .ProcessInline()
    .MaximumParallelMessages(20);

opts.ListenToRabbitQueue("default-inbox")
    .MaximumParallelMessages(10);

opts.ListenToRabbitQueue("bulk-inbox")
    .MaximumParallelMessages(5);
```

**Dedicated worker pools:**

Deploy specialized workers for different workloads:

```yaml
# Critical worker - always running, fast processing
worker-critical:
  environment:
    WALLOW_QUEUES: critical-inbox,default-inbox
    WOLVERINE_PARALLELISM: 20
  replicas: 2

# Bulk worker - scales with queue depth
worker-bulk:
  environment:
    WALLOW_QUEUES: bulk-inbox
    WOLVERINE_PARALLELISM: 5
  replicas: 1  # Scaled by KEDA
```

### 7.3 Workload Isolation

For extreme isolation, run different modules in separate worker pools:

```yaml
# Billing worker - handles payment-critical messages
worker-billing:
  environment:
    WALLOW_MODE: worker
    WALLOW_MODULES: billing
    WALLOW_QUEUES: billing-inbox
  replicas: 2

# Communications worker - handles high-volume notifications and email
worker-communications:
  environment:
    WALLOW_MODE: worker
    WALLOW_MODULES: communications
    WALLOW_QUEUES: communications-inbox
  replicas: 4

# General worker - handles everything else
worker-general:
  environment:
    WALLOW_MODE: worker
    WALLOW_QUEUES: storage-inbox
  replicas: 2
```

---

## 8. Monitoring

### 8.1 API Metrics

Key metrics to track for API instances:

```promql
# Request rate
sum(rate(http_server_requests_total{service="wallow-api"}[5m]))

# P95 latency
histogram_quantile(0.95,
  sum(rate(http_server_request_duration_seconds_bucket{service="wallow-api"}[5m])) by (le)
)

# Error rate
sum(rate(http_server_requests_total{service="wallow-api",status_code=~"5.."}[5m]))
/ sum(rate(http_server_requests_total{service="wallow-api"}[5m]))

# Active connections
sum(http_server_active_requests{service="wallow-api"})
```

### 8.2 Worker Metrics

Key metrics to track for worker instances:

```promql
# Messages processed per second
sum(rate(wolverine_messages_processed_total{service="wallow-worker"}[5m]))

# Message processing time
histogram_quantile(0.95,
  sum(rate(wolverine_message_processing_seconds_bucket{service="wallow-worker"}[5m])) by (le)
)

# Queue depth (from RabbitMQ)
rabbitmq_queue_messages{queue=~".*-inbox"}

# Hangfire job counts
sum(hangfire_jobs_total{state="Enqueued"})
sum(hangfire_jobs_total{state="Processing"})
sum(hangfire_jobs_total{state="Failed"})
```

### 8.3 Combined Dashboard

Create a Grafana dashboard with panels for both API and Worker metrics:

```json
{
  "title": "Wallow - API & Workers",
  "panels": [
    {
      "title": "API Request Rate",
      "type": "timeseries",
      "targets": [
        {
          "expr": "sum(rate(http_server_requests_total{service=\"wallow-api\"}[5m]))",
          "legendFormat": "Requests/s"
        }
      ]
    },
    {
      "title": "API Latency (P95)",
      "type": "timeseries",
      "targets": [
        {
          "expr": "histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket{service=\"wallow-api\"}[5m])) by (le))",
          "legendFormat": "P95"
        }
      ]
    },
    {
      "title": "Worker Message Rate",
      "type": "timeseries",
      "targets": [
        {
          "expr": "sum(rate(wolverine_messages_processed_total{service=\"wallow-worker\"}[5m]))",
          "legendFormat": "Messages/s"
        }
      ]
    },
    {
      "title": "Queue Depths",
      "type": "timeseries",
      "targets": [
        {
          "expr": "rabbitmq_queue_messages{queue=~\".*-inbox\"}",
          "legendFormat": "{{queue}}"
        }
      ]
    },
    {
      "title": "Hangfire Jobs",
      "type": "stat",
      "targets": [
        {
          "expr": "sum(hangfire_jobs_total{state=\"Enqueued\"})",
          "legendFormat": "Enqueued"
        },
        {
          "expr": "sum(hangfire_jobs_total{state=\"Processing\"})",
          "legendFormat": "Processing"
        }
      ]
    },
    {
      "title": "Instance Count",
      "type": "stat",
      "targets": [
        {
          "expr": "count(up{service=\"wallow-api\"})",
          "legendFormat": "API Pods"
        },
        {
          "expr": "count(up{service=\"wallow-worker\"})",
          "legendFormat": "Worker Pods"
        }
      ]
    }
  ]
}
```

**Alert rules:**

```yaml
groups:
  - name: wallow-api
    rules:
      - alert: HighApiLatency
        expr: histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket{service="wallow-api"}[5m])) by (le)) > 1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: API latency is high

      - alert: HighApiErrorRate
        expr: sum(rate(http_server_requests_total{service="wallow-api",status_code=~"5.."}[5m])) / sum(rate(http_server_requests_total{service="wallow-api"}[5m])) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: API error rate above 5%

  - name: wallow-workers
    rules:
      - alert: QueueBacklog
        expr: rabbitmq_queue_messages{queue=~".*-inbox"} > 1000
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: Message queue backlog growing

      - alert: HangfireJobsStalled
        expr: sum(hangfire_jobs_total{state="Enqueued"}) > 500
        for: 15m
        labels:
          severity: warning
        annotations:
          summary: Hangfire job queue growing
```

---

## 9. Graceful Shutdown

### 9.1 API Shutdown

API instances need to drain connections gracefully before stopping.

**Configure shutdown timeout:**

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

builder.Host.ConfigureHostOptions(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});
```

**Health check integration with load balancer:**

```csharp
// Track shutdown state
public class ShutdownTracker
{
    public bool IsShuttingDown { get; private set; }

    public void SignalShutdown() => IsShuttingDown = true;
}

// Register and use
builder.Services.AddSingleton<ShutdownTracker>();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        var tracker = context.RequestServices.GetRequiredService<ShutdownTracker>();
        if (tracker.IsShuttingDown)
        {
            context.Response.StatusCode = 503; // Service Unavailable
        }
        await WriteHealthCheckResponse(context, report);
    }
});

// Signal shutdown early
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var tracker = app.Services.GetRequiredService<ShutdownTracker>();
    tracker.SignalShutdown();

    // Give load balancer time to stop sending requests
    Thread.Sleep(TimeSpan.FromSeconds(10));
});
```

### 9.2 Worker Shutdown

Workers need to finish processing in-progress messages before stopping.

**Wolverine shutdown configuration:**

```csharp
builder.Host.UseWolverine(opts =>
{
    // Graceful shutdown timeout
    opts.Durability.ScheduledJobPollingTime = TimeSpan.FromSeconds(5);
    opts.Durability.FirstMessagePollingInterval = TimeSpan.FromMilliseconds(100);

    // Cancel policies on shutdown
    opts.Policies.OnAnyException()
        .RetryTimes(1)
        .Then.MoveToErrorQueue();
});

// Configure host shutdown
builder.Host.ConfigureHostOptions(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(60); // Longer for workers
});
```

**Hangfire shutdown:**

```csharp
services.AddHangfireServer(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
    options.CancellationCheckInterval = TimeSpan.FromSeconds(5);
});

// In jobs, check cancellation token
public class LongRunningJob
{
    public async Task Execute(PerformContext context, CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessItem(item);
        }
    }
}
```

**Kubernetes termination:**

```yaml
spec:
  containers:
    - name: worker
      lifecycle:
        preStop:
          exec:
            command: ["/bin/sh", "-c", "sleep 5"]
  terminationGracePeriodSeconds: 60
```

The shutdown sequence:
1. Kubernetes sends SIGTERM
2. preStop hook runs (5s delay)
3. Readiness probe starts failing (removed from service)
4. Application begins shutdown
5. Wolverine stops accepting new messages
6. Hangfire stops processing new jobs
7. Wait for in-progress work to complete (up to 60s)
8. Force termination if timeout exceeded

---

## 10. Troubleshooting

### Messages Not Being Processed

**Symptoms:**
- Queue depth growing
- No worker logs showing message processing
- API successfully publishes messages

**Diagnostic steps:**

```bash
# Check if workers are consuming
docker logs wallow-worker-1 | grep "Listening"

# Check RabbitMQ queue bindings
docker exec wallow-rabbitmq rabbitmqctl list_bindings

# Verify Wolverine is configured to consume
docker exec wallow-worker-1 env | grep WALLOW_MODE
```

**Common causes:**

1. **WALLOW_MODE not set to worker:**
   ```bash
   # Fix: Set environment variable
   WALLOW_MODE=worker
   ```

2. **Queue not declared:**
   ```csharp
   // Ensure AutoProvision is enabled
   opts.UseRabbitMq(uri).AutoProvision();
   ```

3. **Handler not discovered:**
   ```csharp
   // Ensure assembly is included
   opts.Discovery.IncludeAssembly(typeof(NotificationHandler).Assembly);
   ```

### Duplicate Processing

**Symptoms:**
- Same message processed multiple times
- Database constraint violations
- Inconsistent data

**Diagnostic steps:**

```bash
# Check for multiple consumers on exclusive queue
docker exec wallow-rabbitmq rabbitmqctl list_consumers

# Check message acknowledgment settings
grep -r "AutoAck" src/
```

**Solutions:**

1. **Implement idempotent handlers:**
   ```csharp
   public async Task Handle(OrderCreatedEvent @event, IDocumentSession session)
   {
       // Check if already processed
       if (await session.Query<Order>().AnyAsync(o => o.Id == @event.OrderId))
       {
           return; // Already processed
       }

       // Process...
   }
   ```

2. **Use Wolverine's built-in deduplication:**
   ```csharp
   opts.Policies.ConfigureConventionalLocalRouting()
       .CustomizeQueues((_, queue) =>
       {
           queue.UseDurableInbox(); // Tracks processed message IDs
       });
   ```

### Worker Not Starting

**Symptoms:**
- Worker pod crashes immediately
- Exit code 1
- No logs after startup

**Diagnostic steps:**

```bash
# Check container logs
docker logs wallow-worker-1

# Check if dependencies are healthy
docker exec wallow-worker-1 curl -s http://postgres:5432 || echo "Postgres unreachable"
docker exec wallow-worker-1 curl -s http://rabbitmq:5672 || echo "RabbitMQ unreachable"
```

**Common causes:**

1. **Database connection failure:**
   ```
   Error: Connection refused to postgres:5432
   ```
   Fix: Ensure `depends_on` conditions are met

2. **Missing configuration:**
   ```
   Error: ConnectionStrings__DefaultConnection not configured
   ```
   Fix: Check environment variables are passed

3. **Handler startup error:**
   ```
   Error: Could not find handler for MessageType
   ```
   Fix: Ensure handler assemblies are included

### Queue Buildup

**Symptoms:**
- Messages accumulating faster than processing
- Queue depth growing over time
- Memory pressure on RabbitMQ

**Diagnostic steps:**

```bash
# Check queue message rates
docker exec wallow-rabbitmq rabbitmqctl list_queues name messages messages_ready messages_unacknowledged

# Check worker processing rate
docker logs wallow-worker-1 | grep "processed" | tail -100

# Check for slow handlers
docker logs wallow-worker-1 | grep -E "took [0-9]{4,}ms"
```

**Solutions:**

1. **Scale workers:**
   ```bash
   docker compose up -d --scale worker=4
   ```

2. **Increase parallelism:**
   ```csharp
   opts.ListenToRabbitQueue("communications-inbox")
       .MaximumParallelMessages(20);
   ```

3. **Optimize slow handlers:**
   - Add caching for repeated lookups
   - Batch database operations
   - Move heavy processing to separate queue

4. **Add priority queues:**
   - Route time-sensitive messages to faster queues
   - Dedicate workers to critical queues

### Connection Exhaustion

**Symptoms:**
- "Connection pool exhausted" errors
- "Too many connections" from PostgreSQL
- Workers hang on database operations

**Diagnostic steps:**

```bash
# Check PostgreSQL connections
docker exec wallow-postgres psql -U wallow -c "SELECT count(*) FROM pg_stat_activity;"

# Check per-service connections
docker exec wallow-postgres psql -U wallow -c "SELECT application_name, count(*) FROM pg_stat_activity GROUP BY application_name;"
```

**Solutions:**

1. **Configure connection pool per instance:**
   ```csharp
   // Smaller pool per instance since we have many instances
   services.AddDbContext<AppDbContext>(options =>
       options.UseNpgsql(connectionString, npgsql =>
       {
           npgsql.MinBatchSize(1);
       })
       .EnableConnectionPool(maxPoolSize: 10)); // Per instance
   ```

2. **Use connection multiplexing:**
   ```
   Host=postgres;Port=5432;Database=wallow;Username=wallow;Password=xxx;
   Pooling=true;MinPoolSize=5;MaxPoolSize=20;ConnectionLifetime=300
   ```

3. **Implement connection timeouts:**
   ```csharp
   opts.UseNpgsql(connectionString, npgsql =>
   {
       npgsql.CommandTimeout(30);
   });
   ```

---

## Quick Reference

### Commands

```bash
# Start with separated services
docker compose -f docker-compose.separated.yml up -d

# Scale API instances
docker compose -f docker-compose.separated.yml up -d --scale api=4

# Scale workers
docker compose -f docker-compose.separated.yml up -d --scale worker=3

# View API logs
docker compose -f docker-compose.separated.yml logs -f api

# View worker logs
docker compose -f docker-compose.separated.yml logs -f worker

# Check queue depths
docker exec wallow-rabbitmq rabbitmqctl list_queues name messages

# Check Hangfire jobs
docker exec wallow-postgres psql -U wallow -c "SELECT state_name, count(*) FROM hangfire.job GROUP BY state_name;"
```

### Environment Variables

| Variable | API | Worker | Description |
|----------|-----|--------|-------------|
| `WALLOW_MODE` | `api` | `worker` | Operating mode |
| `ASPNETCORE_URLS` | `http://+:8080` | `http://+:8081` | Listen address |
| `ConnectionStrings__DefaultConnection` | Yes | Yes | PostgreSQL |
| `ConnectionStrings__RabbitMq` | Yes | Yes | RabbitMQ |
| `ConnectionStrings__Redis` | Yes | Yes | Redis/Valkey |
| `Hangfire__WorkerCount` | N/A | `8` | Parallel job count |
| `OpenTelemetry__ServiceName` | `wallow-api` | `wallow-worker` | Tracing service name |

### Health Endpoints

| Endpoint | API | Worker | Purpose |
|----------|-----|--------|---------|
| `/health` | :8080 | :8081 | Full health check |
| `/health/ready` | :8080 | :8081 | Readiness probe |
| `/health/live` | :8080 | :8081 | Liveness probe |
| `/hangfire` | :8080 | N/A | Job dashboard |
| `/scalar/v1` | :8080 | N/A | API documentation |
