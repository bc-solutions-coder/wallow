# Auth Security Hardening — Design Document

**Date:** 2026-03-28
**Status:** Approved (revised after security + architecture review)
**Scope:** 5 verified auth security gaps, implemented in 3 phases

---

## Background

A comprehensive auth flow analysis identified 5 verified gaps in the Wallow authentication system. Three false positives were ruled out by verification agents (SecurityStamp updates on password reset are handled by ASP.NET Identity internals, CSRF is mitigated by the Blazor Server architecture, and password complexity defaults are active).

### Verified Gaps

| # | Gap | Severity | Phase |
|---|-----|----------|-------|
| 1 | No per-user MFA lockout after failed TOTP attempts | HIGH | 1 |
| 2 | No login audit trail (failed/succeeded/lockout events) | MEDIUM | 1 |
| 3 | Sign-in ticket replayable within 60-second window | MEDIUM | 2 |
| 4 | No concurrent session limits | MEDIUM | 2 |
| 5 | No email change flow | LOW | 3 |

---

## Existing Infrastructure

### Audit System

Wallow has an EF Core-based entity audit system in `Shared.Infrastructure.Core.Auditing`:

- **AuditInterceptor** — `SaveChangesInterceptor` that captures Insert/Update/Delete on all entities. Skips processing when the context is `AuditDbContext` (guard clause at line 26).
- **AuditDbContext** — writes to `audit.audit_entries` table (PostgreSQL, jsonb for old/new values)
- **AuditEntry** — EntityType, EntityId, Action, OldValues, NewValues, UserId, TenantId, Timestamp
- **AuditingExtensions** — DI registration with pooled DbContext, auto-migration in dev

This handles data-level auditing. Auth event auditing (login attempts, lockouts) requires a complementary system in a **separate `AuthAuditDbContext`** to avoid coupling auth audit writes with entity audit writes.

### Auth Rate Limiting

- `[EnableRateLimiting("auth")]` on AccountController — 3 requests per 10 minutes
- Partition key: tenant ID (authenticated) or IP (unauthenticated)
- Redis-backed fixed-window rate limiter
- Insufficient for per-user MFA brute force protection

### MFA Partial Auth

- `MfaPartialAuthService` issues a data-protected cookie (`Identity.MfaPartial`, 5-min TTL)
- Payload: UserId, Email, LoginMethod, RememberMe, IssuedAt
- No attempt tracking within the partial auth window

### Event Conventions

Existing Identity events are published directly from `AccountController` via `messageBus.PublishAsync()`. All events follow this pattern:

- `sealed record` extending `IntegrationEvent` base record
- Lives in `Wallow.Shared.Contracts.Identity.Events`
- Properties use `required` + `init`
- `TenantId` is non-nullable `Guid`

---

## Phase 1: MFA Per-User Lockout + Login Audit Events

### 1A. MFA Per-User Lockout

**Goal:** Prevent brute-force attacks against TOTP codes during the MFA challenge window.

**Schema changes (WallowUser):**

```csharp
public int MfaFailedAttempts { get; private set; }
public DateTimeOffset? MfaLockoutEnd { get; private set; }
public int MfaLockoutCount { get; private set; }   // Tracks consecutive lockouts for exponential backoff
```

**Lockout parameters with exponential backoff:**

| Lockout # | Duration | Notes |
|-----------|----------|-------|
| 1st | 15 minutes | After 5 failed attempts |
| 2nd | 1 hour | Sustained attack |
| 3rd | 4 hours | Escalating |
| 4th+ | 24 hours | Maximum lockout |

`MfaLockoutCount` resets only on successful MFA verification, not on timer expiry. This prevents persistent attackers from cycling through short lockouts indefinitely.

**Domain method on WallowUser (encapsulates the lockout policy):**

```csharp
public bool RecordMfaFailure(DateTimeOffset now, int maxAttempts = 5)
{
    MfaFailedAttempts++;
    if (MfaFailedAttempts >= maxAttempts)
    {
        MfaLockoutCount++;
        TimeSpan duration = MfaLockoutCount switch
        {
            1 => TimeSpan.FromMinutes(15),
            2 => TimeSpan.FromHours(1),
            3 => TimeSpan.FromHours(4),
            _ => TimeSpan.FromHours(24)
        };
        MfaLockoutEnd = now + duration;
        return true; // locked out
    }
    return false;
}

public void ResetMfaAttempts()
{
    MfaFailedAttempts = 0;
    MfaLockoutEnd = null;
    MfaLockoutCount = 0;
}

public bool IsMfaLockedOut(DateTimeOffset now)
    => MfaLockoutEnd.HasValue && MfaLockoutEnd.Value > now;
```

