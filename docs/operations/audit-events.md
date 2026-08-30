# Audit Events

Wallow records security-relevant authentication events to a dedicated `auth_audit` PostgreSQL schema. These records are append-only and written independently of the main module schemas, so a failure to write an audit entry never fails the originating request.

## Table Schema

Events are stored in `auth_audit.auth_audit_entries`.

Column names are PascalCase in the database. EF Core maps the entity's property names straight
through — nothing in this repo applies a snake_case naming convention — so every identifier must be
double-quoted in hand-written SQL.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `"Id"` | `uuid` | No | Primary key, generated per event |
| `"EventType"` | `text` | No | String identifier for the event (see below) |
| `"UserId"` | `uuid` | No | The user the event is about |
| `"ActorId"` | `uuid` | Yes | Who caused the event, when that is somebody other than the subject. Null for every authentication event — nobody logs in on another person's behalf |
| `"TenantId"` | `uuid` | Yes | The tenant the event happened inside, or null when it happened outside every organization |
| `"IpAddress"` | `text` | Yes | Client IP address, when available |
| `"UserAgent"` | `text` | Yes | HTTP User-Agent header, when available |
| `"ClientId"` | `text` | Yes | The OAuth client the event is about, for the client lifecycle events below; null for every other event |
| `"Reason"` | `text` | Yes | The operator's stated reason, for the platform-suspension events that carry one; null for everything else |
| `"OccurredAt"` | `timestamp with time zone` | No | UTC timestamp; defaults to `now()` at insert |

The table is created by the `InitialCreate` EF Core migration in `Wallow.Shared.Infrastructure.Core`
(`Migrations/AuthAudit/`).

## Event Types

The `"EventType"` column uses plain string values. The following events are recorded by default.

| `"EventType"` | Trigger | IP recorded |
|--------------|---------|-------------|
| `LoginSucceeded` | A user successfully authenticates | Yes |
| `LoginFailed` | A login attempt is rejected (wrong password, unknown user, etc.) | Yes |
| `AccountLockedOut` | A user account is locked after repeated failed login attempts | Yes |
| `MfaLockedOut` | A user is locked out after repeated MFA failures | No |
| `Membership<Transition>` | Somebody's membership of an organization changed state (see below) | No |
| `ClientRegistered` | Somebody registered an application or service account for an organization (see below) | Yes |
| `ClientSecretRotated` | Somebody rotated a registered client's secret (see below) | Yes |
| `ClientSuspended` | Somebody suspended a registered client, ending every token it held (see below) | Yes |
| `ClientReinstated` | Somebody reinstated a suspended client (see below) | Yes |
| `ClientDeleted` | Somebody deleted a registered client along with its tokens, consents and branding (see below) | Yes |
| `ClientSuspendedByPlatform` | A global admin placed the platform's suspension on a registered client (see below) | Yes |
| `ClientReinstatedByPlatform` | A global admin lifted a client's platform suspension (see below) | Yes |
| `OrganizationSuspendedByPlatform` | A global admin placed the platform's suspension on an organization (see below) | No |
| `OrganizationReinstatedByPlatform` | A global admin lifted an organization's platform suspension (see below) | No |

Each event is written by `AuthAuditEventHandlers` in the Identity module, which subscribes to the corresponding Wolverine in-memory integration events published by the Identity domain.

| `"EventType"` | Source integration event |
|--------------|--------------------------|
| `LoginSucceeded` | `UserLoginSucceededEvent` |
| `LoginFailed` | `UserLoginFailedEvent` |
| `AccountLockedOut` | `UserAccountLockedOutEvent` |
| `MfaLockedOut` | `UserMfaLockedOutEvent` |
| `Membership<Transition>` | `MembershipTransitionedEvent` |
| `ClientRegistered` | `ClientRegisteredEvent` |
| `ClientSecretRotated` | `ClientSecretRotatedEvent` |
| `ClientSuspended` | `ClientSuspendedEvent` |
| `ClientReinstated` | `ClientReinstatedEvent` |
| `ClientDeleted` | `ClientDeletedEvent` |
| `ClientSuspendedByPlatform` | `ClientSuspendedByPlatformEvent` |
| `ClientReinstatedByPlatform` | `ClientReinstatedByPlatformEvent` |
| `OrganizationSuspendedByPlatform` | `OrganizationSuspendedByPlatformEvent` |
| `OrganizationReinstatedByPlatform` | `OrganizationReinstatedByPlatformEvent` |

