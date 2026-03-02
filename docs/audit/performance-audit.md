# Performance and Memory Efficiency Audit

**Date:** 2026-03-02
**Scope:** All source files under `src/`
**Auditor:** perf-auditor

---

## Executive Summary

The Foundry codebase demonstrates solid architecture with good use of Dapper for read-heavy queries, Redis for metering/caching, and proper async patterns throughout. However, several performance issues were identified across N+1 queries, unbounded result sets, missing read-only tracking, and middleware inefficiencies that could significantly impact production performance at scale.

**Finding counts:** 3 CRITICAL, 7 HIGH, 8 MEDIUM, 5 LOW

---

## CRITICAL Findings

### PERF-C1: N+1 Query in GetConversationsAsync (Messaging)

**File:** `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/MessagingQueryService.cs:153-200`
**Impact:** Each conversation in the result set triggers an additional SQL query for participants. For a user with 50 conversations, this produces 51 queries instead of 1-2.

```csharp
foreach (ConversationRow row in rows)
{
    const string participantsSql = """
        SELECT user_id AS "UserId", ...
        FROM communications.participants
        WHERE conversation_id = @ConversationId
        """;

    IEnumerable<ParticipantDto> participants = await connection.QueryAsync<ParticipantDto>(
        new CommandDefinition(participantsSql, new { ConversationId = row.Id }, ...));
    // ...
}
```

**Remediation:** Batch-load participants for all conversations in a single query using `WHERE conversation_id = ANY(@ConversationIds)`, then group in memory:

```csharp
Guid[] conversationIds = rows.Select(r => r.Id).ToArray();
const string allParticipantsSql = """
    SELECT conversation_id AS "ConversationId", user_id AS "UserId", ...
    FROM communications.participants
    WHERE conversation_id = ANY(@ConversationIds)
    """;
IEnumerable<ParticipantRow> allParticipants = await connection.QueryAsync<ParticipantRow>(
    new CommandDefinition(allParticipantsSql, new { ConversationIds = conversationIds }, ...));
ILookup<Guid, ParticipantDto> lookup = allParticipants.ToLookup(p => p.ConversationId, p => ...);
```

---

### PERF-C2: N+1 Query in GetUsersAsync (Keycloak Admin)

**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/KeycloakAdminService.cs:155-195`
**Impact:** For each user returned by Keycloak, a separate HTTP call is made to `GetUserRolesAsync`. Listing 20 users means 21 HTTP requests to Keycloak.

```csharp
foreach (UserRepresentation user in users)
{
    Guid userId = Guid.Parse(user.Id);
    IReadOnlyList<string> roles = await GetUserRolesAsync(userId, ct); // HTTP call per user
    userDtos.Add(new UserDto(...));
}
```

**Remediation:** Keycloak supports retrieving users with roles in a single call using `briefRepresentation=false` and the `realmRoles` property, or use batch role-mapping endpoints. As a minimum, parallelize the calls:

```csharp
List<Task<(Guid UserId, IReadOnlyList<string> Roles)>> roleTasks = users
    .Where(u => !string.IsNullOrWhiteSpace(u.Id))
    .Select(async u => {
        Guid id = Guid.Parse(u.Id);
        IReadOnlyList<string> roles = await GetUserRolesAsync(id, ct);
        return (id, roles);
    }).ToList();