**Concurrency safety:** The MFA failure increment must be atomic to prevent race conditions where concurrent requests both read the same counter value. Use raw SQL `UPDATE ... SET mfa_failed_attempts = mfa_failed_attempts + 1 ... RETURNING mfa_failed_attempts` rather than in-memory increment + save. This avoids retry loops and is a single database round-trip.

**Redis lockout cache:** After setting lockout, write a Redis key `mfa:lockout:<userId>` with matching TTL. Check Redis first in the verification flow — short-circuits most attack traffic before it hits PostgreSQL. Only fall through to the database if the Redis key is absent.

**Flow changes in `AccountController.VerifyMfaChallenge`:**

```
1. ValidatePartialCookieAsync() → get userId
2. FindByIdAsync(userId)
3. NEW: Check Redis mfa:lockout:<userId> — if present, return 423 "mfa_locked_out"
4. NEW: If Redis miss, check user.IsMfaLockedOut(now) — if locked, set Redis cache, return 423
5. ValidateTotpAsync(secret, code)
6. If invalid: ValidateBackupCodeAsync(userId, code)
7. If both invalid:
   a. NEW: Atomic SQL increment + lockout check (UPDATE RETURNING)
   b. NEW: If locked out → set Redis cache, publish UserMfaLockedOutEvent, return 423
   c. Return 401 "invalid_code"
8. If valid:
   a. NEW: user.ResetMfaAttempts() + save
   b. UpgradeToFullAuthAsync() (existing)
   c. Create sign-in ticket (existing)
```

**New event:** `UserMfaLockedOutEvent` — sealed record extending IntegrationEvent, in `Shared.Contracts.Identity.Events`, with `required Guid UserId`, `required string Email`, `required Guid TenantId`.

**Admin endpoint:** Extend existing `MfaController.AdminClearLockout` to also reset MFA lockout fields (MfaFailedAttempts, MfaLockoutEnd, MfaLockoutCount).

**Incidental fix:** Update `WallowUser.SetMfaGraceDeadline` to accept `DateTimeOffset now` as a parameter instead of using `DateTimeOffset.UtcNow` directly, matching the `TimeProvider` pattern used in `Create`.

**Migration:** Add `MfaFailedAttempts` (int, default 0), `MfaLockoutEnd` (timestamptz, nullable), and `MfaLockoutCount` (int, default 0) to `identity.AspNetUsers`.

### 1B. Login Audit Events

**Goal:** Provide a forensic audit trail for all authentication events.

**New entity — `AuthAuditEntry`:**

```csharp
public class AuthAuditEntry
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public required string Email { get; set; }
    public required string EventType { get; set; }    // LoginSucceeded, LoginFailed, AccountLockedOut, MfaFailed, MfaLockedOut, MfaSucceeded, PasswordReset, ExternalLogin, EmailChangeRequested, EmailChangeConfirmed
    public required string Method { get; set; }        // Password, MagicLink, Otp, External, ApiKey
    public required string Result { get; set; }        // Success, Failed, LockedOut, MfaRequired, EmailNotConfirmed
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? TenantId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Details { get; set; }               // jsonb — extra context (provider name, failure reason)
}
```

**Storage:** `audit.auth_events` table in a **new `AuthAuditDbContext`** — separate from the existing `AuditDbContext`. Same `audit` schema, but a dedicated DbContext to avoid coupling auth audit writes with entity change tracking. The `AuditInterceptor` guard clause already skips `AuditDbContext`; `AuthAuditDbContext` will also need to be excluded (or simply not registered with the interceptor).

**Indexes:**

- Composite index on `(UserId, Timestamp)` — forensic queries per user
- Single-column index on `(Email)` — lookup by email
- Table partitioning by month on `Timestamp` column

**Retention policy:** 90 days hot storage. Background job prunes partitions older than 90 days. Archive to cold storage if compliance requires longer retention.

**Service interface — `IAuthAuditService`:**

