# Performance and Memory Efficiency Audit -- Verified Report

**Date:** 2026-03-02
**Original Auditor:** perf-auditor
**Verified by:** perf-verifier
**Scope:** All source files under `src/`

---

## Executive Summary

The original performance audit identified 23 findings (3 CRITICAL, 7 HIGH, 8 MEDIUM, 5 LOW). After reading the actual source code for every finding, 18 are **CONFIRMED**, 3 are **FALSE POSITIVE** or significantly inaccurate, and 2 have **SEVERITY ADJUSTED**. One new finding was identified that the original audit missed.

**Verified counts:** 3 CRITICAL, 4 HIGH, 9 MEDIUM, 5 LOW

---

## CRITICAL Findings

### PERF-C1: N+1 Query in GetConversationsAsync (Messaging)

**Verdict: CONFIRMED**

**File:** `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/MessagingQueryService.cs:170-208`

The N+1 pattern is exactly as described. Lines 170-208 show a `foreach` loop over `ConversationRow` results, with each iteration executing a separate Dapper query (lines 172-185) to load participants for that conversation. The line numbers in the audit are slightly off (audit says 153-200, actual loop is 170-208), but the issue is real.

The main query already uses pagination (LIMIT/OFFSET at line 153), which bounds the N+1 to the page size (typically 20-50), but this is still 21-51 queries instead of 2. The remediation suggestion to batch-load participants with `WHERE conversation_id = ANY(@ConversationIds)` is correct and idiomatic.

**Severity: CRITICAL -- confirmed**

---

### PERF-C2: N+1 Query in GetUsersAsync (Keycloak Admin)

**Verdict: CONFIRMED**

**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/KeycloakAdminService.cs:157-204`

Lines 178-194 confirm the sequential HTTP call pattern: `foreach (UserRepresentation user in users)` with `GetUserRolesAsync(userId, ct)` inside, which makes an HTTP GET to `/admin/realms/{Realm}/users/{userId}/role-mappings/realm` per user. The audit's line numbers (155-195) are close to the actual range (157-204).

The audit correctly notes this results in N+1 HTTP requests. However, the method already accepts `max` (default 20), so the practical upper bound is 21 requests. The remediation suggestion to parallelize with `Task.WhenAll` is the most practical fix since Keycloak's API does not natively batch role queries. The suggestion about `briefRepresentation=false` and `realmRoles` is not reliable across Keycloak versions.

**Severity: CRITICAL -- confirmed** (note: bounded by `max` parameter, but still significant for admin API latency)

---

### PERF-C3: MeteringMiddleware Runs Two Redis Roundtrips Per API Request

**Verdict: CONFIRMED with nuance**

**File:** `src/Modules/Billing/Wallow.Billing.Api/Middleware/MeteringMiddleware.cs:22-73`
**File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/ValkeyMeteringService.cs:63-101`

The middleware at line 32 calls `CheckQuotaAsync` and at line 71 calls `IncrementAsync` for every `/api/*` request. Inside `CheckQuotaAsync` (ValkeyMeteringService.cs:63-101):
1. Line 68: `GetActivePlanCodeAsync` -- DB query via subscription service
2. Line 71: `GetEffectiveQuotaAsync` -- DB query via quota repository
3. Line 86: `db.StringGetAsync(key)` -- Redis read for current counter
4. Lines 92-93: `CheckAndRaiseThresholdEventsAsync` -- additional Redis read for threshold key (line 113), plus potential DB lookup for meter definition (line 120)

Then `IncrementAsync` (lines 50-61) performs two more Redis calls: `StringIncrementAsync` and `KeyExpireAsync`.

The audit's description is accurate. The "20-50ms" latency estimate is reasonable given the 2 sequential DB queries + 2-3 Redis reads + 2 Redis writes per API request.

**Severity: CRITICAL -- confirmed**

---

## HIGH Findings

