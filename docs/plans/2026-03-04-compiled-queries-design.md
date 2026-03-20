# EF Core Compiled Queries Design

**Date:** 2026-03-04
**Status:** Draft
**Epic:** Compiled query optimization for hot-path EF Core queries

## Context

Wallow uses EF Core across 5 modules with 7 DbContext classes. No compiled queries exist today. Every LINQ query pays the cost of expression tree translation on every call. For hot-path queries (middleware, per-request lookups), this overhead adds up.

EF Core compiled queries (`EF.CompileAsyncQuery`) pre-translate LINQ to SQL once and reuse the compiled delegate, eliminating per-call translation cost.

## Current State

### Hot-Path Queries (run on every or most requests)

| Query | Module | File | Caching | Frequency |
|-------|--------|------|---------|-----------|
| `FeatureFlagRepository.GetByKeyAsync(key)` | Configuration | `Wallow.Configuration.Infrastructure/Persistence/Repositories/FeatureFlagRepository.cs:17` | DistributedCache 60s | Per flag check (multiple/request) |
| `ScimConfigurationRepository.GetAsync()` | Identity | `Wallow.Identity.Infrastructure/Repositories/ScimConfigurationRepository.cs` | None | Every SCIM request |
| `ServiceAccountRepository.GetByKeycloakClientIdAsync(clientId)` | Identity | `Wallow.Identity.Infrastructure/Repositories/ServiceAccountRepository.cs:16` | None | Every service account request |

### High-Value CRUD Queries (frequently called)

| Query | Module | File | Notes |
|-------|--------|------|-------|
| `FeatureFlagRepository.GetByIdAsync(id)` | Configuration | Same file | Cache miss path |
| `StorageBucketRepository.GetByNameAsync(name)` | Storage | `Wallow.Storage.Infrastructure/Persistence/Repositories/StorageBucketRepository.cs` | Every file operation |
| `StoredFileRepository.GetByIdAsync(id)` | Storage | `Wallow.Storage.Infrastructure/Persistence/Repositories/StoredFileRepository.cs` | File CRUD |
| `InvoiceRepository.GetByIdAsync(id)` | Billing | `Wallow.Billing.Infrastructure/Persistence/Repositories/InvoiceRepository.cs` | Billing CRUD |
| `SubscriptionRepository.GetByIdAsync(id)` | Billing | `Wallow.Billing.Infrastructure/Persistence/Repositories/SubscriptionRepository.cs` | Billing CRUD |
| `EmailPreferenceRepository.GetByUserAndTypeAsync(userId, type)` | Communications | `Wallow.Communications.Infrastructure/Persistence/Repositories/EmailPreferenceRepository.cs` | Composite key lookup |

### Already Optimized (No Action Needed)

- **Dapper queries** — InvoiceQueryService, RevenueReportService, InvoiceReportService, PaymentReportService, MessagingQueryService. Already raw SQL.
- **Redis queries** — API key validation via RedisApiKeyService. No EF Core involved.
- **Claims parsing** — CurrentUserService reads JWT claims, no DB access.

## Design Decisions

### 1. Use `EF.CompileAsyncQuery` static fields in repository classes

Define compiled queries as `private static readonly` fields in each repository. This keeps the compiled query co-located with its usage and avoids a separate "compiled queries" abstraction.

```csharp
public class FeatureFlagRepository : IFeatureFlagRepository
{
    private static readonly Func<ConfigurationDbContext, string, CancellationToken, Task<FeatureFlag?>>
        GetByKeyQuery = EF.CompileAsyncQuery(
            (ConfigurationDbContext ctx, string key, CancellationToken ct) =>
                ctx.FeatureFlags
                    .Include(f => f.Overrides)
                    .FirstOrDefault(f => f.Key == key));

    public Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken ct = default)
        => GetByKeyQuery(_context, key, ct);
}
```

### 2. Prioritize by tier

- **Tier 1 (hot-path):** Feature flag by key, SCIM config, service account by client ID, storage bucket by name
- **Tier 2 (high-value CRUD):** Feature flag by ID, stored file by ID, invoice by ID, subscription by ID
- **Tier 3 (medium):** Email preference by user+type, announcement by ID, conversation by ID

### 3. Handle tenant query filters

Most queries go through `TenantAwareDbContext` which applies automatic tenant filtering. Compiled queries work with global query filters. The one exception is `ServiceAccountRepository.GetByKeycloakClientIdAsync` which uses `.IgnoreQueryFilters()` — this works fine with compiled queries since the filter exclusion is part of the compiled expression.