```csharp
public interface IAuthAuditService
{
    Task LogAsync(AuthAuditEntry entry, CancellationToken ct = default);
}
```

Interface defined in `Wallow.Shared.Kernel` (not Infrastructure.Core) to maintain proper dependency direction. Implementation (`AuthAuditService`) lives in `Shared.Infrastructure.Core.Auditing` alongside existing audit code.

**Non-blocking audit writes:** Wolverine consumers for auth audit events must be configured with local queue semantics so that database outages or slowdowns on the audit schema do not block or fail authentication flows. Audit write failures should be logged but never propagate to the caller.

**Domain events (via Wolverine):**

| Event | Published When |
|-------|---------------|
| `UserLoginSucceededEvent` | Successful password/passwordless login |
| `UserLoginFailedEvent` | Invalid credentials |
| `UserAccountLockedOutEvent` | Password lockout triggered |
| `UserMfaLockedOutEvent` | MFA lockout triggered (from 1A) |

All events follow the established convention: `sealed record` extending `IntegrationEvent`, in `Shared.Contracts.Identity.Events`, with `required` properties and non-nullable `Guid TenantId`. Published from `AccountController` via `messageBus.PublishAsync()` (consistent with existing codebase pattern).

**Structured logging:** Add `[LoggerMessage]` methods to AccountController for each auth outcome (per LOGGING rules). These provide immediate observability via Grafana/Loki, complementing the database audit trail.

**Instrumentation points in AccountController:**

| Location | Event | Method |
|----------|-------|--------|
| Login success (before ticket) | LoginSucceeded | Password |
| Login failed (invalid creds) | LoginFailed | Password |
| Login locked out | AccountLockedOut | Password |
| Login email not confirmed | LoginFailed | Password |
| MFA verify success | MfaSucceeded | Password |
| MFA verify failed | MfaFailed | Password |
| MFA locked out | MfaLockedOut | Password |
| Magic link verified | LoginSucceeded | MagicLink |
| OTP verified | LoginSucceeded | Otp |
| External login success | LoginSucceeded | External |
| External login failed | LoginFailed | External |
| Password reset completed | PasswordReset | Password |
| Email change requested | EmailChangeRequested | N/A |
| Email change confirmed | EmailChangeConfirmed | N/A |

---

## Phase 2: Sign-In Ticket Single-Use + Concurrent Session Limits

### 2A. Sign-In Ticket Single-Use Enforcement

**Goal:** Prevent replay of sign-in tickets within their 60-second validity window.

**Approach:** Atomic Redis-backed ticket nonce tracking.

**Sign-in ticket payload enhancement:** Add a `Guid Jti` field to `SignInTicketPayload` for defense-in-depth. This ensures each ticket has a unique identifier independent of the data protector's internal nonce, and simplifies audit correlation.

```csharp
public sealed record SignInTicketPayload(string Email, bool RememberMe, Guid Jti);
```

**Flow changes in `AccountController.ExchangeTicket`:**

```
1. ValidateSignInTicket(ticket) → payload (existing)
2. NEW: Compute SHA256 hash of ticket string
3. NEW: Atomic SET NX EX — StringSetAsync("ticket:used:<hash>", "1", 65s, When.NotExists)
   - If returns false (key already existed) → return 401 "ticket_already_used"
   - If returns true (key was set) → proceed
   (65s > 60s ticket lifetime to account for clock skew)
4. SignInAsync(user, rememberMe) (existing)
5. Redirect to returnUrl (existing)
```

Steps 2-3 replace the original check-then-set with a single atomic `SET NX EX` operation, eliminating the TOCTOU race condition. In StackExchange.Redis: `db.StringSetAsync(key, value, expiry, When.NotExists)`.

**Dependencies:** Existing Valkey (Redis) infrastructure. Uses `IConnectionMultiplexer` already registered.

**No new entities or migrations.** Pure Redis operation.

### 2B. Concurrent Session Limits

**Goal:** Limit concurrent sessions per user with oldest-session eviction and notification.

**New entity — `ActiveSession` (DDD-modeled):**