### PERF-H1: Unbounded Queries Without Pagination

**Verdict: CONFIRMED**

All files listed in the audit were verified:
- `InvoiceRepository.cs:29-35` (`GetByUserIdAsync`) -- confirmed, no pagination
- `InvoiceRepository.cs:38-44` (`GetAllAsync`) -- confirmed, no pagination
- `PaymentRepository.cs:22-28` (`GetByInvoiceIdAsync`) -- confirmed
- `PaymentRepository.cs:30-35` (`GetByUserIdAsync`) -- confirmed
- `PaymentRepository.cs:38-43` (`GetAllAsync`) -- confirmed
- `SubscriptionRepository.cs:23-29` (`GetByUserIdAsync`) and `GetAllAsync` -- confirmed
- `NotificationRepository.cs:28-33` (`GetByUserIdAsync`) -- confirmed (though `GetByUserIdPagedAsync` exists separately at line 36)
- `NotificationRepository.cs:59-64` (`GetUnreadByUserIdAsync`) -- confirmed
- `AnnouncementRepository.cs:23-32` (`GetPublishedAsync`) and `GetAllAsync` -- confirmed
- `FeatureFlagRepository.cs:29-33` (`GetAllAsync`) -- confirmed

The audit correctly notes that `PagedResult<T>` already exists in the codebase. The `NotificationRepository` even has a `GetByUserIdPagedAsync` method alongside the unbounded `GetByUserIdAsync`, demonstrating the pattern exists but is inconsistently applied.

**Severity: HIGH -- confirmed**

---

### PERF-H2: No AsNoTracking for Read-Only Queries

**Verdict: CONFIRMED**

A codebase-wide search for `AsNoTracking` and `QueryTrackingBehavior` returned zero matches across the entire `src/` directory. The audit's claim of "Zero uses of `AsNoTracking()` found in the entire codebase" is accurate. No DbContext is configured with `QueryTrackingBehavior.NoTracking` either.

This is a legitimate concern for read-heavy query paths where entities are loaded, mapped to DTOs, and never modified. All repository `Get*` methods used for queries (not writes) would benefit from `AsNoTracking()`.

**Severity: HIGH -- confirmed**

---

### PERF-H5: Multiple Redis Connection Multiplexers

**Verdict: CONFIRMED**

**File:** `src/Wallow.Api/Program.cs:192-225`

Three separate `ConnectionMultiplexer` instances are created:
1. Lines 192-198: `ConnectionMultiplexer.Connect(connectionString)` for `IConnectionMultiplexer` singleton
2. Lines 203-211: `ConnectionMultiplexer.ConnectAsync(connStr)` for `IDistributedCache`
3. Lines 216-225: `ConnectionMultiplexer.ConnectAsync(connStr, writer)` for SignalR backplane

The audit accurately describes this. Each creates a separate TCP connection pool. The remediation to share a single multiplexer is correct. The code comments explain *why* separate connections are created ("Defer connection to use final config (WebApplicationFactory overrides)"), which is a valid concern for testing but could be solved with a lazy-initialized shared singleton.

**Severity: HIGH -- confirmed**

---

### PERF-H6: UserQueryService Fetches All Org Members for Count Operations

**Verdict: CONFIRMED**

**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/UserQueryService.cs:21-92`

All three count methods (`GetNewUsersCountAsync` at line 21, `GetActiveUsersCountAsync` at line 49, `GetTotalUsersCountAsync` at line 76) call `GetOrganizationMembersAsync` which does a full HTTP GET to `/admin/realms/{Realm}/organizations/{orgId}/members` (line 96) and deserializes all members into a `List<UserMemberRepresentation>`. They then use LINQ `.Count()` in memory.

For `GetTotalUsersCountAsync`, the entire member list is loaded just to return `members?.Count ?? 0`.

The remediation to use Keycloak's count/query parameters is correct.

**Severity: HIGH -- confirmed**

---

## Severity-Adjusted Findings (Originally HIGH, Now MEDIUM)

### PERF-H3: FlushUsageJob Uses SCAN on Redis

**Original Severity: HIGH -> Adjusted: MEDIUM**

**File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/FlushUsageJob.cs:45-50`