### 4. Handle Include() in compiled queries

`FeatureFlagRepository.GetByKeyAsync` uses `.Include(f => f.Overrides)`. EF Core compiled queries support `Include()` — the navigation is part of the compiled expression tree.

### 5. Keep AsTracking() behavior

Current queries use `AsTracking()` for mutation paths. Compiled queries respect tracking behavior. For read-only paths, consider `AsNoTracking()` variants where applicable.

## Implementation Plan

### Task 1: Compile hot-path Configuration queries

**Files:** `src/Modules/Configuration/Wallow.Configuration.Infrastructure/Persistence/Repositories/FeatureFlagRepository.cs`

Compile:
- `GetByKeyAsync(string key)` — includes `Include(f => f.Overrides)`, `FirstOrDefault(f => f.Key == key)`
- `GetByIdAsync(FeatureFlagId id)` — simple ID lookup

### Task 2: Compile hot-path Identity queries

**Files:**
- `src/Modules/Identity/Wallow.Identity.Infrastructure/Repositories/ServiceAccountRepository.cs`
- `src/Modules/Identity/Wallow.Identity.Infrastructure/Repositories/ScimConfigurationRepository.cs`

Compile:
- `GetByKeycloakClientIdAsync(string keycloakClientId)` — uses `IgnoreQueryFilters()`, `FirstOrDefault`
- `GetAsync()` (SCIM config) — single-row tenant-scoped lookup

### Task 3: Compile Storage module queries

**Files:**
- `src/Modules/Storage/Wallow.Storage.Infrastructure/Persistence/Repositories/StorageBucketRepository.cs`
- `src/Modules/Storage/Wallow.Storage.Infrastructure/Persistence/Repositories/StoredFileRepository.cs`

Compile:
- `GetByNameAsync(string name)` — every file operation resolves bucket by name
- `GetByIdAsync(StoredFileId id)` — file CRUD

### Task 4: Compile Billing module queries

**Files:**
- `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/InvoiceRepository.cs`
- `src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/SubscriptionRepository.cs`

Compile:
- `GetByIdAsync(InvoiceId id)` — billing CRUD hot path
- `GetByIdAsync(SubscriptionId id)` — subscription lookups

### Task 5: Compile Communications module queries

**Files:**
- `src/Modules/Communications/Wallow.Communications.Infrastructure/Persistence/Repositories/EmailPreferenceRepository.cs`

Compile:
- `GetByUserAndTypeAsync(Guid userId, NotificationType type)` — composite key lookup

### Task 6: Add benchmarks for compiled vs non-compiled queries

Create BenchmarkDotNet benchmarks to measure the actual improvement:
- Compare compiled vs non-compiled for each tier 1 query
- Measure cold start (first call) vs warm (subsequent calls)
- Document results in the design doc

## Files Changed

| File | Change |
|------|--------|
| `FeatureFlagRepository.cs` | Add compiled query static fields, refactor methods |
| `ServiceAccountRepository.cs` | Add compiled query for GetByKeycloakClientIdAsync |
| `ScimConfigurationRepository.cs` | Add compiled query for GetAsync |
| `StorageBucketRepository.cs` | Add compiled query for GetByNameAsync |
| `StoredFileRepository.cs` | Add compiled query for GetByIdAsync |
| `InvoiceRepository.cs` | Add compiled query for GetByIdAsync |
| `SubscriptionRepository.cs` | Add compiled query for GetByIdAsync |
| `EmailPreferenceRepository.cs` | Add compiled query for GetByUserAndTypeAsync |

## Out of Scope

- **Dapper queries** — already raw SQL, no LINQ translation cost
- **Redis lookups** — not EF Core
- **Dynamic queries** with runtime-conditional WHERE clauses (e.g., QuotaDefinitionRepository.GetEffectiveQuotaAsync)
- **List/paging queries** — variable shapes make compilation impractical

## Risks

1. **Compiled query limitations** — Cannot use certain LINQ operators (`Contains` with variable lists, conditional `Include`). All identified candidates use simple fixed-shape expressions.
2. **Tenant filter interaction** — Compiled queries work with global query filters, but verify in integration tests.
3. **Change tracking** — Compiled queries that return tracked entities work normally. Verify no behavioral change in mutation flows.