```csharp
public class ActiveSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string SessionId { get; private set; }      // From auth cookie ticket identifier
    public string? DeviceInfo { get; private set; }     // Parsed User-Agent
    public string? IpAddress { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastActiveAt { get; private set; }
    public Guid TenantId { get; private set; }

    public static ActiveSession Create(
        Guid userId, string sessionId, string? deviceInfo,
        string? ipAddress, Guid tenantId, DateTimeOffset now)
    {
        return new ActiveSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = sessionId,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress,
            TenantId = tenantId,
            CreatedAt = now,
            LastActiveAt = now
        };
    }

    public void Touch(DateTimeOffset now) => LastActiveAt = now;

    public bool IsExpired(DateTimeOffset now, TimeSpan maxInactivity)
        => now - LastActiveAt > maxInactivity;
}
```

**Storage:** `identity.active_sessions` table in IdentityDbContext.

**Session lifecycle:**

| Action | Trigger | Behavior |
|--------|---------|----------|
| Create | `ExchangeTicket` succeeds | Insert ActiveSession record |
| Update | Authenticated request (throttled) | `Touch()` every 5 min via middleware |
| Evict | New login exceeds limit | Delete oldest session, revoke via Redis |
| Delete | Logout | Remove session record |
| Prune | Background job (hourly) | Remove sessions where `IsExpired(now, 30 days)` |

**Session eviction — Redis-based revocation (not SecurityStamp):**

Updating SecurityStamp would invalidate ALL sessions for a user, not just the oldest one. Instead, use a Redis-backed revocation set:

- On eviction: add `session:revoked:<sessionId>` to Redis with 30-day TTL
- Auth middleware checks the revocation set on each authenticated request
- This provides surgical per-session eviction without affecting other sessions

**Concurrency safety:** Wrap the count-check-evict-insert sequence in a PostgreSQL advisory lock: `SELECT pg_advisory_xact_lock(hashtext(userId::text))` at the start of the transaction. This prevents two simultaneous logins from both passing the count check and inserting, which would exceed the session limit.

**Soft limit enforcement:**

```
1. On ExchangeTicket success:
2. Acquire advisory lock on userId
3. Count active sessions for user
4. If count >= limit (default 5):
   a. Find oldest session by LastActiveAt
   b. Delete session record
   c. Add session ID to Redis revocation set
   d. Publish UserSessionEvictedEvent → notification email to user
5. Insert new ActiveSession
6. Release advisory lock (automatic on transaction commit)
```

**Configuration:** Default limit = 5. Configurable per-org via tenant settings (existing `TenantSettingEntity` infrastructure). Setting key: `security.max_concurrent_sessions`.

