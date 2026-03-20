# Phase 3: Shared Infrastructure Core

**Scope:** `src/Shared/Wallow.Shared.Infrastructure.Core/`
**Status:** Not Started
**Files:** 14 source files (+ 3 migration files), 0 test files (tests in Shared.Infrastructure.Tests)

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Source Files

### Auditing

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuditEntry.cs` | Entity representing a single audit log entry stored in PostgreSQL | Properties: Id, EntityType, EntityId, Action, OldValues/NewValues (JSONB), UserId, TenantId, Timestamp | None (POCO) | |
| 2 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuditDbContext.cs` | Dedicated EF Core DbContext for the audit schema with JSONB column mappings | `AuditEntries` DbSet; configures `audit` schema, `audit_entries` table, JSONB columns, default timestamp | Microsoft.EntityFrameworkCore | |
| 3 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuditInterceptor.cs` | EF Core SaveChanges interceptor that captures entity changes and writes audit entries | `SavingChangesAsync` captures changes via `CaptureChanges`, resolves user/tenant from HTTP context, saves to AuditDbContext | Kernel.Auditing, Kernel.MultiTenancy, Microsoft.AspNetCore.Http, Microsoft.EntityFrameworkCore | |
| 4 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Auditing/AuditingExtensions.cs` | DI registration and initialization extensions for the auditing subsystem | `AddWallowAuditing` registers AuditDbContext + interceptor; `InitializeAuditingAsync` runs migrations in dev | Microsoft.EntityFrameworkCore, Microsoft.AspNetCore.Builder | |

### Cache

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 5 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Cache/InstrumentedDistributedCache.cs` | Decorator around IDistributedCache that tracks cache hit/miss metrics via OpenTelemetry | Wraps all `Get`/`Set`/`Remove`/`Refresh` methods; records `wallow.cache.hits_total` and `wallow.cache.misses_total` counters | Kernel.Diagnostics, Microsoft.Extensions.Caching.Distributed | |

### Messaging

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 6 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Messaging/WolverineErrorHandlingExtensions.cs` | Configures standard Wolverine error handling: retry policies with backoff and dead letter queue | `ConfigureStandardErrorHandling` (TimeoutException 3 retries, InvalidOperationException 2 retries, others 1 retry then DLQ); `ConfigureMessageLogging` | WolverineFx | |

### Middleware

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 7 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/TenantRestoringMiddleware.cs` | Wolverine middleware that restores tenant context from `X-Tenant-Id` message header | `Before` static method parses Guid from envelope header and calls `SetTenant` on `ITenantContextSetter` | Kernel.Identity, Kernel.MultiTenancy, WolverineFx | |
| 8 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/TenantStampingMiddleware.cs` | Wolverine middleware that stamps tenant ID into outgoing message headers | `Before` static method writes `ITenantContext.TenantId` to `X-Tenant-Id` header if resolved | Kernel.MultiTenancy, WolverineFx | |
| 9 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Middleware/WolverineModuleTaggingMiddleware.cs` | Wolverine middleware that tags activities with module name, tenant ID, and records messaging metrics | `Before` extracts module from namespace regex, tags `Activity.Current`, tracks domain events; `After` records duration metrics | Kernel.Diagnostics, Kernel.Domain, WolverineFx, System.Diagnostics | |

### Persistence

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 10 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Persistence/DictionaryValueComparer.cs` | EF Core ValueComparer for `Dictionary<string, object>?` stored as JSONB, using JSON serialization for equality | `AreEqual` via JSON comparison, `GetDictionaryHashCode`, `CreateSnapshot` via deserialize/serialize roundtrip | System.Text.Json, Microsoft.EntityFrameworkCore | |
| 11 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Persistence/TenantAwareDbContext.cs` | Abstract DbContext base that applies per-tenant query filters to all ITenantScoped entities | `ApplyTenantQueryFilters` uses expression trees to add `HasQueryFilter` comparing entity TenantId to context `_tenantId` field | Kernel.Identity, Kernel.MultiTenancy, Microsoft.EntityFrameworkCore | |

### Resilience

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 12 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Resilience/ResilienceExtensions.cs` | HTTP client resilience configuration with named profiles (default, identity-provider) using Polly | `AddWallowResilienceHandler` configures retry, circuit breaker, timeout per profile; structured logging for retry/circuit events | Microsoft.Extensions.Http.Resilience, Polly | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 13 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Services/CurrentUserService.cs` | Extracts current user ID from HTTP context claims (NameIdentifier or sub claim) | `GetCurrentUserId` checks `IsAuthenticated`, parses `sub` claim to Guid; includes `AddCurrentUserService` extension | Kernel.Services.ICurrentUserService, Microsoft.AspNetCore.Http | |
| 14 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Services/HtmlSanitizationService.cs` | HTML sanitization service using HtmlSanitizer with a strict allowlist of tags, attributes, and URI schemes | Configures allowed tags (formatting only), attributes (href, class, etc.), schemes (http/https/mailto); `AddHtmlSanitization` extension | Ganss.Xss (HtmlSanitizer) | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 15 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Migrations/20260227204022_InitialAuditSchema.cs` | EF Core migration creating the initial audit schema and `audit_entries` table | Creates `audit` schema with JSONB columns for old/new values | Microsoft.EntityFrameworkCore.Migrations | |
| 16 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Migrations/20260227204022_InitialAuditSchema.Designer.cs` | Auto-generated migration designer metadata for InitialAuditSchema | EF Core migration snapshot metadata | Microsoft.EntityFrameworkCore | |
| 17 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Core/Migrations/AuditDbContextModelSnapshot.cs` | Auto-generated model snapshot for AuditDbContext | Current state of audit schema for migration diffing | Microsoft.EntityFrameworkCore | |

## Test Files

Tests for Infrastructure.Core classes are located in `tests/Wallow.Shared.Infrastructure.Tests/` and cataloged in Phase 4.