The code uses `server.KeysAsync(pattern: "meter:*")` which is confirmed at line 47. However, the audit's description is misleading: `KeysAsync` in StackExchange.Redis uses `SCAN` (not `KEYS`), which is non-blocking and cursor-based. The audit mentions "SCAN" but then says it "blocks the Redis event loop" -- SCAN itself does not block; it pages through keys incrementally.

The real concern is the unbounded `List<RedisKey>` at line 46 that collects all matching keys into memory before processing. For a large number of tenants this could be problematic, but the remediation suggestion (use a Redis Set) is correct.

---

### PERF-H4: AuditInterceptor Registered as Singleton Creates Scopes Per Save

**Original Severity: HIGH -> Adjusted: MEDIUM**

**File:** `src/Shared/Wallow.Shared.Infrastructure/Auditing/AuditInterceptor.cs:24-35`
**Registration:** `src/Shared/Wallow.Shared.Infrastructure/Auditing/AuditingExtensions.cs:22` -- confirmed `services.AddSingleton<AuditInterceptor>()`

The interceptor is indeed a singleton (confirmed at AuditingExtensions.cs:22). It creates scopes at two points:
1. `CaptureChanges` (line 52): `_serviceProvider.CreateScope()` to get `IHttpContextAccessor` and `ITenantContext`
2. `SaveAuditEntriesAsync` (line 114): `_serviceProvider.CreateAsyncScope()` to get `AuditDbContext`

This is actually the **correct pattern** for a singleton interceptor that needs scoped services. The overhead of scope creation is minimal (~microseconds). The more significant concern is the serialization of ALL properties (addressed in PERF-M3).

The audit correctly identifies the pattern but overrates its impact. The two scopes per save are architecturally correct; registering as scoped wouldn't work because EF Core interceptors should be singletons when shared across DbContexts.

---

### PERF-H7: Duplicate TenantContext/Multi-tenancy Registration

**Original Severity: HIGH -> Adjusted: LOW**

**Files verified:**
- `src/Shared/Wallow.Shared.Kernel/Extensions/ServiceCollectionExtensions.cs:13-17` -- registers TenantContext, ITenantContext, ITenantContextSetter
- `src/Modules/Identity/Wallow.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:57-59` -- registers the same types again

The duplicate registration is confirmed. However, the audit's severity rating of HIGH is wrong:
- Last-registration-wins is well-defined DI behavior in Microsoft.Extensions.DependencyInjection
- There is zero runtime performance impact -- both register the exact same types with the same lifetimes
- Startup time impact is negligible (microseconds to add 3 service descriptors)
- The main risk is confusion during debugging, not performance

---

## MEDIUM Findings

### PERF-M1: BuildServiceProvider Anti-Pattern During Registration

**Verdict: CONFIRMED**

**File:** `src/Modules/Communications/Wallow.Communications.Infrastructure/Extensions/CommunicationsModuleExtensions.cs:119-124`

The code at lines 119-124 confirms `services.BuildServiceProvider()` inside `RegisterEmailProvider`, creating a temporary service provider solely to log a warning about an unrecognized email provider. This is indeed the ASP0000 anti-pattern.

**Severity: MEDIUM -- confirmed**

---

### PERF-M2: CachedFeatureFlagService Cache Invalidation Is Ineffective

**Verdict: CONFIRMED**

**File:** `src/Modules/Configuration/Wallow.Configuration.Infrastructure/Services/CachedFeatureFlagService.cs:54-60`

