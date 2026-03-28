# Background Jobs

This guide covers background processing in Wallow using Hangfire for scheduled and recurring jobs, alongside Wolverine for event-driven processing.

## Overview

Wallow uses **Hangfire** for scheduled and recurring background jobs, backed by PostgreSQL. Hangfire provides persistent job storage, a dashboard UI, automatic retries, cron scheduling, and fire-and-forget execution.

**Use Hangfire for** time-based work: scheduled tasks, recurring cron jobs, deferred execution, and jobs not triggered by events.

**Use Wolverine for** event-driven work: reacting to domain events, cross-module integration events, immediate command processing, and saga orchestration.

## Hangfire Configuration

### Service Registration

Hangfire is configured via `AddHangfireServices` in `src/Wallow.Api/Extensions/HangfireExtensions.cs`. It connects to PostgreSQL using the `DefaultConnection` connection string and stores jobs in the `hangfire` schema.

The Hangfire server is registered with shutdown/stop timeouts for graceful termination during deployments.

### Dashboard

The Hangfire dashboard is available at `/hangfire`, protected by `HangfireDashboardAuthFilter` (`src/Wallow.Api/Middleware/HangfireDashboardAuthFilter.cs`). In development, access is open. In production, it requires an authenticated user with the `admin` role.

| Environment | URL | Access |
|-------------|-----|--------|
| Development | http://localhost:5000/hangfire | Open to all |
| Production | https://your-domain/hangfire | Admin role required |

## IJobScheduler Abstraction

Wallow provides an `IJobScheduler` abstraction in `src/Shared/Wallow.Shared.Kernel/BackgroundJobs/IJobScheduler.cs` for enqueuing and scheduling jobs without depending on Hangfire directly. The Hangfire implementation lives in `src/Shared/Wallow.Shared.Infrastructure.BackgroundJobs/HangfireJobScheduler.cs`.

## Recurring Jobs

Recurring jobs are registered at startup in `Program.cs` using `IRecurringJobManager` via a scoped DI call:

| Job | Schedule | Location |
|-----|----------|----------|
| `SystemHeartbeatJob` | Every 5 minutes | `src/Wallow.Api/Jobs/SystemHeartbeatJob.cs` |
| `RetryFailedEmailsJob` | Every 5 minutes (feature-flagged) | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Jobs/RetryFailedEmailsJob.cs` |
| `OpenIddictTokenPruningJob` | Every 4 hours | `src/Modules/Identity/Wallow.Identity.Infrastructure/Jobs/OpenIddictTokenPruningJob.cs` |
| `ExpiredInvitationPruningJob` | Every hour | `src/Modules/Identity/Wallow.Identity.Infrastructure/Jobs/ExpiredInvitationPruningJob.cs` |
| `FlushUsageJob` | Every 5 minutes | `src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/FlushUsageJob.cs` |

The `RetryFailedEmailsJob` is conditionally registered behind the `Modules.Notifications` feature flag.

### Cron Expression Reference

| Expression | Description |
|------------|-------------|
| `*/5 * * * *` | Every 5 minutes |
| `0 * * * *` | Every hour |
| `0 */4 * * *` | Every 4 hours |
| `0 2 * * *` | Daily at 2 AM |
| `0 0 * * 0` | Weekly on Sunday at midnight |
| `0 0 1 * *` | Monthly on the 1st at midnight |

Hangfire also provides helpers: `Cron.Minutely`, `Cron.Hourly`, `Cron.Daily`, `Cron.Weekly`, `Cron.Monthly`, `Cron.Yearly`, and overloads like `Cron.Daily(hour: 3)`.

## Job Patterns

### Job Class Structure

Jobs use constructor injection, an `ExecuteAsync` method, and structured logging via `[LoggerMessage]` source generators. The `FlushUsageJob` is a representative example: it reads Valkey counters, performs atomic get-and-reset, persists to PostgreSQL within a tenant context, and publishes a Wolverine event on completion.

### Error Handling

Use `[AutomaticRetry]` to configure retry behavior. The default is 10 retries with exponential backoff. Set `Attempts = 0` to disable retries or `Attempts = 3` for a limited count.

Per-item error handling within batch jobs prevents a single failure from aborting the entire run.

### Job Parameters

Job parameters are serialized to JSON and stored in PostgreSQL. Pass simple types (IDs, strings) rather than complex objects. Retrieve full entities from the database inside the job.

## Best Practices

- **Idempotency**: Jobs may run multiple times due to retries or server restarts. Check state before processing.
- **Cancellation**: Check `CancellationToken` regularly in long-running jobs.
- **Tenant context**: For tenant-scoped jobs, use `ITenantContextFactory.CreateScope(tenantId)` to establish the tenant context before executing queries.
- **Batching**: Process large datasets in batches to control memory usage.

## Wolverine vs Hangfire

| Scenario | Use |
|----------|-----|
| React to domain event | Wolverine |
| Scheduled task (cron) | Hangfire |
| Cross-module notification | Wolverine |
| Recurring maintenance | Hangfire |
| Long-running workflow/saga | Wolverine |
| Deferred execution (run in X minutes) | Hangfire |

The two systems complement each other. Hangfire jobs can publish Wolverine events upon completion (as `FlushUsageJob` does), bridging time-based triggers with event-driven processing.
