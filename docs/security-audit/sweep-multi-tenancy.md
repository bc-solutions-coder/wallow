# Multi-Tenancy Isolation Security Audit

**Date:** 2026-03-03
**Auditor:** tenant-scout (automated security sweep)
**Scope:** All multi-tenancy isolation mechanisms across the Foundry modular monolith

---

## Executive Summary

The multi-tenancy architecture is **generally well-designed** with defense-in-depth: EF Core global query filters, a SaveChanges interceptor that stamps TenantId on new entities and prevents TenantId modification on updates, and consistent use of TenantId in Dapper queries. However, several findings require attention, ranging from medium-severity design risks to low-severity informational items.

**Critical:** 0 | **High:** 1 | **Medium:** 5 | **Low:** 4

---

## Architecture Overview

### Tenant Resolution Flow
1. `ApiKeyAuthenticationMiddleware` -- sets tenant from API key validation result
2. `UseAuthentication()` -- Keycloak OIDC JWT validation
3. `TenantResolutionMiddleware` -- reads `organization` claim from JWT, populates `ITenantContext`
4. Admin override via `X-Tenant-Id` header (requires `admin` realm role)

### Tenant Enforcement Layers
- **EF Core Global Query Filters:** `TenantAwareDbContext.ApplyTenantQueryFilters()` adds `WHERE TenantId = _tenantId` to all `ITenantScoped` entities
- **SaveChanges Interceptor:** `TenantSaveChangesInterceptor` stamps TenantId on Added entities; blocks TenantId modification on Modified entities
- **Dapper Queries:** Manual `WHERE TenantId = @TenantId` in all raw SQL services
- **Wolverine Messages:** `TenantStampingMiddleware` (outbound) / `TenantRestoringMiddleware` (inbound) propagate TenantId via `X-Tenant-Id` header

### DI Registration
`TenantContext` is registered as **Scoped**, meaning one instance per HTTP request. Both `ITenantContext` and `ITenantContextSetter` resolve to the same scoped instance.

---

## Findings

### HIGH-1: Presence Service Has No Tenant Isolation

**Severity:** HIGH
**File:** `src/Foundry.Api/Services/RedisPresenceService.cs`
**Lines:** 10-172

**Description:** The `RedisPresenceService` uses a single global Redis hash (`presence:conn2user`) and global key patterns (`presence:user:{userId}`, `presence:page:{pageContext}`) with **no tenant segmentation**. This means:

1. `GetOnlineUsersAsync()` returns **all online users across all tenants**.
2. `GetUsersOnPageAsync()` returns **all users on a page across all tenants**.
3. `IsUserOnlineAsync()` checks presence globally, not per-tenant.

**Impact:** A user in Tenant A can see the presence/online status of users in Tenant B. If presence data is exposed via the RealtimeHub, this leaks cross-tenant user activity.

**Code Snippet:**
```csharp
// src/Foundry.Api/Services/RedisPresenceService.cs:96-112
public async Task<IReadOnlyList<UserPresence>> GetOnlineUsersAsync(...)
{
    IDatabase db = Db;
    HashEntry[] allEntries = await db.HashGetAllAsync(ConnectionToUserKey); // ALL tenants
    // ...groups by userId with no tenant filter...
}
```

**Recommendation:** Prefix all Redis presence keys with tenant ID: `presence:{tenantId}:conn2user`, `presence:{tenantId}:user:{userId}`, etc. The `RealtimeHub` already has `ITenantContext` injected, so pass the tenant ID to the presence service.

---

### MEDIUM-1: Entities Without ITenantScoped May Leak Cross-Tenant Data