The `InvalidateAsync` method at line 59 removes key `ff:{flagKey}`. But `BuildCacheKey` at lines 62-65 builds keys as `ff:{flagKey}:{tenantId}:{userId}` or `ff:{flagKey}:{tenantId}:`. So `InvalidateAsync` removes a key that was never set -- the actual cached entries have the tenant/user suffix and are never invalidated. They rely entirely on TTL expiry (60 seconds).

The code even has a comment at line 56 acknowledging this: "Invalidate by removing known key patterns is impractical without scanning."

**Severity: MEDIUM -- confirmed**

---

### PERF-M3: Audit Interceptor Serializes All Entity Properties

**Verdict: CONFIRMED**

**File:** `src/Shared/Wallow.Shared.Infrastructure/Auditing/AuditInterceptor.cs:137-145`

The `SerializeValues` method at lines 137-145 iterates all properties without filtering. No exclusion of large text fields, JSONB columns, or binary data. The remediation suggestion to filter by type/size is appropriate.

**Severity: MEDIUM -- confirmed**

---

### PERF-M4: SmtpEmailProvider Creates New SmtpClient Per Send Attempt

**Verdict: CONFIRMED**

**File:** `src/Modules/Communications/Wallow.Communications.Infrastructure/Services/SmtpEmailProvider.cs:99-124`

Lines 99-124 confirm: `while (attempt < _settings.MaxRetries)` with `using SmtpClient client = new SmtpClient()` inside the loop at line 105, followed by `ConnectAsync`, `AuthenticateAsync`, `SendAsync`, and `DisconnectAsync` for each attempt. The remediation to move connection/auth outside the retry loop is correct.

**Severity: MEDIUM -- confirmed**

---

### PERF-M5: TenantSaveChangesInterceptor Iterates ChangeTracker Twice

**Verdict: CONFIRMED**

**File:** `src/Shared/Wallow.Shared.Kernel/MultiTenancy/TenantSaveChangesInterceptor.cs:38-57`

Lines 38-45 iterate `Entries<ITenantScoped>()` for Added entities, then lines 47-57 iterate again for Modified entities. The remediation to use a single enumeration with state check is correct.

**Severity: MEDIUM -- confirmed** (though practical impact is small for typical save operations with few tracked entities)

---

### PERF-M6: JSON Deserialization in Middleware Without Caching

**Verdict: FALSE POSITIVE**

**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs:80-93`
**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs:60-78`

The audit claims `realm_access` is parsed "up to twice (once in each middleware)." However, examining the actual code:

1. `TenantResolutionMiddleware` at line 88 parses `realm_access` only in the `HasRealmAdminRole` fallback path (line 80 checks standard role claims first). This is only reached for admin users when standard role claims aren't mapped.
2. `PermissionExpansionMiddleware` at line 66 parses `realm_access` only in the `else` branch when `standardRoles.Count` is 0 (line 54-58 checks standard roles first).

In practice, if standard role claims are present (which they should be with proper Keycloak JWT mapping), neither middleware parses `realm_access`. And even if both parse it, `JsonSerializer.Deserialize<JsonElement>` for a small JSON claim is negligible (~microseconds).

---

### PERF-M7: Dapper Queries Don't Use CancellationToken Consistently

**Verdict: CONFIRMED**

**File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/InvoiceQueryService.cs`
**File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/RevenueReportService.cs`

In `InvoiceQueryService.cs`, all four methods (`GetTotalRevenueAsync`:34, `GetCountAsync`:53, `GetPendingCountAsync`:71, `GetOutstandingAmountAsync`:89) accept `CancellationToken ct` but pass Dapper queries without wrapping in `CommandDefinition`. Example at line 34: `connection.QuerySingleAsync<decimal>(sql, new { ... })` -- no cancellation token passed.

In `RevenueReportService.cs`, line 44: `connection.QueryAsync<RevenueReportRow>(sql, new { ... })` -- same issue.

The audit correctly notes that `MessagingQueryService` does this correctly (using `CommandDefinition` with `cancellationToken`).