**API endpoints:**

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/v1/identity/sessions` | GET | List user's active sessions |
| `/api/v1/identity/sessions/{id}` | DELETE | Revoke a specific session |

**New event:** `UserSessionEvictedEvent` — sealed record extending IntegrationEvent, with `required Guid UserId`, `required string DeviceInfo`, `required string IpAddress`, `required Guid TenantId`.

---

## Phase 3: Email Change Flow

### 3A. Email Change with Verification

**Goal:** Allow users to change their email with proper verification of the new address.

**Schema changes (WallowUser):**

```csharp
public string? PendingEmail { get; private set; }
public DateTimeOffset? PendingEmailExpiry { get; private set; }
```

Note: `PendingEmailToken` is NOT stored on the entity. ASP.NET Identity's `GenerateChangeEmailTokenAsync`/`ChangeEmailAsync` manages token validation internally against the SecurityStamp. Storing the token separately would create two sources of truth.

**Rate limiting:** 3 requests per hour per user on the initiation endpoint. 72-hour cooldown after a successful email change before another change can be initiated.

**Flow:**

```
Initiate:
1. POST /api/v1/identity/users/me/email/change { newEmail, password }
2. Rate limit check (3/hour per user + 72h post-change cooldown)
3. Verify current password
4. Check newEmail not already in use
5. Generate confirmation token (ASP.NET Identity's GenerateChangeEmailTokenAsync)
6. Store PendingEmail + PendingEmailExpiry (24 hours) on user entity
7. Publish UserEmailChangeRequestedEvent → sends verification email to NEW address
8. NEW: Publish notification to OLD email ("Email change requested. If not you, change your password.")
9. Return 200 { message: "verification_sent" }

Confirm:
1. GET /api/v1/identity/users/me/email/confirm?token=...&email=...
2. Validate PendingEmailExpiry has not passed
3. Validate token via ChangeEmailAsync (ASP.NET Identity built-in)
4. Clear PendingEmail + PendingEmailExpiry
5. Update SecurityStamp (automatic via ChangeEmailAsync)
6. Publish UserEmailChangedEvent { UserId, OldEmail, NewEmail }
7. Return 200 { succeeded: true }
```

**Security:**

- Password required to initiate (prevents unauthorized changes if session is hijacked)
- Token sent to new email (proves ownership)
- Notification sent to old email at initiation (alerts legitimate user to unauthorized attempts)
- 24-hour expiry on pending change
- Rate limited: 3/hour + 72-hour cooldown after successful change
- Old email remains active until confirmation
- SecurityStamp updated on confirmation (invalidates existing sessions — forces re-login)
- Only one pending change at a time (new request overwrites previous)
- Token managed by ASP.NET Identity (not stored on entity — avoids dual source of truth)

**New events:**

| Event | Purpose |
|-------|---------|
| `UserEmailChangeRequestedEvent` | Triggers verification email to new address + alert to old address |
| `UserEmailChangedEvent` | Audit trail, triggers confirmation notification to old address |

Both follow the established convention: sealed record extending IntegrationEvent, `required` properties, non-nullable `Guid TenantId`, in `Shared.Contracts.Identity.Events`.

**Migration:** Add `PendingEmail` (text, nullable) and `PendingEmailExpiry` (timestamptz, nullable) to `identity.AspNetUsers`.

---

## Cross-Cutting Concerns

### Testing Strategy

- **Unit tests:** Domain methods on WallowUser (MFA lockout logic with exponential backoff, email change state)
- **Unit tests:** AuthAuditService, ticket hash computation, ActiveSession lifecycle
- **Integration tests:** MFA lockout flow (login → MFA fail × 5 → lockout → wait → retry → escalating lockout)
- **Integration tests:** Ticket replay rejection (concurrent exchange attempts)
- **Integration tests:** Session eviction on limit exceeded (advisory lock correctness)
- **Integration tests:** Email change initiate → confirm flow (token management, old email notification)
- **E2E tests:** MFA lockout visible in UI (error message)
- **E2E tests:** Session management page (if UI added)

### Migration Strategy

All migrations are additive (new columns with defaults, new tables). No breaking changes. Safe for zero-downtime deployment.

| Phase | Migration | Risk |
|-------|-----------|------|
| 1 | Add MfaFailedAttempts, MfaLockoutEnd, MfaLockoutCount to AspNetUsers | None — defaults to 0/null |
| 1 | Create AuthAuditDbContext + auth_events table in audit schema | None — new table, separate DbContext |
| 2 | Add active_sessions table to identity schema | None — new table |
| 3 | Add PendingEmail + PendingEmailExpiry to AspNetUsers | None — nullable columns |

### Backward Compatibility

All changes are additive. Existing auth flows continue to work unchanged. New behavior (lockout, audit, session tracking) activates alongside existing flows without requiring client changes.

---

## Review History

Design reviewed by two agents (security + architecture) with 26 total findings. Key changes from review:

- **Atomic operations:** Ticket replay uses `SET NX EX`, MFA increment uses SQL `UPDATE RETURNING`, session eviction uses advisory lock
- **Exponential backoff:** MFA lockout escalates from 15 min to 24 hours across consecutive lockouts
- **Encapsulated domain logic:** `RecordMfaFailure` method on WallowUser enforces lockout policy internally
- **Separate AuthAuditDbContext:** Auth audit writes isolated from entity audit writes
- **IAuthAuditService in Shared.Kernel:** Maintains proper dependency direction (Identity → Kernel, not Identity → Infrastructure)
- **Redis session revocation:** Surgical per-session eviction instead of SecurityStamp (which would nuke all sessions)
- **No PendingEmailToken storage:** ASP.NET Identity manages tokens; entity tracks only pending state
- **Old email notification on change initiation:** Alerts legitimate user before change is confirmed
- **Rate limiting on email change:** 3/hour + 72-hour post-change cooldown
- **Jti on sign-in ticket:** Defense-in-depth unique identifier per ticket
- **Event naming:** All events prefixed with `User` (e.g., `UserSessionEvictedEvent`, `UserEmailChangedEvent`)
- **ActiveSession DDD modeling:** Factory method, private setters, `Touch()` and `IsExpired()` domain methods
- **Non-blocking audit writes:** Wolverine local queue for audit consumers