**Severity:** MEDIUM
**Files:**
- `src/Modules/Configuration/Foundry.Configuration.Domain/Entities/FeatureFlag.cs:14` -- `FeatureFlag : AggregateRoot<FeatureFlagId>` (no `ITenantScoped`)
- `src/Modules/Configuration/Foundry.Configuration.Domain/Entities/FeatureFlagOverride.cs:9` -- `FeatureFlagOverride : Entity<FeatureFlagOverrideId>` (no `ITenantScoped`)
- `src/Modules/Billing/Foundry.Billing.Domain/Metering/Entities/MeterDefinition.cs:11` -- `MeterDefinition : AuditableEntity<MeterDefinitionId>` (no `ITenantScoped`)
- `src/Modules/Billing/Foundry.Billing.Domain/Entities/InvoiceLineItem.cs:10` -- `InvoiceLineItem : Entity<InvoiceLineItemId>` (no `ITenantScoped`)
- `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Entities/ChangelogEntry.cs:7` -- `ChangelogEntry : AggregateRoot<ChangelogEntryId>` (no `ITenantScoped`)
- `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Entities/ChangelogItem.cs:7` -- (no `ITenantScoped`)
- `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Entities/AnnouncementDismissal.cs:7` -- (no `ITenantScoped`)
- `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Entities/Message.cs:7` -- (no `ITenantScoped`)
- `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Entities/Participant.cs:6` -- (no `ITenantScoped`)
- `src/Modules/Identity/Foundry.Identity.Domain/Entities/ApiScope.cs:10` -- (no `ITenantScoped`)

**Description:** These entities do not implement `ITenantScoped`, which means:
- No automatic global query filter is applied by `TenantAwareDbContext`
- No automatic TenantId stamping by `TenantSaveChangesInterceptor`

**Analysis:** Some of these are intentionally global:
- `MeterDefinition` and `FeatureFlag` appear to be system-wide definitions (not tenant-specific data)
- `InvoiceLineItem` is a child of `Invoice` (which IS tenant-scoped), so it's indirectly protected via navigation
- `Message` and `Participant` are children of `Conversation` (which IS tenant-scoped)

**Risk:** The indirect protection via parent entity navigation only works if queries always go through the parent. Direct queries on `Message`, `Participant`, or `InvoiceLineItem` tables would bypass tenant isolation.

**Recommendation:**
- For child entities (`Message`, `Participant`, `InvoiceLineItem`): Consider adding `ITenantScoped` for defense-in-depth, or document the intentional design decision.
- For `FeatureFlag` and `FeatureFlagOverride`: Verify these are intentionally global. If tenants should have their own feature flags, add `ITenantScoped`.
- For `ChangelogEntry`/`ChangelogItem`: These appear to be global announcements, which may be intentional.

---

### MEDIUM-2: Admin Tenant Override Lacks Audit Trail Persistence

**Severity:** MEDIUM
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs`
**Lines:** 40-48

**Description:** The `X-Tenant-Id` header allows any user with the `admin` realm role to override their tenant context. While this is guarded by `HasRealmAdminRole()`, the check only looks for a generic "admin" role:

```csharp
private const string AdminRole = "admin";