**Severity: MEDIUM -- confirmed**

---

### PERF-M8: ValkeyMeteringService.CheckQuotaAsync Performs Multiple Awaited DB Queries

**Verdict: CONFIRMED**

**File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Services/ValkeyMeteringService.cs:63-101`

Lines 68 and 71 show sequential `await` calls to `GetActivePlanCodeAsync` then `GetEffectiveQuotaAsync`. The second call depends on the first (needs `planCode`), so they cannot be fully parallelized. The `CancellationToken.None` usage at lines 68 and 74 is also confirmed -- the method signature doesn't accept a `CancellationToken`.

The audit's remediation note correctly acknowledges that these can't be parallelized due to the dependency, but caching would help.

**Severity: MEDIUM -- confirmed** (the sequential dependency is inherent; the real issue is no caching of plan/quota data)

---

## LOW Findings

### PERF-L1: GetAllInvoicesHandler Returns Unbounded Result Without Pagination

**Verdict: CONFIRMED**

**File:** `src/Modules/Billing/Wallow.Billing.Application/Queries/GetAllInvoices/GetAllInvoicesHandler.cs:11-18`

Line 15 calls `invoiceRepository.GetAllAsync(cancellationToken)` which loads all invoices. This is a duplicate of PERF-H1 at the handler level.

**Severity: LOW -- confirmed** (subsumed by PERF-H1)

---

### PERF-L2: RedisApiKeyService.ListApiKeysAsync Performs Sequential Redis Gets

**Verdict: CONFIRMED**

**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/RedisApiKeyService.cs:190-216`

Lines 190-213 confirm: `foreach (RedisValue keyId in keyIds)` with `await db.StringGetAsync(...)` at line 192 for each key. The remediation to use batched `StringGetAsync(RedisKey[])` is correct.

**Severity: LOW -- confirmed** (bounded by the number of API keys per user, which is typically small)

---

### PERF-L3: PermissionExpansionMiddleware Adds Claims Without Deduplication

**Verdict: FALSE POSITIVE**

