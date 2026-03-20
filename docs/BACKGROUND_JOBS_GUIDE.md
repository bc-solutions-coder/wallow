# Wallow Background Jobs Guide

This guide covers background processing in Wallow, including Hangfire configuration, job patterns, scheduling, error handling, and when to choose Hangfire versus Wolverine for background work.

## Table of Contents

1. [Overview](#1-overview)
2. [Hangfire Configuration](#2-hangfire-configuration)
3. [Job Types](#3-job-types)
4. [Creating Jobs](#4-creating-jobs)
5. [Recurring Jobs](#5-recurring-jobs)
6. [Error Handling](#6-error-handling)
7. [Best Practices](#7-best-practices)
8. [Wolverine vs Hangfire](#8-wolverine-vs-hangfire)

---

## 1. Overview

Wallow uses **Hangfire** for scheduled and recurring background jobs. Hangfire provides:

- **Persistent job storage** in PostgreSQL
- **Dashboard UI** for monitoring and managing jobs
- **Automatic retries** with configurable policies
- **Recurring jobs** with cron expressions
- **Fire-and-forget** execution

### When to Use Hangfire

Use Hangfire for:
- **Scheduled tasks** that must run at specific times (e.g., daily cleanup at 2 AM)
- **Recurring jobs** with cron schedules (e.g., flush usage counters every 5 minutes)
- **Deferred execution** (e.g., send reminder in 1 hour)
- **Jobs that don't depend on events** from other modules

### When to Use Wolverine Instead

Use Wolverine for:
- **Event-driven processing** (reacting to domain events)
- **Cross-module communication** (integration events)
- **Commands requiring immediate response** (via `InvokeAsync`)
- **Saga/workflow orchestration**

See [Section 8](#8-wolverine-vs-hangfire) for a detailed comparison.

---

## 2. Hangfire Configuration

### Service Registration

Hangfire is configured in `Program.cs` via the `AddHangfireServices` extension:

```csharp
// src/Wallow.Api/Extensions/HangfireExtensions.cs
public static IServiceCollection AddHangfireServices(
    this IServiceCollection services, IConfiguration configuration)
{
    string connectionString = configuration.GetConnectionString("DefaultConnection")!;

    services.AddHangfire(config =>
    {
        config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(opts =>
                opts.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = "hangfire"
                });
    });

    services.AddHangfireServer();

    return services;
}
```

### PostgreSQL Storage

Hangfire stores jobs in the `hangfire` PostgreSQL schema. Tables include:

| Table | Purpose |
|-------|---------|
| `hangfire.job` | Job definitions and state |
| `hangfire.state` | Job state history |
| `hangfire.jobqueue` | Queued jobs waiting to run |
| `hangfire.server` | Active Hangfire server instances |
| `hangfire.set` | Recurring job metadata |
| `hangfire.hash` | Job parameters and metadata |

### Dashboard Setup

The Hangfire dashboard is available at `/hangfire`:

```csharp
// src/Wallow.Api/Extensions/HangfireExtensions.cs
public static WebApplication UseHangfireDashboard(this WebApplication app)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthFilter(app.Environment)],
        DashboardTitle = "Wallow Jobs"
    });

    return app;
}
```

### Dashboard Authorization

The dashboard is protected by `HangfireDashboardAuthFilter`:

```csharp
// src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    private readonly IWebHostEnvironment _environment;

    public HangfireDashboardAuthFilter(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public bool Authorize(DashboardContext context)
    {
        // Allow all access in development
        if (_environment.IsDevelopment())
            return true;

        // In production, require authenticated Admin role
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("Admin");
    }
}
```

**Access URLs:**

| Environment | URL | Access |
|-------------|-----|--------|
| Development | http://localhost:5000/hangfire | Open to all |
| Production | https://your-domain/hangfire | Admin role required |

---

## 3. Job Types

Hangfire supports four job types, each suited for different scenarios.

### Fire-and-Forget Jobs

Execute immediately in the background. Use when you need to offload work from the current request without waiting:

```csharp
// Enqueue a job to run as soon as possible
BackgroundJob.Enqueue<GenerateReportJob>(
    job => job.Execute(reportId));

// Using IBackgroundJobClient (injectable)
_backgroundJobClient.Enqueue<SendNotificationJob>(
    job => job.SendAsync(userId, message));
```

### Delayed Jobs

Execute after a specified delay:

```csharp
// Schedule a job to run in 1 hour
BackgroundJob.Schedule<SendReminderJob>(
    job => job.Execute(invoiceId),
    TimeSpan.FromHours(1));

// Schedule for a specific time
BackgroundJob.Schedule<ProcessBatchJob>(
    job => job.Execute(batchId),
    DateTimeOffset.UtcNow.AddDays(1));
```

### Recurring Jobs

Execute on a cron schedule. This is the most common pattern in Wallow:

```csharp
// Every 5 minutes - flush usage counters from cache to database
RecurringJob.AddOrUpdate<FlushUsageJob>(
    "flush-usage",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *");

// Daily at 2 AM UTC
RecurringJob.AddOrUpdate<CleanupExpiredTokensJob>(
    "cleanup-expired-tokens",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 2 * * *");

// Every 5 minutes - system heartbeat
RecurringJob.AddOrUpdate<SystemHeartbeatJob>(
    "system-heartbeat",
    job => job.ExecuteAsync(),
    "*/5 * * * *");
```

### Continuation Jobs

Execute after another job completes. Useful for job chains:

```csharp
// Job B runs after Job A completes
var jobAId = BackgroundJob.Enqueue<PrepareDataJob>(
    job => job.Execute(dataId));

BackgroundJob.ContinueJobWith<ProcessDataJob>(
    jobAId,
    job => job.Execute(dataId));

// Chain multiple jobs
var step1 = BackgroundJob.Enqueue(() => Step1());
var step2 = BackgroundJob.ContinueJobWith(step1, () => Step2());
var step3 = BackgroundJob.ContinueJobWith(step2, () => Step3());
```

---

## 4. Creating Jobs

### Job Class Pattern

Jobs in Wallow follow a consistent pattern:

1. **Constructor injection** for dependencies
2. **`ExecuteAsync` method** with `CancellationToken`
3. **`[AutomaticRetry]` attribute** for error handling
4. **Structured logging** throughout

**Example: Billing Usage Flush Job**

```csharp
// src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/FlushUsageJob.cs
public class FlushUsageJob
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<FlushUsageJob> _logger;

    public FlushUsageJob(
        IConnectionMultiplexer redis,
        IUsageRecordRepository usageRepository,
        IMessageBus messageBus,
        ILogger<FlushUsageJob> logger)
    {
        _redis = redis;
        _usageRepository = usageRepository;
        _messageBus = messageBus;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting usage flush job");
        var flushedCount = await FlushCountersToDatabase(ct);

        if (flushedCount > 0)
        {
            await _messageBus.PublishAsync(new UsageFlushedEvent(DateTime.UtcNow, flushedCount));
        }

        _logger.LogInformation("Flushed {Count} records", flushedCount);
    }
}
```

**Example: Simple Cleanup Job**

```csharp
public class CleanupExpiredTokensJob
{
    private readonly ITokenRepository _tokenRepository;
    private readonly ILogger<CleanupExpiredTokensJob> _logger;

    public CleanupExpiredTokensJob(
        ITokenRepository tokenRepository,
        ILogger<CleanupExpiredTokensJob> logger)
    {
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Running token cleanup job");

        try
        {
            var removed = await _tokenRepository.RemoveExpiredAsync(ct);
            _logger.LogInformation("Token cleanup completed, removed {Count} expired tokens", removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token cleanup job failed");
            throw; // Re-throw to trigger retry
        }
    }
}
```

### Dependency Injection

Jobs are resolved from the DI container. Register job classes in module extensions:

```csharp
// src/Modules/Billing/Wallow.Billing.Infrastructure/Extensions/BillingInfrastructureExtensions.cs
private static IServiceCollection AddBillingBackgroundJobs(this IServiceCollection services)
{
    // Register job classes
    services.AddScoped<FlushUsageJob>();

    return services;
}
```

### Job Parameters and Serialization

Job parameters are serialized to JSON and stored in PostgreSQL. Use simple types:

```csharp
// Good - simple types serialize well
public async Task ProcessOrderAsync(Guid orderId, string status, CancellationToken ct)

// Avoid - complex objects may have serialization issues
public async Task ProcessOrderAsync(Order order, CancellationToken ct) // Don't do this
```

For complex data, pass IDs and retrieve from the database:

```csharp
public class ProcessOrderJob
{
    private readonly IOrderRepository _repository;

    public ProcessOrderJob(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            // Order may have been deleted - log and exit gracefully
            return;
        }

        // Process order...
    }
}
```

---

## 5. Recurring Jobs

### Registration Pattern

Wallow provides the `IJobScheduler` abstraction in `Shared.Kernel` for scheduling background jobs. The implementation uses Hangfire and is registered in `Shared.Infrastructure`:

```csharp
// src/Shared/Wallow.Shared.Kernel/BackgroundJobs/IJobScheduler.cs
public interface IJobScheduler
{
    string Enqueue(Expression<Func<Task>> job);
    string Enqueue<T>(Expression<Func<T, Task>> job);
    void AddRecurring(string id, string cron, Expression<Func<Task>> job);
    void RemoveRecurring(string id);
}
```

The Hangfire implementation:

```csharp
// src/Shared/Wallow.Shared.Infrastructure/BackgroundJobs/HangfireJobScheduler.cs
public sealed class HangfireJobScheduler : IJobScheduler
{
    public string Enqueue(Expression<Func<Task>> job) =>
        BackgroundJob.Enqueue(job);

    public string Enqueue<T>(Expression<Func<T, Task>> job) =>
        BackgroundJob.Enqueue(job);

    public void AddRecurring(string id, string cron, Expression<Func<Task>> job) =>
        RecurringJob.AddOrUpdate(id, job, cron);

    public void RemoveRecurring(string id) =>
        RecurringJob.RemoveIfExists(id);
}
```

Modules can register recurring jobs in their infrastructure extensions or use the `IJobScheduler` directly:

```csharp
// Example: Registering a recurring job in a module
public static class BillingInfrastructureExtensions
{
    public static IServiceCollection AddBillingBackgroundJobs(this IServiceCollection services)
    {
        services.AddScoped<FlushUsageJob>();
        return services;
    }
}
```

### Application Startup

Recurring jobs are registered directly in `Program.cs` using `IRecurringJobManager` via a scoped DI call:

```csharp
// In Program.cs — use DI-based IRecurringJobManager, not the static RecurringJob class
await using (AsyncServiceScope jobScope = app.Services.CreateAsyncScope())
{
    IRecurringJobManager jobManager = jobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    jobManager.AddOrUpdate<SystemHeartbeatJob>(
        "system-heartbeat",
        job => job.ExecuteAsync(),
        "*/5 * * * *");

    jobManager.AddOrUpdate<RetryFailedEmailsJob>(
        "retry-failed-emails",
        job => job.ExecuteAsync(CancellationToken.None),
        "*/5 * * * *");
}
```

### Cron Expression Reference

Wallow uses standard 5-field cron expressions:

```
┌───────────── minute (0-59)
│ ┌───────────── hour (0-23)
│ │ ┌───────────── day of month (1-31)
│ │ │ ┌───────────── month (1-12)
│ │ │ │ ┌───────────── day of week (0-6, Sunday = 0)
│ │ │ │ │
* * * * *
```

**Common Expressions:**

| Expression | Description | Example Usage |
|------------|-------------|---------------|
| `* * * * *` | Every minute | Check for scheduled announcements |
| `*/5 * * * *` | Every 5 minutes | Flush usage counters, heartbeat |
| `*/15 * * * *` | Every 15 minutes | Execute scheduled reports |
| `0 * * * *` | Every hour (on the hour) | Cleanup expired data |
| `0 2 * * *` | Daily at 2 AM | Token cleanup |
| `0 3 * * *` | Daily at 3 AM | Data maintenance |
| `0 0 * * 0` | Weekly on Sunday at midnight | Weekly reports |
| `0 0 1 * *` | Monthly on the 1st at midnight | Monthly billing |

**Hangfire Helpers:**

```csharp
Cron.Minutely        // "* * * * *"
Cron.Hourly          // "0 * * * *"
Cron.Daily           // "0 0 * * *"
Cron.Weekly          // "0 0 * * 0"
Cron.Monthly         // "0 0 1 * *"
Cron.Yearly          // "0 0 1 1 *"
Cron.Daily(hour: 3)  // "0 3 * * *" (daily at 3 AM)
Cron.Weekly(DayOfWeek.Monday, hour: 9)  // "0 9 * * 1"
```

### Managing Schedules

Jobs can be updated at runtime:

```csharp
// Update schedule
_recurringJobManager.AddOrUpdate(
    "my-job",
    () => ExecuteJobAsync(),
    "0 4 * * *"); // Changed to 4 AM

// Disable a job (remove from schedule)
_recurringJobManager.RemoveIfExists("my-job");

// Trigger immediately (runs once, doesn't affect schedule)
_recurringJobManager.Trigger("my-job");
```

---

## 6. Error Handling

### Automatic Retry

Use `[AutomaticRetry]` to configure retry behavior:

```csharp
// Default: 10 retries with exponential backoff
public async Task ExecuteAsync(CancellationToken ct)

// Custom retry count
[AutomaticRetry(Attempts = 3)]
public async Task ExecuteAsync(CancellationToken ct)

// Disable retries
[AutomaticRetry(Attempts = 0)]
public async Task ExecuteAsync(CancellationToken ct)

// Retry only on specific exceptions
[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
public async Task ExecuteAsync(CancellationToken ct)
```

**Retry Behavior:**

| Attempt | Default Delay |
|---------|---------------|
| 1st retry | ~30 seconds |
| 2nd retry | ~1 minute |
| 3rd retry | ~3 minutes |
| 4th retry | ~10 minutes |
| 5th retry | ~30 minutes |

### Job-Level Error Handling

Handle errors within the job to prevent unnecessary retries:

```csharp
[AutomaticRetry(Attempts = 3)]
public async Task ExecuteAsync(CancellationToken ct = default)
{
    _logger.LogInformation("Running export expiration job");

    var expiredRequests = await _repository.GetExpiredExportsAsync(ct);

    foreach (var request in expiredRequests)
    {
        try
        {
            // Per-item error handling - don't fail the whole job
            await CleanupExportAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup export {RequestId}", request.Id.Value);
            // Continue processing other requests
        }
    }

    await _repository.SaveChangesAsync(ct);
    _logger.LogInformation("Export expiration job completed");
}
```

### Failed Job Management

Failed jobs (after all retries exhausted) appear in the Hangfire dashboard under "Failed":

**Dashboard location:** `/hangfire/failed`

**Actions:**
- **Requeue** - Retry the job immediately
- **Delete** - Remove the job permanently
- **View details** - See exception stack trace and parameters

### Monitoring Failed Jobs

Query failed jobs from PostgreSQL:

```sql
-- View recent failed jobs
SELECT
    j.id,
    j.invocation_data,
    s.name AS current_state,
    s.reason AS failure_reason,
    s.created_at
FROM hangfire.job j
JOIN hangfire.state s ON j.state_id = s.id
WHERE s.name = 'Failed'
ORDER BY s.created_at DESC
LIMIT 20;
```

**Alerting:**

Consider monitoring the failed job count and alerting when it exceeds a threshold:

```sql
-- Count of failed jobs in last 24 hours
SELECT COUNT(*)
FROM hangfire.job j
JOIN hangfire.state s ON j.state_id = s.id
WHERE s.name = 'Failed'
  AND s.created_at > NOW() - INTERVAL '24 hours';
```

---

## 7. Best Practices

### Job Idempotency

Jobs may run multiple times (retries, server restarts). Design for idempotency:

```csharp
public async Task ProcessPaymentAsync(Guid paymentId, CancellationToken ct)
{
    var payment = await _repository.GetByIdAsync(paymentId, ct);

    // Check if already processed
    if (payment.Status == PaymentStatus.Processed)
    {
        _logger.LogInformation("Payment {PaymentId} already processed, skipping", paymentId);
        return;
    }

    // Process...
    payment.MarkProcessed();
    await _repository.SaveChangesAsync(ct);
}
```

### Long-Running Jobs

For jobs that may take a long time:

1. **Check cancellation token** regularly:

```csharp
public async Task ProcessBatchAsync(CancellationToken ct)
{
    var items = await _repository.GetPendingItemsAsync(ct);

    foreach (var item in items)
    {
        // Check for cancellation (e.g., during shutdown)
        ct.ThrowIfCancellationRequested();

        await ProcessItemAsync(item, ct);
    }
}
```

2. **Use batching** to avoid memory issues:

```csharp
public async Task ProcessAllRecordsAsync(CancellationToken ct)
{
    const int batchSize = 100;
    int processed = 0;

    while (true)
    {
        ct.ThrowIfCancellationRequested();

        var batch = await _repository.GetBatchAsync(skip: processed, take: batchSize, ct);
        if (batch.Count == 0)
            break;

        foreach (var record in batch)
        {
            await ProcessRecordAsync(record, ct);
        }

        processed += batch.Count;
        _logger.LogInformation("Processed {Count} records so far", processed);
    }
}
```

3. **Consider progress tracking** for very long jobs:

```csharp
public async Task GenerateLargeReportAsync(Guid reportId, CancellationToken ct)
{
    var report = await _repository.GetByIdAsync(reportId, ct);
    report.UpdateProgress(0, "Starting...");
    await _repository.SaveChangesAsync(ct);

    // ... do work, updating progress periodically ...

    report.UpdateProgress(50, "Processing data...");
    await _repository.SaveChangesAsync(ct);

    // ... more work ...

    report.Complete();
    await _repository.SaveChangesAsync(ct);
}
```

### Resource Cleanup

Always clean up resources, especially in jobs that may be interrupted:

```csharp
public async Task ProcessFilesAsync(CancellationToken ct)
{
    var tempFiles = new List<string>();

    try
    {
        // Create temp files
        tempFiles.Add(await CreateTempFileAsync(ct));

        // Process...

        // Cleanup on success
        foreach (var file in tempFiles)
        {
            File.Delete(file);
        }
    }
    catch
    {
        // Cleanup on failure
        foreach (var file in tempFiles)
        {
            try { File.Delete(file); } catch { }
        }
        throw;
    }
}
```

### Tenant Context in Jobs

For multi-tenant jobs that need to process data for a specific tenant, create a scope and set the tenant context:

```csharp
[AutomaticRetry(Attempts = 0)]
public async Task ExecuteJobAsync(Guid tenantId)
{
    using var serviceScope = _serviceProvider.CreateScope();
    var tenantContext = serviceScope.ServiceProvider.GetRequiredService<ITenantContext>();
    tenantContext.SetTenant(TenantId.Create(tenantId));

    // Now all tenant-scoped queries will filter by this tenant
    await ExecuteJobLogicAsync(serviceScope, ct);
}
```

For jobs that process all tenants:

```csharp
public async Task ProcessAllTenantsAsync(CancellationToken ct)
{
    var tenants = await _tenantRepository.GetAllActiveAsync(ct);

    foreach (var tenant in tenants)
    {
        using var scope = _serviceProvider.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetTenant(tenant.Id);

        try
        {
            await ProcessTenantDataAsync(scope, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process tenant {TenantId}", tenant.Id);
            // Continue with other tenants
        }
    }
}
```

---

## 8. Wolverine vs Hangfire

Wallow uses both Wolverine and Hangfire for background processing. Understanding when to use each is crucial.

### Quick Decision Matrix

| Scenario | Use |
|----------|-----|
| React to domain event (e.g., user registered) | Wolverine |
| Scheduled task (e.g., daily cleanup) | Hangfire |
| Cross-module notification (e.g., invoice created) | Wolverine |
| Recurring maintenance (e.g., expire reservations) | Hangfire |
| Long-running workflow/saga | Wolverine |
| Deferred execution (e.g., send reminder in 1 hour) | Hangfire |
| Immediate background offload | Either |

### Wolverine: Event-Driven Processing

Wolverine excels at reacting to events:

```csharp
// Handler reacts to integration event from another module
public sealed class UserRegisteredEventHandler
{
    public static async Task HandleAsync(
        UserRegisteredEvent integrationEvent,
        INotificationRepository notificationRepository,
        INotificationService notificationService,
        ILogger<UserRegisteredEventHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("User {UserId} registered, creating welcome notification", integrationEvent.UserId);

        var notification = Notification.Create(
            tenantContext.TenantId,
            integrationEvent.UserId,
            NotificationType.SystemAlert,
            "Welcome to Wallow!",
            $"Hi {integrationEvent.FirstName}, welcome!");

        notificationRepository.Add(notification);
        await notificationRepository.SaveChangesAsync(cancellationToken);
    }
}
```

**Wolverine strengths:**
- Automatic handler discovery
- Built-in retry policies with dead letter queue
- Durable outbox for reliable delivery
- RabbitMQ integration for distributed processing
- Saga support for workflows

### Hangfire: Time-Based Processing

Hangfire excels at scheduled work:

```csharp
// Job runs on a schedule, not triggered by events
// src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/FlushUsageJob.cs
public class FlushUsageJob
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<FlushUsageJob> _logger;

    public FlushUsageJob(
        IConnectionMultiplexer redis,
        IUsageRecordRepository usageRepository,
        IMessageBus messageBus,
        ILogger<FlushUsageJob> logger)
    {
        _redis = redis;
        _usageRepository = usageRepository;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting usage flush job");
        var flushedCount = await FlushCountersToDatabase(ct);

        // Publish event for other modules to react to
        if (flushedCount > 0)
        {
            await _messageBus.PublishAsync(new UsageFlushedEvent(DateTime.UtcNow, flushedCount));
        }

        _logger.LogInformation("Flushed {Count} records", flushedCount);
    }
}
```

**Hangfire strengths:**
- Visual dashboard for monitoring
- Cron scheduling with persistent state
- Delayed execution (run in X minutes)
- Job continuations (A then B then C)
- Manual job triggering via UI

### Hybrid Approach

Sometimes a job uses both:

```csharp
// Hangfire job that publishes Wolverine events
// src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/FlushUsageJob.cs
public class FlushUsageJob
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IUsageRecordRepository _usageRepository;
    private readonly IMessageBus _messageBus;  // Wolverine
    private readonly ILogger<FlushUsageJob> _logger;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting usage flush job");
        var flushedCount = await FlushCountersToDatabase(ct);

        // Hangfire job publishes Wolverine event
        if (flushedCount > 0)
        {
            await _messageBus.PublishAsync(new UsageFlushedEvent(DateTime.UtcNow, flushedCount));
        }

        _logger.LogInformation("Flushed {Count} records", flushedCount);
    }
}
```

### Architecture Summary

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Background Processing                        │
├─────────────────────────────────┬───────────────────────────────────┤
│           Wolverine             │            Hangfire               │
├─────────────────────────────────┼───────────────────────────────────┤
│ • Event handlers                │ • Recurring jobs (cron)           │
│ • Integration event consumers   │ • Delayed execution               │
│ • Domain event handlers         │ • Scheduled tasks                 │
│ • Sagas/workflows               │ • Job continuations               │
│ • Cross-module messaging        │ • Time-based maintenance          │
├─────────────────────────────────┼───────────────────────────────────┤
│ Storage: RabbitMQ + PostgreSQL  │ Storage: PostgreSQL               │
│ Trigger: Events/Messages        │ Trigger: Time/Schedule            │
│ Discovery: Automatic            │ Discovery: Explicit registration  │
└─────────────────────────────────┴───────────────────────────────────┘
```

---

## Quick Reference

### Module Job Registration Checklist

1. **Create job class** in `Infrastructure/BackgroundJobs/`:
   ```csharp
   public class MyModuleCleanupJob
   {
       [AutomaticRetry(Attempts = 3)]
       public async Task ExecuteAsync(CancellationToken ct = default) { ... }
   }
   ```

2. **Register job class** in module extensions:
   ```csharp
   services.AddScoped<MyModuleCleanupJob>();
   ```

3. **Create recurring job registration**:
   ```csharp
   public class MyModuleRecurringJobRegistration : IRecurringJobRegistration
   {
       private readonly IRecurringJobManager _jobs;

       public MyModuleRecurringJobRegistration(IRecurringJobManager jobs)
       {
           _jobs = jobs;
       }

       public void RegisterJobs()
       {
           _jobs.AddOrUpdate<MyModuleCleanupJob>(
               "my-module-cleanup",
               job => job.ExecuteAsync(CancellationToken.None),
               "0 3 * * *"); // Daily at 3 AM
       }
   }
   ```

4. **Register the registration class**:
   ```csharp
   services.AddSingleton<IRecurringJobRegistration, MyModuleRecurringJobRegistration>();
   ```

### Common Cron Patterns

| Pattern | Expression |
|---------|------------|
| Every minute | `* * * * *` or `Cron.Minutely` |
| Every 5 minutes | `*/5 * * * *` |
| Every 15 minutes | `*/15 * * * *` |
| Every hour | `0 * * * *` or `Cron.Hourly` |
| Every 6 hours | `0 */6 * * *` |
| Daily at midnight | `0 0 * * *` or `Cron.Daily` |
| Daily at 2 AM | `0 2 * * *` or `Cron.Daily(2)` |
| Weekdays at 9 AM | `0 9 * * 1-5` |
| Weekly on Sunday | `0 0 * * 0` or `Cron.Weekly` |
| Monthly on 1st | `0 0 1 * *` or `Cron.Monthly` |

### Dashboard Access

| Environment | URL | Credentials |
|-------------|-----|-------------|
| Development | http://localhost:5000/hangfire | None required |
| Staging | https://staging.example.com/hangfire | Admin role |
| Production | https://api.example.com/hangfire | Admin role |