private static bool HasRealmAdminRole(ClaimsPrincipal user)
{
    if (user.FindAll(ClaimTypes.Role).Any(c =>
            string.Equals(c.Value, AdminRole, StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }
    // Fallback: parse Keycloak realm_access claim
    // ...
}
```

**Issues:**
1. The "admin" role is very broad. Any user with the admin role in ANY tenant can override to ANY other tenant. There is no validation that the target tenant exists or that the admin has authority over it.
2. The override is only logged (`LogAdminTenantOverride`) but there's no persistent audit trail beyond Serilog output. If a malicious admin overrides to another tenant, the evidence is only in logs.
3. No rate limiting on tenant override usage.

**Recommendation:**
- Consider requiring a more specific role like `super_admin` or `platform_admin` for cross-tenant access.
- Validate that the target tenant ID actually exists.
- Persist admin override events to a database audit table.

---

### MEDIUM-3: Dapper Participants Query Missing Tenant Filter

**Severity:** MEDIUM
**File:** `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/MessagingQueryService.cs`
**Lines:** 205-216

**Description:** In `GetConversationsAsync()`, the participants sub-query fetches participants by `conversation_id` array without a tenant filter:

```sql
SELECT conversation_id AS "ConversationId",
       user_id AS "UserId",
       joined_at AS "JoinedAt",
       last_read_at AS "LastReadAt",
       is_active AS "IsActive"
FROM communications.participants
WHERE conversation_id = ANY(@ConversationIds)
```

**Analysis:** The outer query correctly filters conversations by `c.tenant_id = @TenantId`, so `ConversationIds` should only contain tenant-scoped IDs. However, this is an implicit dependency -- if the participant sub-query is ever extracted or reused independently, it would lack tenant isolation.

Additionally, the `Participant` entity does not implement `ITenantScoped`, so there is no EF Core query filter as a safety net for EF-based queries on participants.

**Recommendation:** While the current code is functionally safe due to the outer query filter, adding `AND p.tenant_id = @TenantId` (after adding a `tenant_id` column to participants) or using a JOIN to conversations would provide defense-in-depth.

---

### MEDIUM-4: ApiKeyAuthenticationMiddleware Sets TenantContext Directly

**Severity:** MEDIUM
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Authorization/ApiKeyAuthenticationMiddleware.cs`
**Lines:** 89-94

**Description:** The API key middleware sets tenant context by directly mutating properties instead of using the `ITenantContextSetter.SetTenant()` method:

```csharp
// Set tenant context (same pattern as TenantResolutionMiddleware)
tenantContext.TenantId = TenantId.Create(result.TenantId!.Value);
tenantContext.TenantName = $"api-key-{result.KeyId}";
tenantContext.IsResolved = true;
```

Note that it injects the concrete `TenantContext` class, not the `ITenantContextSetter` interface:
```csharp
public async Task InvokeAsync(
    HttpContext context,
    IApiKeyService apiKeyService,
    TenantContext tenantContext)  // Concrete type, not interface
```

**Issues:**
1. Bypasses the `SetTenant()` method, which means if validation or logging is ever added to `SetTenant()`, API key authentication won't benefit.
2. The `Region` property is not set, defaulting to the primary region. This may be incorrect if the API key belongs to a tenant in a different region.
3. After API key auth sets the tenant, `TenantResolutionMiddleware` still runs and could potentially override the tenant context if the JWT also has organization claims (though unlikely for API key requests since authentication is set before JWT processing).

**Recommendation:** Use `ITenantContextSetter` interface and call `SetTenant()` method for consistency. Consider adding a guard in `TenantResolutionMiddleware` to skip if tenant is already resolved by API key auth.

---

### MEDIUM-5: TenantQueryExtensions.AllTenants() Is Too Easily Accessible

**Severity:** MEDIUM
**File:** `src/Shared/Foundry.Shared.Kernel/MultiTenancy/TenantQueryExtensions.cs`
**Lines:** 7-14

**Description:** The `AllTenants<T>()` extension method is a public static method in the shared kernel, accessible from any layer in any module:

```csharp
public static IQueryable<T> AllTenants<T>(this IQueryable<T> query) where T : class
{
    return query.IgnoreQueryFilters();
}
```

**Issues:**
1. Any developer can call `.AllTenants()` on any query, bypassing tenant isolation. It wraps `IgnoreQueryFilters()` which also removes any other global filters (e.g., soft-delete filters if added later).
2. No runtime check that the caller has admin/system-level permissions.
3. No logging when tenant filters are bypassed.

**Current usages in production code:**
- None found in `src/` (only in tests and the `IgnoreQueryFilters()` calls are used directly)

**Recommendation:**
- Consider making this method internal to the shared kernel or moving it to an admin-specific namespace.
- Add logging when `AllTenants()` is invoked in production.
- Consider an architecture test that restricts `AllTenants()`/`IgnoreQueryFilters()` usage to specific admin service classes.

---

### LOW-1: Unauthenticated Requests Have Empty TenantContext

**Severity:** LOW
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/MultiTenancy/TenantResolutionMiddleware.cs`
**Lines:** 22-23

**Description:** When a request is unauthenticated (`context.User.Identity?.IsAuthenticated != true`), the middleware does nothing -- it simply calls `_next(context)`. The `TenantContext` remains in its default state: `TenantId = default (Guid.Empty)`, `IsResolved = false`.

**Analysis:** This is generally safe because:
1. The `TenantSaveChangesInterceptor.SetTenantId()` checks `_tenantContext.IsResolved` and skips if false.
2. The EF Core query filter uses the `_tenantId` field value (which would be `Guid.Empty` for unresolved), so queries would match nothing or only system-level data.

**Risk:** If any endpoint is `[AllowAnonymous]` but still accesses tenant-scoped data, it would operate with `TenantId = Guid.Empty`. Current `[AllowAnonymous]` endpoints are: AuthController, ChangelogController, ScimController, health checks, and OpenAPI docs -- none of which appear to access tenant-scoped EF data.

**Recommendation:** No immediate action required. Consider adding an `EnsureTenantResolved()` guard to repositories or DbContext that throws if `IsResolved == false` when accessing tenant-scoped data.

---

### LOW-2: Redis API Key Storage Has No Tenant Namespace Isolation

**Severity:** LOW
**File:** `src/Modules/Identity/Foundry.Identity.Infrastructure/Services/RedisApiKeyService.cs`
**Lines:** 18-19, 120-125

**Description:** API keys are stored in Redis with a flat namespace (`apikey:{hash}`, `apikeys:user:{userId}`) without tenant prefixing. The `ServiceAccountRepository` also uses `IgnoreQueryFilters()` for cross-tenant lookups.

```csharp
private const string KeyPrefix = "apikey:";
private const string UserKeysPrefix = "apikeys:user:";
```

**Analysis:** This is likely intentional since API key validation must work before tenant context is established (the middleware uses the key to determine the tenant). The `IgnoreQueryFilters()` in `GetByKeycloakClientIdAsync()` is also correct for this reason.

**Risk:** Minimal. The API key hash is unique per key regardless of tenant. However, `GetUserKeysAsync()` returns all keys for a userId without tenant filtering, meaning if user IDs overlap across tenants (unlikely with Keycloak GUIDs), keys could be listed across tenants.

**Recommendation:** No immediate action. The design is correct for the authentication flow.

---

### LOW-3: Wolverine TenantRestoringMiddleware Trusts Message Headers

**Severity:** LOW
**File:** `src/Shared/Foundry.Shared.Infrastructure.Core/Middleware/TenantRestoringMiddleware.cs`
**Lines:** 9-18

**Description:** The inbound middleware blindly trusts the `X-Tenant-Id` header from Wolverine message envelopes:

```csharp
public static void Before(Envelope envelope, ITenantContextSetter tenantContextSetter)
{
    if (!envelope.Headers.TryGetValue("X-Tenant-Id", out string? tenantHeader))
        return;

    if (Guid.TryParse(tenantHeader, out Guid tenantGuid))
        tenantContextSetter.SetTenant(TenantId.Create(tenantGuid));
}
```

**Analysis:** Since messages flow through RabbitMQ (an internal transport), the header is set by the trusted `TenantStampingMiddleware` on the publishing side. External actors cannot inject messages unless they have RabbitMQ credentials.

**Risk:** If RabbitMQ credentials are compromised, an attacker could publish messages with arbitrary tenant IDs. This is an infrastructure-level concern, not a code-level one.

**Recommendation:** No immediate action for code. Ensure RabbitMQ credentials are properly secured and rotated.

---

### LOW-4: SignalR Group Validation Only Applies to "tenant:" Prefixed Groups

**Severity:** LOW
**File:** `src/Foundry.Api/Hubs/RealtimeHub.cs`
**Lines:** 87-108

**Description:** `ValidateTenantGroup()` only validates groups that start with `"tenant:"`. Other group patterns (e.g., `page:`, custom groups) have no tenant validation:

```csharp
private void ValidateTenantGroup(string groupId)
{
    if (!groupId.StartsWith("tenant:", StringComparison.Ordinal))
    {
        return; // No validation for non-tenant groups
    }
    // ... validates tenant match ...
}
```

**Analysis:** The `page:` groups are used for page-context presence, which is global (not tenant-scoped). However, if a client sends a custom group name like `"my-custom-group"`, there's no restriction.

**Risk:** Low, since SignalR groups are just broadcast channels (they don't expose data directly). A user joining a non-tenant group can only receive messages broadcast to that group, which requires server-side code to send.

**Recommendation:** Consider using an allowlist of valid group prefixes (`tenant:`, `page:`) and rejecting unknown patterns.

---

## Positive Findings

### Well-Implemented Tenant Isolation

1. **EF Core Query Filters:** `TenantAwareDbContext.ApplyTenantQueryFilters()` correctly uses expression trees to bind the `_tenantId` field (not a captured constant), ensuring the filter uses the current request's tenant ID dynamically.

2. **SaveChanges Interceptor:** `TenantSaveChangesInterceptor` properly:
   - Stamps TenantId on Added entities
   - Blocks TenantId modification on Modified entities (sets `IsModified = false`)
   - Checks `IsResolved` before acting

3. **All Dapper Queries Use TenantId:** Every Dapper-based service (`InvoiceReportService`, `PaymentReportService`, `RevenueReportService`, `InvoiceQueryService`, `MessagingQueryService`) correctly includes `WHERE ... TenantId = @TenantId` using `_tenantContext.TenantId.Value`.

4. **Feature Flag Cache Keys Include TenantId:** `CachedFeatureFlagService.BuildCacheKey()` correctly includes tenant ID in cache keys: `ff:{prefix}:{flagKey}:{tenantId}:{userId}`.

5. **Metering Keys Include TenantId:** `ValkeyMeteringService` uses `meter:{tenantId}:{meterCode}:{period}` keys, ensuring per-tenant metering isolation.

6. **MeteringMiddleware Cache Keys Include TenantId:** `MeteringMiddleware` uses `quota:{tenantContext.TenantId.Value}:{meterCode}` for quota cache keys.

7. **SignalR Tenant Group Validation:** `RealtimeHub.ValidateTenantGroup()` correctly prevents cross-tenant group joining by comparing group tenant ID against the user's tenant context.

8. **FlushUsageJob Uses TenantContextFactory:** The background job correctly parses tenant ID from Redis keys and creates a tenant scope before accessing the repository.

9. **Scoped TenantContext:** DI registration as Scoped ensures per-request isolation.

---

## Summary Table

| ID | Severity | Finding | File |
|----|----------|---------|------|
| HIGH-1 | HIGH | Presence service has no tenant isolation | `RedisPresenceService.cs` |
| MEDIUM-1 | MEDIUM | Multiple entities missing ITenantScoped | Various domain entities |
| MEDIUM-2 | MEDIUM | Admin tenant override lacks robust authorization | `TenantResolutionMiddleware.cs` |
| MEDIUM-3 | MEDIUM | Participants Dapper query missing tenant filter | `MessagingQueryService.cs` |
| MEDIUM-4 | MEDIUM | API key middleware sets TenantContext directly | `ApiKeyAuthenticationMiddleware.cs` |
| MEDIUM-5 | MEDIUM | AllTenants() extension too easily accessible | `TenantQueryExtensions.cs` |
| LOW-1 | LOW | Unresolved tenant context on anonymous requests | `TenantResolutionMiddleware.cs` |
| LOW-2 | LOW | Redis API key storage has no tenant namespace | `RedisApiKeyService.cs` |
| LOW-3 | LOW | Wolverine middleware trusts message headers | `TenantRestoringMiddleware.cs` |
| LOW-4 | LOW | SignalR group validation only for tenant: prefix | `RealtimeHub.cs` |