**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs:84`

The audit claims duplicate claims can be added when roles overlap. However, `RolePermissionMapping.GetPermissions` at `RolePermissionMapping.cs:34` already calls `.Distinct()`. Permissions are deduplicated before claims are added.

---

### PERF-L4: FlushUsageJob SaveChanges Called Once for All Records

**Verdict: CONFIRMED**

**File:** `src/Modules/Billing/Wallow.Billing.Infrastructure/Jobs/FlushUsageJob.cs:68`

Line 68 calls `_usageRepository.SaveChangesAsync(cancellationToken)` once after the loop. The atomic get-and-reset at line 108 (`StringGetSetAsync(key, 0)`) means a failure after resetting Redis but before `SaveChangesAsync` would lose data. The suggestion to batch saves every N records is reasonable.

**Severity: LOW -- confirmed**

---

### PERF-L5: DateTime.UtcNow Used Instead of TimeProvider

**Verdict: CONFIRMED**

Confirmed in:
- `ValkeyMeteringService.cs:170` -- `DateTime.UtcNow`
- `MeteringMiddleware.cs:77,84` -- `DateTime.UtcNow` (twice)

This is a testability concern, not a performance issue. The audit correctly acknowledges this.

**Severity: LOW -- confirmed**

---

### PERF-H7 (reclassified): Duplicate TenantContext Registration

**Verdict: CONFIRMED but reclassified to LOW**

See severity-adjusted section above.

**Severity: LOW**

---

## NEW Findings (Missed by Original Audit)

### PERF-NEW-1: UserQueryService Makes Redundant Full Member Fetches for Multiple Count Operations

**File:** `src/Modules/Identity/Wallow.Identity.Infrastructure/Services/UserQueryService.cs:21-92`
**Severity: MEDIUM**

While PERF-H6 correctly identified that individual count methods fetch all members, it missed a compounding issue: if a dashboard or analytics endpoint calls `GetNewUsersCountAsync`, `GetActiveUsersCountAsync`, AND `GetTotalUsersCountAsync` in sequence, it fetches the entire member list three separate times. There is no caching or result sharing between these methods. Each call independently deserializes the full JSON response from Keycloak.

**Remediation:** Add a `GetOrganizationMembersCachedAsync` that caches the member list for a short duration (5-10 seconds) using `IMemoryCache`, or restructure the API to return all counts from a single method that fetches members once.

---

## Summary of Verification Results

| Finding | Original Severity | Verified Severity | Verdict |
|---------|------------------|-------------------|---------|
| PERF-C1 | CRITICAL | CRITICAL | CONFIRMED |
| PERF-C2 | CRITICAL | CRITICAL | CONFIRMED |
| PERF-C3 | CRITICAL | CRITICAL | CONFIRMED |
| PERF-H1 | HIGH | HIGH | CONFIRMED |
| PERF-H2 | HIGH | HIGH | CONFIRMED |
| PERF-H3 | HIGH | **MEDIUM** | SEVERITY ADJUSTED (SCAN is non-blocking) |
| PERF-H4 | HIGH | **MEDIUM** | SEVERITY ADJUSTED (scope creation is correct pattern) |
| PERF-H5 | HIGH | HIGH | CONFIRMED |
| PERF-H6 | HIGH | HIGH | CONFIRMED |
| PERF-H7 | HIGH | **LOW** | SEVERITY ADJUSTED (no runtime perf impact) |
| PERF-M1 | MEDIUM | MEDIUM | CONFIRMED |
| PERF-M2 | MEDIUM | MEDIUM | CONFIRMED |
| PERF-M3 | MEDIUM | MEDIUM | CONFIRMED |
| PERF-M4 | MEDIUM | MEDIUM | CONFIRMED |
| PERF-M5 | MEDIUM | MEDIUM | CONFIRMED |
| PERF-M6 | MEDIUM | -- | **FALSE POSITIVE** |
| PERF-M7 | MEDIUM | MEDIUM | CONFIRMED |
| PERF-M8 | MEDIUM | MEDIUM | CONFIRMED |
| PERF-L1 | LOW | LOW | CONFIRMED |
| PERF-L2 | LOW | LOW | CONFIRMED |
| PERF-L3 | LOW | -- | **FALSE POSITIVE** |
| PERF-L4 | LOW | LOW | CONFIRMED |
| PERF-L5 | LOW | LOW | CONFIRMED |
| PERF-NEW-1 | -- | MEDIUM | **NEW FINDING** |

## Final Verified Counts

| Severity | Count |
|----------|-------|
| CRITICAL | 3 |
| HIGH | 4 |
| MEDIUM | 9 |
| LOW | 5 |
| FALSE POSITIVE | 2 (PERF-M6, PERF-L3) |
| **Total Valid** | **21** |

## Revised Priority Remediation Roadmap

| Priority | Finding | Effort | Impact |
|----------|---------|--------|--------|
| 1 | PERF-C1: N+1 conversations query | Low | High - direct user-facing latency |
| 2 | PERF-H2: Add AsNoTracking globally | Low | High - memory reduction across all reads |
| 3 | PERF-C3: Metering middleware optimization | Medium | High - affects every API request |
| 4 | PERF-H1: Add pagination to unbounded queries | Medium | High - prevents memory exhaustion |
| 5 | PERF-C2: N+1 Keycloak user roles | Medium | Medium - admin-facing, bounded by page size |
| 6 | PERF-H5: Share Redis ConnectionMultiplexer | Low | Medium - reduces connection overhead |
| 7 | PERF-H6: UserQueryService count optimization | Low | Medium - admin dashboard latency |
| 8 | PERF-M7: Pass CancellationToken to Dapper | Low | Low - prevents resource waste on cancellation |
