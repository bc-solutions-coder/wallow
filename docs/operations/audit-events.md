# Audit Events

Wallow records security-relevant authentication events to a dedicated `auth_audit` PostgreSQL schema. These records are append-only and written independently of the main module schemas, so a failure to write an audit entry never fails the originating request.

## Table Schema

Events are stored in `auth_audit.auth_audit_entries`.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | `uuid` | No | Primary key, generated per event |
| `event_type` | `text` | No | String identifier for the event (see below) |
| `user_id` | `uuid` | No | The user the event relates to |
| `tenant_id` | `uuid` | No | The tenant the event relates to |
| `ip_address` | `text` | Yes | Client IP address, when available |
| `user_agent` | `text` | Yes | HTTP User-Agent header, when available |
| `occurred_at` | `timestamp with time zone` | No | UTC timestamp; defaults to `now()` at insert |

The table is created by the `InitialAuthAudit` EF Core migration in `Wallow.Shared.Infrastructure.Core`.

## Event Types

The `event_type` column uses plain string values. The following events are recorded by default.

| `event_type` | Trigger | IP recorded |
|--------------|---------|-------------|
| `LoginSucceeded` | A user successfully authenticates | Yes |
| `LoginFailed` | A login attempt is rejected (wrong password, unknown user, etc.) | Yes |
| `AccountLockedOut` | A user account is locked after repeated failed login attempts | Yes |
| `MfaLockedOut` | A user is locked out after repeated MFA failures | No |

Each event is written by `AuthAuditEventHandlers` in the Identity module, which subscribes to the corresponding Wolverine in-memory integration events published by the Identity domain.

| `event_type` | Source integration event |
|--------------|--------------------------|
| `LoginSucceeded` | `UserLoginSucceededEvent` |
| `LoginFailed` | `UserLoginFailedEvent` |
| `AccountLockedOut` | `UserAccountLockedOutEvent` |
| `MfaLockedOut` | `UserMfaLockedOutEvent` |

## Querying Events

All examples use the `auth_audit` schema. Substitute real UUIDs for `$user_id` and `$tenant_id`.

**Recent logins for a user:**

```sql
SELECT id, event_type, ip_address, occurred_at
FROM auth_audit.auth_audit_entries
WHERE user_id = '$user_id'
  AND event_type = 'LoginSucceeded'
ORDER BY occurred_at DESC
LIMIT 50;
```

**Failed login attempts in the last 24 hours (across a tenant):**

```sql
SELECT user_id, ip_address, COUNT(*) AS attempts
FROM auth_audit.auth_audit_entries
WHERE tenant_id = '$tenant_id'
  AND event_type = 'LoginFailed'
  AND occurred_at >= now() - INTERVAL '24 hours'
GROUP BY user_id, ip_address
ORDER BY attempts DESC;
```

**All security events for a user (ordered most recent first):**

```sql
SELECT event_type, ip_address, user_agent, occurred_at
FROM auth_audit.auth_audit_entries
WHERE user_id = '$user_id'
ORDER BY occurred_at DESC;
```

**Lockout events in a date range:**

```sql
SELECT user_id, event_type, ip_address, occurred_at
FROM auth_audit.auth_audit_entries
WHERE tenant_id = '$tenant_id'
  AND event_type IN ('AccountLockedOut', 'MfaLockedOut')
  AND occurred_at BETWEEN '2026-01-01' AND '2026-02-01'
ORDER BY occurred_at DESC;
```

## Retention Policy

No automatic retention policy is applied out of the box. The `auth_audit_entries` table grows indefinitely. For production deployments, add a scheduled job (for example, a PostgreSQL `pg_cron` rule or an application-level Hangfire job) to delete rows older than your required retention window:

```sql
-- Example: delete entries older than 90 days
DELETE FROM auth_audit.auth_audit_entries
WHERE occurred_at < now() - INTERVAL '90 days';
```

Consider adding an index on `occurred_at` before running this at scale:

```sql
CREATE INDEX IF NOT EXISTS ix_auth_audit_entries_occurred_at
    ON auth_audit.auth_audit_entries (occurred_at);
```

## Extending Audit Coverage

`IAuthAuditService` is a shared-kernel interface available to any module. The implementation (`AuthAuditService` in `Wallow.Shared.Infrastructure.Core`) writes to the same `auth_audit_entries` table. Calling it from another module only requires injecting the interface.

**1. Add a Wolverine handler in your module's Infrastructure project:**

```csharp
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Auditing;

public static class MyModuleAuditHandlers
{
    public static Task Handle(UserSessionEvictedEvent message, IAuthAuditService authAuditService)
    {
        return authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = "SessionEvicted",
            UserId = message.UserId,
            TenantId = message.TenantId,
            OccurredAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);
    }
}
```

Wolverine auto-discovers handlers in all `Wallow.*` assemblies, so no explicit registration is needed.

**2. Use a descriptive, consistent `EventType` string.** Use PascalCase. Prefix with a module name if the event is module-specific (e.g., `Billing.InvoiceAccessed`).

**3. Populate `IpAddress` only when it is available on the source event.** Do not fabricate or forward stale IP values.

`IAuthAuditService` swallows exceptions internally and logs them at `Error` level, so a database outage does not propagate back to the caller.

## Related Documentation

- [Observability Guide](observability.md) — structured logging, tracing, and metrics
- [Troubleshooting Guide](troubleshooting.md) — diagnosing issues in production
- [Messaging Guide](../architecture/messaging.md) — how Wolverine events work