### Membership events

`MembershipTransitionedEvent` carries a `Transition` discriminator rather than shipping one record
per transition. The handler spells that discriminator into the event type — `MembershipApproved`,
`MembershipSuspended`, and so on — so a membership decision is queried exactly the same way every
other audited event is.

The fourteen `MembershipTransition` values are `AccessRequested`, `Enrolled`, `Added`, `Approved`,
`Denied`, `DenialCleared`, `Suspended`, `Reinstated`, `RoleAssigned`, `RoleRemoved`, `Left`,
`Removed`, `OwnerMarked` and `OwnerUnmarked`.

This handler writes `"ActorId"`: it records who made the change, while `"UserId"`
records who it was made about. The two are equal for the transitions somebody performs on their own
membership — requesting access, enrolling, leaving — and that equality is the record, not an
omission. Apart from the client lifecycle events below, every other audited event leaves
`"ActorId"` null.

To read the whole family:

```sql
SELECT "EventType", "UserId", "ActorId", "OccurredAt"
FROM auth_audit.auth_audit_entries
WHERE "EventType" LIKE 'Membership%'
ORDER BY "OccurredAt" DESC;
```

### Client lifecycle events

`ClientRegistered`, `ClientSecretRotated`, `ClientSuspended`, `ClientReinstated`, `ClientDeleted`
and the two client platform-suspension events below are about a registered client rather than a
person, so they are the only events that fill `"ClientId"`. The person who did it stands in both `"UserId"` and `"ActorId"` — there is no separate
subject — and `"TenantId"` is the organization that owns the client. All of them carry the caller's
IP address when the request exposed one.

`ClientSuspended` means every token the client held was revoked and its realtime connections were
hung up; the client's configuration, branding and consents survive, and `ClientReinstated` puts it
back in service without asking anyone to consent again. `ClientDeleted` is the end of the record:
the client's tokens, consents and branding are gone with it, and registering the same name again
starts a fresh client with no consents. A deleted client's audit rows are all that remains of it,
which is why `"ClientId"` is a string rather than a foreign key.

`ClientSecretRotatedEvent` also says whether the rotation revoked the client's outstanding tokens;
that flag is logged by the Identity module but not stored in the audit row.

To see a client's history:

```sql
SELECT "EventType", "ActorId", "IpAddress", "OccurredAt"
FROM auth_audit.auth_audit_entries
WHERE "ClientId" = '$client_id'
ORDER BY "OccurredAt" DESC;
```

### Platform suspension events

`ClientSuspendedByPlatform`, `ClientReinstatedByPlatform`, `OrganizationSuspendedByPlatform` and
`OrganizationReinstatedByPlatform` record the platform operator's own interventions — a global
admin acting above every organization role. `"UserId"` and `"ActorId"` both hold the operator, and
`"TenantId"` holds the affected organization (for the client events, the organization that owns the
client). The two `…SuspendedByPlatform` events carry the operator's stated reason in `"Reason"` —
the same reason the organization's admins read on the client or organization — while the
`…ReinstatedByPlatform` events leave it null: lifting needs no justification on the record, the
placement carries it.

A platform-suspended client behaves exactly as a suspended one (tokens revoked, authorize and token
endpoints refuse) but none of the organization's own controls lift it. A platform-suspended
organization loses every member's and every bound client's tokens, and every change to the
organization is refused until the suspension is lifted; it also cannot be deleted while suspended.

To see everything the platform has done to an organization:

```sql
SELECT "EventType", "ActorId", "ClientId", "Reason", "OccurredAt"
FROM auth_audit.auth_audit_entries
WHERE "TenantId" = '$tenant_id'
  AND "EventType" LIKE '%ByPlatform'
ORDER BY "OccurredAt" DESC;
```

## Querying Events

All examples use the `auth_audit` schema. Substitute real UUIDs for `$user_id` and `$tenant_id`.
Every column identifier is double-quoted, because the columns are PascalCase — an unquoted
`user_id` or `occurredat` does not exist and the query errors out.