await Task.WhenAll(roleTasks);
```

---

### PERF-C3: MeteringMiddleware Runs Two Redis Roundtrips Per API Request

**File:** `src/Modules/Billing/Foundry.Billing.Api/Middleware/MeteringMiddleware.cs:30-62`
**Impact:** Every `/api/*` request performs a `CheckQuotaAsync` (which itself does 2-4 Redis reads + a DB query for quotas) before processing, then an `IncrementAsync` after. This adds 20-50ms latency to every API call.

```csharp
QuotaCheckResult quotaCheck = await meteringService.CheckQuotaAsync(ApiCallsMeterCode);
// ... process request ...
await meteringService.IncrementAsync(ApiCallsMeterCode);
```

Inside `CheckQuotaAsync` in `ValkeyMeteringService.cs:72-97`:
- Calls `_subscriptionQueryService.GetActivePlanCodeAsync` (DB query via repository)
- Calls `_quotaRepository.GetEffectiveQuotaAsync` (DB query)
- Reads Redis counter
- Reads Redis threshold key
- Potential threshold event publish + meter definition DB lookup

**Remediation:**
1. Cache quota definitions and subscription plan codes in Redis with TTL (they change rarely)
2. Use a Lua script to atomically check+increment in a single Redis roundtrip
3. Consider moving quota checking to a rate limiter with token bucket in Redis rather than per-request DB queries

---

## HIGH Findings

### PERF-H1: Unbounded Queries Without Pagination

**Files:**
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/InvoiceRepository.cs:29-36` -- `GetByUserIdAsync` loads ALL invoices
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/InvoiceRepository.cs:38-44` -- `GetAllAsync` loads ALL invoices
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/PaymentRepository.cs:21-28` -- `GetByInvoiceIdAsync` loads ALL payments
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/PaymentRepository.cs:30-37` -- `GetByUserIdAsync` loads ALL payments
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/PaymentRepository.cs:39-44` -- `GetAllAsync` loads ALL payments
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/SubscriptionRepository.cs:23-29` -- `GetByUserIdAsync` and `GetAllAsync`
- `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/NotificationRepository.cs:24-33` -- `GetByUserIdAsync` loads ALL notifications
- `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/NotificationRepository.cs:55-64` -- `GetUnreadByUserIdAsync` loads ALL unread
- `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/AnnouncementRepository.cs:21-33` -- `GetPublishedAsync` and `GetAllAsync`
- `src/Modules/Configuration/Foundry.Configuration.Infrastructure/Persistence/Repositories/FeatureFlagRepository.cs:26-33` -- `GetAllAsync`

**Impact:** As data grows, these queries will load entire tables into memory. A tenant with 10,000 invoices will load all of them for `GetAllAsync`.

**Remediation:** Add pagination parameters to all list queries. The codebase already has `PagedResult<T>` in `src/Shared/Foundry.Shared.Kernel/Pagination/PagedResult.cs` -- use it consistently:

```csharp
public async Task<PagedResult<Invoice>> GetByUserIdAsync(
    Guid userId, int page, int pageSize, CancellationToken ct = default)
{
    IQueryable<Invoice> query = _context.Invoices
        .Where(i => i.UserId == userId)
        .OrderByDescending(i => i.CreatedAt);
    int total = await query.CountAsync(ct);
    List<Invoice> items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    return new PagedResult<Invoice>(items, total, page, pageSize);
}
```

---

### PERF-H2: No AsNoTracking for Read-Only Queries

**Files:** ALL repositories and query handlers across the entire codebase
**Impact:** Zero uses of `AsNoTracking()` found in the entire codebase. Every EF Core read query enables change tracking, which:
- Allocates tracking snapshots for every loaded entity
- Increases memory pressure proportionally to result set size
- Adds CPU overhead for identity resolution

**Remediation:** Add `.AsNoTracking()` to all read-only queries in repositories. For repositories that mix reads and writes, provide separate read methods:

```csharp
public Task<Invoice?> GetByIdReadOnlyAsync(InvoiceId id, CancellationToken ct = default)
{
    return _context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
}
```

Alternatively, configure the DbContext default to `QueryTrackingBehavior.NoTracking` and opt-in to tracking only when needed:

```csharp
options.UseNpgsql(connectionString);
options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
```

---

### PERF-H3: FlushUsageJob Uses KEYS Command on Redis

**File:** `src/Modules/Billing/Foundry.Billing.Infrastructure/Jobs/FlushUsageJob.cs:50-54`

```csharp
IServer server = _redis.GetServer(_redis.GetEndPoints().First());
List<RedisKey> keys = [];
await foreach (RedisKey key in server.KeysAsync(pattern: "meter:*"))
{
    keys.Add(key);
}
```

**Impact:** `KeysAsync` with `SCAN` is O(N) on the keyspace and blocks the Redis event loop on large datasets. Collecting all keys into a `List` also means unbounded memory allocation. In production with thousands of tenants, this could have thousands of keys.

**Remediation:** Use a Redis Set to track active meter keys instead of scanning:
```csharp
// On increment:
await db.SetAddAsync("active_meters", key);

// On flush:
RedisValue[] activeKeys = await db.SetMembersAsync("active_meters");
// Process each, then remove from set
```

---

### PERF-H4: AuditInterceptor Registered as Singleton Creates Scopes Per Save

**File:** `src/Shared/Foundry.Shared.Infrastructure/Auditing/AuditInterceptor.cs:24-35`
**Registration:** `src/Shared/Foundry.Shared.Infrastructure/Auditing/AuditingExtensions.cs:22` -- `services.AddSingleton<AuditInterceptor>()`

**Impact:** The interceptor is singleton but calls `_serviceProvider.CreateScope()` on every `SavingChangesAsync`. This is correct for avoiding captive dependency, but:
1. Creates a new DI scope on every save operation (extra allocation + disposal)
2. The AuditDbContext itself is scoped, meaning each audit write gets its own connection
3. Serializes ALL changed properties to JSON including potentially large text/binary fields

```csharp
private static string SerializeValues(PropertyValues propertyValues)
{
    Dictionary<string, object?> dict = new Dictionary<string, object?>();
    foreach (IProperty property in propertyValues.Properties)
    {
        dict[property.Name] = propertyValues[property]; // Includes ALL columns
    }
    return JsonSerializer.Serialize(dict);
}
```

**Remediation:**
1. Exclude large/binary properties from audit serialization
2. Consider batching audit writes or using a queue rather than synchronous DB writes during SaveChanges
3. Register as Scoped if interceptors are resolved per-DbContext to avoid scope creation overhead

---

### PERF-H5: Multiple Redis Connection Multiplexers

**File:** `src/Foundry.Api/Program.cs:192-224`
**Impact:** Three separate `ConnectionMultiplexer.Connect/ConnectAsync` calls for: (1) the singleton `IConnectionMultiplexer`, (2) the `IDistributedCache`, and (3) SignalR backplane. Each creates a new TCP connection pool to Redis.

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => {
    return ConnectionMultiplexer.Connect(connectionString);  // Connection #1
});
builder.Services.AddStackExchangeRedisCache(options => {
    options.ConnectionMultiplexerFactory = async () => {
        return await ConnectionMultiplexer.ConnectAsync(connStr);  // Connection #2
    };
});
builder.Services.AddSignalR().AddStackExchangeRedis(options => {
    options.ConnectionFactory = async writer => {
        return await ConnectionMultiplexer.ConnectAsync(connStr, writer);  // Connection #3
    };
});
```

**Remediation:** Share a single `IConnectionMultiplexer` across all Redis consumers:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => {
    string connStr = sp.GetRequiredService<IConfiguration>().GetConnectionString("Redis")!;
    return ConnectionMultiplexer.Connect(connStr);
});
builder.Services.AddStackExchangeRedisCache(options => {
    options.ConnectionMultiplexerFactory = () => {
        IConnectionMultiplexer mux = builder.Services.BuildServiceProvider()
            .GetRequiredService<IConnectionMultiplexer>();
        return Task.FromResult(mux);
    };
});
```

---

### PERF-H6: UserQueryService Fetches All Org Members for Count Operations

**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/UserQueryService.cs:20-100`
**Impact:** `GetNewUsersCountAsync`, `GetActiveUsersCountAsync`, and `GetTotalUsersCountAsync` all call `GetOrganizationMembersAsync` which deserializes the full member list into memory just to count/filter them. An org with 10,000 members downloads all member JSON for a simple count.

```csharp
List<UserMemberRepresentation>? members = await GetOrganizationMembersAsync(tenantId, ct);
int newUsersCount = members.Count(m => m.CreatedTimestamp >= fromTimestamp && ...);
```

**Remediation:** Use Keycloak's query parameters for filtering:
```csharp
// Use count endpoint or query with first/max=0 for count
// Or use search parameters: /members?first=0&max=0 returns with X-Total-Count header
```

---

### PERF-H7: Duplicate TenantContext/Multi-tenancy Registration

**Files:**
- `src/Shared/Foundry.Shared.Kernel/Extensions/ServiceCollectionExtensions.cs:13-17` -- registers `TenantContext`, `ITenantContext`, `ITenantContextSetter`
- `src/Modules/Identity/Foundry.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs:55-60` -- registers the SAME types again

**Impact:** Last registration wins in DI, meaning there are redundant service descriptors. Not a runtime error but wastes startup time and can cause confusion about which registration is active.

**Remediation:** Remove the duplicate registration in `IdentityInfrastructureExtensions` since `AddSharedKernel()` already handles it.

---

## MEDIUM Findings

### PERF-M1: BuildServiceProvider Anti-Pattern During Registration

**File:** `src/Modules/Communications/Foundry.Communications.Infrastructure/Extensions/CommunicationsModuleExtensions.cs:118-124`

```csharp
using (ServiceProvider sp = services.BuildServiceProvider())
{
    ILogger logger = sp.GetRequiredService<ILoggerFactory>()
        .CreateLogger("CommunicationsModule");
    LogUnrecognizedEmailProvider(logger, provider);
}
```

**Impact:** `BuildServiceProvider()` during service registration creates a temporary service provider, which:
- Instantiates singleton services prematurely (before all registrations complete)
- Creates a separate singleton scope that won't be disposed with the real provider
- Triggers CA1062 and ASP0000 analyzer warnings

**Remediation:** Use `ILogger` from `LoggerFactory.Create()` or defer logging to runtime:
```csharp
services.AddScoped<IEmailProvider, SmtpEmailProvider>();
// Log at startup time in InitializeCommunicationsModuleAsync instead
```

---

### PERF-M2: CachedFeatureFlagService Cache Invalidation Is Ineffective

**File:** `src/Modules/Configuration/Foundry.Configuration.Infrastructure/Services/CachedFeatureFlagService.cs:51-56`

```csharp
public static async Task InvalidateAsync(IDistributedCache cache, string flagKey)
{
    // Only removes ff:{flagKey} but actual keys are ff:{flagKey}:{tenantId}:{userId}
    await cache.RemoveAsync($"ff:{flagKey}");
}
```

**Impact:** Cache invalidation removes `ff:{flagKey}` but cached entries use the pattern `ff:{flagKey}:{tenantId}:{userId}`. This means cache entries are never actually invalidated -- they only expire via TTL (60s). Flag changes take up to 60 seconds to propagate.

**Remediation:** Either:
1. Use Redis key prefix scanning: `SCAN 0 MATCH ff:{flagKey}:*`
2. Or maintain a list of cached keys per flag for targeted invalidation
3. Or use a version counter in Redis: cache key includes version, increment version on update

---

### PERF-M3: Audit Interceptor Serializes All Entity Properties

**File:** `src/Shared/Foundry.Shared.Infrastructure/Auditing/AuditInterceptor.cs:103-111`

```csharp
private static string SerializeValues(PropertyValues propertyValues)
{
    Dictionary<string, object?> dict = new Dictionary<string, object?>();
    foreach (IProperty property in propertyValues.Properties)
    {
        dict[property.Name] = propertyValues[property];
    }
    return JsonSerializer.Serialize(dict);
}
```

**Impact:** Serializes every column including large text fields, JSON blobs, custom fields, etc. For entities with `CustomFields` (JSONB column), this double-serializes large JSON structures.

**Remediation:** Filter out navigation properties, large fields, and binary content:
```csharp
foreach (IProperty property in propertyValues.Properties)
{
    if (property.ClrType == typeof(byte[]) || property.GetMaxLength() > 1000)
        continue;
    dict[property.Name] = propertyValues[property];
}
```

---

### PERF-M4: SmtpEmailProvider Creates New SmtpClient Per Send Attempt

**File:** `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/SmtpEmailProvider.cs:97-111`

```csharp
while (attempt < _settings.MaxRetries)
{
    try
    {
        using SmtpClient client = new SmtpClient();  // New TCP connection per attempt
        await client.ConnectAsync(_settings.Host, _settings.Port, ...);
        await client.AuthenticateAsync(...);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
    // retry...
}
```

**Impact:** Each send creates a new TCP connection, performs TLS handshake, and authenticates. For high-volume email sending, this is a significant overhead.

**Remediation:** Use an SMTP connection pool or at minimum keep the connection alive across retries:
```csharp
using SmtpClient client = new SmtpClient();
await client.ConnectAsync(...);
await client.AuthenticateAsync(...);
while (attempt < _settings.MaxRetries)
{
    try { await client.SendAsync(message, ct); return; }
    catch { attempt++; await Task.Delay(...); }
}
await client.DisconnectAsync(true, ct);
```

---

### PERF-M5: TenantSaveChangesInterceptor Iterates ChangeTracker Twice

**File:** `src/Shared/Foundry.Shared.Kernel/MultiTenancy/TenantSaveChangesInterceptor.cs:37-54`

```csharp
IEnumerable<EntityEntry<ITenantScoped>> entries = context.ChangeTracker
    .Entries<ITenantScoped>()
    .Where(e => e.State == EntityState.Added);

foreach (EntityEntry<ITenantScoped>? entry in entries) { ... }

IEnumerable<EntityEntry<ITenantScoped>> modified = context.ChangeTracker
    .Entries<ITenantScoped>()
    .Where(e => e.State == EntityState.Modified);

foreach (EntityEntry<ITenantScoped>? entry in modified) { ... }
```

**Impact:** Two separate enumerations of the ChangeTracker entries. Each `Entries<T>()` call creates a snapshot of tracked entities.

**Remediation:** Single enumeration with state check:
```csharp
foreach (EntityEntry<ITenantScoped> entry in context.ChangeTracker.Entries<ITenantScoped>())
{
    if (entry.State == EntityState.Added)
        entry.Entity.TenantId = _tenantContext.TenantId;
    else if (entry.State == EntityState.Modified && entry.Property(nameof(ITenantScoped.TenantId)).IsModified)
        entry.Property(nameof(ITenantScoped.TenantId)).IsModified = false;
}
```

---

### PERF-M6: JSON Deserialization in Middleware Without Caching

**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs:63-90`
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs:56-72`

Both middleware deserialize `realm_access` JSON claim on every authenticated request:
```csharp
JsonElement parsed = JsonSerializer.Deserialize<JsonElement>(realmAccess);
```

**Impact:** The `realm_access` claim value doesn't change during a request, but is parsed up to twice (once in each middleware). For every authenticated request, this is redundant JSON parsing.

**Remediation:** Store parsed result in `HttpContext.Items` after first parse:
```csharp
const string CacheKey = "ParsedRealmAccess";
if (!context.Items.TryGetValue(CacheKey, out object? cached))
{
    cached = JsonSerializer.Deserialize<JsonElement>(realmAccess);
    context.Items[CacheKey] = cached;
}
```

---

### PERF-M7: Dapper Queries Don't Use CancellationToken Consistently

**Files:**
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/InvoiceQueryService.cs` -- all methods accept `CancellationToken ct` but don't pass it to Dapper
- `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/RevenueReportService.cs:31-48` -- same issue

```csharp
public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
{
    // ct is never passed to Dapper
    decimal result = await connection.QuerySingleAsync<decimal>(sql, new { ... });
}
```

**Impact:** Cancelled requests continue executing queries against the database, wasting resources.

**Remediation:** Use `CommandDefinition` with cancellation token (as done correctly in `MessagingQueryService`):
```csharp
decimal result = await connection.QuerySingleAsync<decimal>(
    new CommandDefinition(sql, new { ... }, cancellationToken: ct));
```

---

### PERF-M8: ValkeyMeteringService.CheckQuotaAsync Performs Multiple Awaited DB Queries

**File:** `src/Modules/Billing/Foundry.Billing.Infrastructure/Services/ValkeyMeteringService.cs:72-97`

```csharp
string? planCode = await _subscriptionQueryService.GetActivePlanCodeAsync(tenantId.Value, CancellationToken.None);
QuotaDefinition? quota = await _quotaRepository.GetEffectiveQuotaAsync(meterCode, planCode, CancellationToken.None);
// Then Redis reads...
```

**Impact:** Sequential await of subscription lookup + quota lookup + Redis reads. These could run in parallel. Also uses `CancellationToken.None` instead of accepting/propagating the caller's token.

**Remediation:** Run independent operations in parallel and accept cancellation token:
```csharp
Task<string?> planTask = _subscriptionQueryService.GetActivePlanCodeAsync(tenantId.Value, ct);
// Can't parallelize quota until we have planCode, but could cache both
```

---

## LOW Findings

### PERF-L1: GetAllInvoicesHandler Returns Unbounded Result Without Pagination

**File:** `src/Modules/Billing/Foundry.Billing.Application/Queries/GetAllInvoices/GetAllInvoicesHandler.cs:12-16`

This is an admin endpoint but still loads all invoices with line items into memory. Combine with PERF-H1 -- the handler should enforce pagination.

---

### PERF-L2: RedisApiKeyService.ListApiKeysAsync Performs Sequential Redis Gets

**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/RedisApiKeyService.cs:190-216`

```csharp
foreach (RedisValue keyId in keyIds)
{
    RedisValue json = await db.StringGetAsync($"{KeyPrefix}id:{keyId}");  // One roundtrip per key
}
```

**Remediation:** Use `StringGetAsync` with multiple keys or Redis pipeline:
```csharp
RedisKey[] redisKeys = keyIds.Select(k => (RedisKey)$"{KeyPrefix}id:{k}").ToArray();
RedisValue[] values = await db.StringGetAsync(redisKeys);
```

---

### PERF-L3: PermissionExpansionMiddleware Adds Claims Without Deduplication

**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/PermissionExpansionMiddleware.cs:78-82`

If a user has multiple roles that map to overlapping permissions, duplicate claims are added:
```csharp
foreach (PermissionType permission in permissions)
{
    identity?.AddClaim(new Claim("permission", permission.ToString()));
}
```

**Impact:** Minor memory overhead from duplicate claims, and potentially slightly slower claim lookups.

**Remediation:** Use `Distinct()` on permissions before adding claims.

---

### PERF-L4: FlushUsageJob SaveChanges Called Once for All Records

**File:** `src/Modules/Billing/Foundry.Billing.Infrastructure/Jobs/FlushUsageJob.cs:72`

`SaveChangesAsync` is called once after processing all keys, which is good for batching but means a failure partway through loses all progress since the last successful save.

**Remediation:** Consider batching saves every N records to balance performance and reliability.

---

### PERF-L5: DateTime.UtcNow Used Instead of TimeProvider

**Files:** Multiple files throughout the codebase use `DateTime.UtcNow` directly instead of the injected `TimeProvider`:
- `ValkeyMeteringService.cs`
- `MeteringMiddleware.cs`
- `AuditInterceptor.cs`
- `SignalRNotificationService.cs`

**Impact:** Not a performance issue per se, but `TimeProvider` was already registered as a singleton. Using it consistently would improve testability without any perf cost.

---

## What's Done Well

1. **Dapper for complex reads** -- Billing and Communications modules correctly use Dapper for complex queries (revenue reports, conversations, messaging) avoiding EF Core overhead for read-heavy paths.

2. **Redis-based metering** -- The Valkey metering service uses Redis counters for sub-millisecond quota tracking with atomic operations. The key expiration pattern prevents unbounded growth.

3. **Consistent async/await** -- No sync-over-async patterns (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`) found in the entire codebase.

4. **Source-generated logging** -- Extensive use of `[LoggerMessage]` attribute for zero-allocation logging throughout all modules.

5. **HttpClient via IHttpClientFactory** -- All HTTP clients (Keycloak, Twilio) are properly created via `IHttpClientFactory`, avoiding socket exhaustion.

6. **Pagination where it matters** -- `NotificationRepository.GetByUserIdPagedAsync` and `MessagingQueryService.GetConversationsAsync` properly implement pagination.

7. **Proper DI lifetimes** -- No captive dependency issues (scoped in singleton) detected. Singletons correctly manage scopes when needed (AuditInterceptor).

8. **Proper resource disposal** -- `using` statements for disposable resources (`SmtpClient`, `HttpRequestMessage`, `FormUrlEncodedContent`, `JsonDocument`) are consistently used.

9. **Efficient tenant query filters** -- `TenantAwareDbContext` applies global query filters at the expression tree level, so tenant filtering is pushed down to SQL.

10. **Fire-and-forget for non-critical updates** -- `RedisApiKeyService.ValidateApiKeyAsync` uses `_ = UpdateLastUsedAsync(...)` for non-critical timestamp updates.

---

## Priority Remediation Roadmap

| Priority | Finding | Effort | Impact |
|----------|---------|--------|--------|
| 1 | PERF-C1: N+1 conversations query | Low | High - direct user-facing latency |
| 2 | PERF-H2: Add AsNoTracking globally | Low | High - memory reduction across all reads |
| 3 | PERF-C3: Metering middleware Redis optimization | Medium | High - affects every API request |
| 4 | PERF-H1: Add pagination to unbounded queries | Medium | High - prevents memory exhaustion |
| 5 | PERF-C2: N+1 Keycloak user roles | Medium | Medium - admin-facing but impactful |
| 6 | PERF-H3: Replace KEYS with Set tracking | Low | Medium - prevents Redis blocking |
| 7 | PERF-H5: Share Redis ConnectionMultiplexer | Low | Medium - reduces connection overhead |
| 8 | PERF-M7: Pass CancellationToken to Dapper | Low | Low - prevents resource waste on cancellation |