**Recent logins for a user:**

```sql
SELECT "Id", "EventType", "IpAddress", "OccurredAt"
FROM auth_audit.auth_audit_entries
WHERE "UserId" = '$user_id'
  AND "EventType" = 'LoginSucceeded'
ORDER BY "OccurredAt" DESC
LIMIT 50;
```

**Failed login attempts in the last 24 hours (across a tenant):**

```sql
SELECT "UserId", "IpAddress", COUNT(*) AS attempts
FROM auth_audit.auth_audit_entries
WHERE "TenantId" = '$tenant_id'
  AND "EventType" = 'LoginFailed'
  AND "OccurredAt" >= now() - INTERVAL '24 hours'
GROUP BY "UserId", "IpAddress"
ORDER BY attempts DESC;
```

**All security events for a user (ordered most recent first):**

```sql
SELECT "EventType", "IpAddress", "UserAgent", "OccurredAt"
FROM auth_audit.auth_audit_entries
WHERE "UserId" = '$user_id'
ORDER BY "OccurredAt" DESC;
```

**Lockout events in a date range:**

```sql
SELECT "UserId", "EventType", "IpAddress", "OccurredAt"
FROM auth_audit.auth_audit_entries
WHERE "TenantId" = '$tenant_id'
  AND "EventType" IN ('AccountLockedOut', 'MfaLockedOut')
  AND "OccurredAt" BETWEEN '2026-01-01' AND '2026-02-01'
ORDER BY "OccurredAt" DESC;
```

## Retention Policy

No automatic retention policy is applied out of the box. The `auth_audit_entries` table grows indefinitely. For production deployments, add a scheduled job (for example, a PostgreSQL `pg_cron` rule or an application-level Hangfire job) to delete rows older than your required retention window:

```sql
-- Example: delete entries older than 90 days
DELETE FROM auth_audit.auth_audit_entries
WHERE "OccurredAt" < now() - INTERVAL '90 days';
```

Consider adding an index on `"OccurredAt"` before running this at scale:

```sql
CREATE INDEX IF NOT EXISTS ix_auth_audit_entries_occurred_at
    ON auth_audit.auth_audit_entries ("OccurredAt");
```

## Extending Audit Coverage

`IAuthAuditService` is a shared-kernel interface available to any module. The implementation (`AuthAuditService` in `Wallow.Shared.Infrastructure.Core`) writes to the same `auth_audit_entries` table. Calling it from another module only requires injecting the interface.

**1. Add a Wolverine handler in your module's Infrastructure project:**

```csharp
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Auditing;
using Wolverine.Attributes;

[WolverineHandler]
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

Your module's `IWallowModule.HandlerAssemblies` already covers its own Infrastructure project, so
there is no registration call to write — but
`[WolverineHandler]` is load-bearing here and must not be dropped. Conventional discovery finds a
public concrete type only if it implements `IWolverineHandler`, carries `[WolverineHandler]`, or has
a **type name ending in `Handler` or `Consumer`**. The name is the whole story: `AuthAuditEventHandlers`
is plural, so it matches none of the three and needs the attribute, without which every method in it
is silently unreachable and nothing is ever audited. Static-ness is not the problem — 29 `public
static class …Handler` types elsewhere in `api/src` are discovered fine with no attribute, which is
why `api/CLAUDE.md` can say static handlers need no registration.

If you name a handler class `…Handlers` (plural) and forget the attribute, nothing fails at startup
and nothing logs — the messages simply go nowhere.

**2. Use a descriptive, consistent `EventType` string.** Use PascalCase. Prefix with a module name if the event is module-specific (e.g., `Storage.FileUploaded`).

**3. Populate `IpAddress` only when it is available on the source event.** Do not fabricate or forward stale IP values.

`IAuthAuditService` swallows exceptions internally and logs them at `Error` level, so a database outage does not propagate back to the caller.

## Related Documentation

- [Observability Guide](observability.md) — structured logging, tracing, and metrics
- [Troubleshooting Guide](troubleshooting.md) — diagnosing issues in production
- [Messaging Guide](../architecture/messaging.md) — how Wolverine events work
