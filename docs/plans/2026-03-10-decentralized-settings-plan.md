# Decentralized Settings Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the centralized Configuration module with per-module settings, custom fields, and `Microsoft.FeatureManagement` for dev gates.

**Architecture:** Each module owns its own `tenant_settings` and `user_settings` tables. Shared infrastructure in `Shared.Kernel` and `Shared.Infrastructure` provides base entities, repositories, caching, and key validation. `Microsoft.FeatureManagement` handles development-level feature gating via `appsettings.json`. The existing `AuditInterceptor` handles change tracking.

**Tech Stack:** .NET 10, EF Core, PostgreSQL, Valkey (HybridCache), Wolverine, Microsoft.FeatureManagement

**Design Doc:** `docs/plans/2026-03-10-decentralized-settings-design.md`

---

## Phase 1: Shared Kernel — Setting Definitions and Interfaces

Build the foundational types that all modules will depend on.

### Task 1.1: Create SettingDefinition<T> in Shared.Kernel

**Files:**
- Create: `src/Shared/Foundry.Shared.Kernel/Settings/SettingDefinition.cs`

**Description:**
Create the strongly-typed setting definition record that modules use to declare their settings. Each definition holds a key, default value, description, and type information.

```csharp
namespace Foundry.Shared.Kernel.Settings;

public sealed record SettingDefinition<T>(
    string Key,
    T DefaultValue,
    string Description)
{
    public Type ValueType => typeof(T);
}
```

**Commit:** `feat(kernel): add SettingDefinition<T> for per-module settings declarations`

---

### Task 1.2: Create ISettingRegistry Interface

**Files:**
- Create: `src/Shared/Foundry.Shared.Kernel/Settings/ISettingRegistry.cs`

**Description:**
Interface for a registry that collects all code-defined setting definitions for a module. Used by the settings service to validate keys and resolve defaults.

```csharp
namespace Foundry.Shared.Kernel.Settings;

public interface ISettingRegistry
{
    string ModuleName { get; }
    IReadOnlyDictionary<string, object> Defaults { get; }
    IReadOnlyDictionary<string, SettingMetadata> Metadata { get; }
    bool IsCodeDefinedKey(string key);
}

public sealed record SettingMetadata(
    string Key,
    string DisplayName,
    string Description,
    Type ValueType,
    object DefaultValue);
```

---

### Task 1.3: Create SettingRegistryBase Abstract Class

**Files:**
- Create: `src/Shared/Foundry.Shared.Kernel/Settings/SettingRegistryBase.cs`

**Description:**
Abstract base that modules inherit from. Uses reflection to discover all `SettingDefinition<T>` static fields in the derived class and build the key/default/metadata dictionaries.

```csharp
namespace Foundry.Shared.Kernel.Settings;

public abstract class SettingRegistryBase : ISettingRegistry
{
    public abstract string ModuleName { get; }

    private readonly Lazy<Dictionary<string, object>> _defaults;
    private readonly Lazy<Dictionary<string, SettingMetadata>> _metadata;

    protected SettingRegistryBase()
    {
        _defaults = new Lazy<Dictionary<string, object>>(BuildDefaults);
        _metadata = new Lazy<Dictionary<string, SettingMetadata>>(BuildMetadata);
    }

    public IReadOnlyDictionary<string, object> Defaults => _defaults.Value;
    public IReadOnlyDictionary<string, SettingMetadata> Metadata => _metadata.Value;

    public bool IsCodeDefinedKey(string key) => _defaults.Value.ContainsKey(key);

    // Reflect over all static SettingDefinition<T> fields in the concrete class
    private Dictionary<string, object> BuildDefaults() { ... }
    private Dictionary<string, SettingMetadata> BuildMetadata() { ... }
}
```

**Commit:** `feat(kernel): add ISettingRegistry and SettingRegistryBase for module setting discovery`

---

### Task 1.4: Create Key Namespace Validation

**Files:**
- Create: `src/Shared/Foundry.Shared.Kernel/Settings/SettingKeyValidator.cs`

**Description:**
Validates setting keys against the three namespaces: code-defined (validated), `custom.` (bypass), `system.` (platform admin only). Enforces the 100 custom key limit per tenant per module.

```csharp
namespace Foundry.Shared.Kernel.Settings;

public static class SettingKeyValidator
{
    public const string CustomPrefix = "custom.";
    public const string SystemPrefix = "system.";
    public const int MaxCustomKeysPerTenant = 100;

    public static bool IsCustomKey(string key) => key.StartsWith(CustomPrefix, StringComparison.Ordinal);
    public static bool IsSystemKey(string key) => key.StartsWith(SystemPrefix, StringComparison.Ordinal);
    public static bool IsCodeDefinedKey(string key, ISettingRegistry registry) => registry.IsCodeDefinedKey(key);

    public static SettingKeyValidationResult Validate(string key, ISettingRegistry registry)
    {
        if (IsCustomKey(key)) return SettingKeyValidationResult.Custom;
        if (IsSystemKey(key)) return SettingKeyValidationResult.System;
        if (IsCodeDefinedKey(key, registry)) return SettingKeyValidationResult.CodeDefined;
        return SettingKeyValidationResult.Unknown;
    }
}

public enum SettingKeyValidationResult { CodeDefined, Custom, System, Unknown }
```

**Commit:** `feat(kernel): add SettingKeyValidator with namespace rules`

---

### Task 1.5: Create ISettingsService Interface

**Files:**
- Create: `src/Shared/Foundry.Shared.Kernel/Settings/ISettingsService.cs`

**Description:**
Interface for the settings service that modules and API controllers depend on. Provides merged reads, updates, and deletes.

```csharp
namespace Foundry.Shared.Kernel.Settings;

public interface ISettingsService
{
    // Read — merged: user > tenant > code default
    Task<IReadOnlyList<ResolvedSetting>> GetUserSettingsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ResolvedSetting>> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct = default);
    Task<ResolvedSettingsConfig> GetConfigAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    // Write
    Task UpdateTenantSettingsAsync(Guid tenantId, IReadOnlyList<SettingUpdate> settings, Guid updatedBy, CancellationToken ct = default);
    Task UpdateUserSettingsAsync(Guid tenantId, Guid userId, IReadOnlyList<SettingUpdate> settings, CancellationToken ct = default);

    // Delete
    Task DeleteTenantSettingsAsync(Guid tenantId, IReadOnlyList<string> keys, Guid deletedBy, CancellationToken ct = default);
    Task DeleteUserSettingsAsync(Guid tenantId, Guid userId, IReadOnlyList<string> keys, CancellationToken ct = default);
}

public sealed record ResolvedSetting(
    string Key,
    string Value,
    string Source,           // "user", "tenant", "default"
    string? DisplayName,     // null for custom/system keys
    string? Description,     // null for custom/system keys
    string? DefaultValue);   // null for custom/system keys

public sealed record ResolvedSettingsConfig(
    IReadOnlyDictionary<string, string> Settings);

public sealed record SettingUpdate(string Key, string Value);
```

**Commit:** `feat(kernel): add ISettingsService interface with resolution chain`

---

### Task 1.6: Create Settings Domain Events

**Files:**
- Create: `src/Shared/Foundry.Shared.Kernel/Settings/Events/TenantSettingChangedEvent.cs`
- Create: `src/Shared/Foundry.Shared.Kernel/Settings/Events/UserSettingChangedEvent.cs`

**Description:**
Domain events for cache invalidation via Wolverine. Published when settings change.

```csharp
namespace Foundry.Shared.Kernel.Settings.Events;

public sealed record TenantSettingChangedEvent(Guid TenantId, string ModuleName);
public sealed record UserSettingChangedEvent(Guid TenantId, Guid UserId, string ModuleName);
```

**Commit:** `feat(kernel): add settings domain events for cache invalidation`

---

## Phase 2: Shared Infrastructure — Entities, Repositories, and Caching

Build the EF Core entities, generic repositories, and Valkey-backed settings service.

### Task 2.1: Create TenantSetting and UserSetting Entities

**Files:**
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Entities/TenantSettingEntity.cs`
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Entities/UserSettingEntity.cs`

**Description:**
EF Core entities for the two settings tables. These are not DDD aggregates — they're simple persistence entities since settings are key-value rows, not rich domain objects.

```csharp
namespace Foundry.Shared.Infrastructure.Settings.Entities;

public sealed class TenantSettingEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class UserSettingEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Commit:** `feat(infrastructure): add TenantSettingEntity and UserSettingEntity`

---

### Task 2.2: Create EF Core Configurations for Settings Entities

**Files:**
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Persistence/TenantSettingConfiguration.cs`
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Persistence/UserSettingConfiguration.cs`

**Description:**
Reusable EF Core configurations that modules apply in their DbContext's `OnModelCreating`. Each module calls `modelBuilder.ApplySettingsConfigurations()` to get both tables in their schema.

```csharp
namespace Foundry.Shared.Infrastructure.Settings.Persistence;

public sealed class TenantSettingConfiguration : IEntityTypeConfiguration<TenantSettingEntity>
{
    public void Configure(EntityTypeBuilder<TenantSettingEntity> builder)
    {
        builder.ToTable("tenant_settings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Value).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
    }
}

// Similar for UserSettingConfiguration with UNIQUE on (TenantId, UserId, Key)
```

Create an extension method for easy registration:

```csharp
public static class SettingsModelBuilderExtensions
{
    public static ModelBuilder ApplySettingsConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantSettingConfiguration());
        modelBuilder.ApplyConfiguration(new UserSettingConfiguration());
        return modelBuilder;
    }
}
```

**Commit:** `feat(infrastructure): add EF Core configurations for settings tables`

---

### Task 2.3: Create Generic Settings Repository

**Files:**
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Persistence/ISettingsRepository.cs`
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Persistence/SettingsRepository.cs`

**Description:**
Generic repository that works with any module's DbContext. Takes the DbContext as a constructor parameter. Handles CRUD for both tenant and user settings.

```csharp
namespace Foundry.Shared.Infrastructure.Settings.Persistence;

public interface ISettingsRepository
{
    // Tenant settings
    Task<List<TenantSettingEntity>> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct = default);
    Task UpsertTenantSettingsAsync(Guid tenantId, IReadOnlyList<TenantSettingEntity> settings, CancellationToken ct = default);
    Task DeleteTenantSettingsAsync(Guid tenantId, IReadOnlyList<string> keys, CancellationToken ct = default);
    Task<int> CountCustomKeysAsync(Guid tenantId, CancellationToken ct = default);

    // User settings
    Task<List<UserSettingEntity>> GetUserSettingsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task UpsertUserSettingsAsync(Guid tenantId, Guid userId, IReadOnlyList<UserSettingEntity> settings, CancellationToken ct = default);
    Task DeleteUserSettingsAsync(Guid tenantId, Guid userId, IReadOnlyList<string> keys, CancellationToken ct = default);
}
```

The `SettingsRepository` implementation takes `DbContext` and operates on `DbSet<TenantSettingEntity>` / `DbSet<UserSettingEntity>`. Upsert uses EF Core's `ExecuteUpdateAsync` or manual find-and-update.

**Commit:** `feat(infrastructure): add generic SettingsRepository for tenant/user settings CRUD`

---

### Task 2.4: Create CachedSettingsService

**Files:**
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Services/SettingsService.cs`
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Services/CachedSettingsService.cs`

**Description:**
`SettingsService` handles the merge logic (user > tenant > code default) and key validation. `CachedSettingsService` wraps it with HybridCache (L1 in-memory + L2 Valkey).

Cache key patterns:
- Tenant settings: `settings:{moduleName}:{tenantId}`
- User settings (merged): `settings:{moduleName}:{tenantId}:{userId}`

Uses `HybridCache.GetOrCreateAsync` with 5-minute TTL. On write, removes the relevant cache entries.

The `SettingsService` implements `ISettingsService` and depends on:
- `ISettingsRepository` — DB access
- `ISettingRegistry` — code-defined keys, defaults, metadata
- `IMessageBus` (Wolverine) — publishes `TenantSettingChangedEvent` / `UserSettingChangedEvent` after writes

The `CachedSettingsService` also implements `ISettingsService`, wraps `SettingsService`, and adds caching.

**Commit:** `feat(infrastructure): add SettingsService with merge logic and CachedSettingsService with Valkey caching`

---

### Task 2.5: Create Cache Invalidation Handler

**Files:**
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/EventHandlers/SettingsCacheInvalidationHandler.cs`

**Description:**
Wolverine handler that listens for `TenantSettingChangedEvent` and `UserSettingChangedEvent` and removes the corresponding cache entries from HybridCache/Valkey.

For tenant changes, invalidate the tenant key. User merged keys for that tenant are also invalidated by using a tag-based approach or pattern-based key removal via `IConnectionMultiplexer`.

```csharp
public static class SettingsCacheInvalidationHandler
{
    public static async Task HandleAsync(
        TenantSettingChangedEvent @event,
        HybridCache cache,
        IConnectionMultiplexer redis)
    {
        // Remove tenant settings cache
        await cache.RemoveAsync($"settings:{@event.ModuleName}:{@event.TenantId}");

        // Remove all user merged caches for this tenant via pattern delete
        IDatabase db = redis.GetDatabase();
        IServer server = redis.GetServers().First();
        await foreach (RedisKey key in server.KeysAsync(pattern: $"settings:{@event.ModuleName}:{@event.TenantId}:*"))
        {
            await db.KeyDeleteAsync(key);
        }
    }

    public static async Task HandleAsync(
        UserSettingChangedEvent @event,
        HybridCache cache)
    {
        await cache.RemoveAsync($"settings:{@event.ModuleName}:{@event.TenantId}:{@event.UserId}");
    }
}
```

**Commit:** `feat(infrastructure): add Wolverine cache invalidation handlers for settings events`

---

### Task 2.6: Create Settings DI Registration Extension

**Files:**
- Create: `src/Shared/Foundry.Shared.Infrastructure/Settings/Extensions/SettingsServiceExtensions.cs`

**Description:**
Extension method that modules call to register all settings infrastructure. Takes the module's `ISettingRegistry` type as a generic parameter.

```csharp
namespace Foundry.Shared.Infrastructure.Settings.Extensions;

public static class SettingsServiceExtensions
{
    public static IServiceCollection AddModuleSettings<TRegistry>(
        this IServiceCollection services)
        where TRegistry : class, ISettingRegistry
    {
        services.AddSingleton<ISettingRegistry, TRegistry>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<SettingsService>();
        services.AddScoped<ISettingsService>(sp =>
            new CachedSettingsService(
                sp.GetRequiredService<SettingsService>(),
                sp.GetRequiredService<HybridCache>(),
                sp.GetRequiredService<IConnectionMultiplexer>(),
                sp.GetRequiredService<ISettingRegistry>()));
        return services;
    }
}
```

**Commit:** `feat(infrastructure): add AddModuleSettings<TRegistry> DI registration extension`

---

## Phase 3: Microsoft.FeatureManagement Integration

Add development-level feature gating for trunk-based development.

### Task 3.1: Add Microsoft.FeatureManagement Package

**Files:**
- Modify: `Directory.Packages.props` — add `Microsoft.FeatureManagement.AspNetCore` package
- Modify: `src/Foundry.Api/Foundry.Api.csproj` — add PackageReference

**Description:**
Add the NuGet package to the central package management and the API host project.

**Commit:** `feat(api): add Microsoft.FeatureManagement.AspNetCore package`

---

### Task 3.2: Configure FeatureManagement in appsettings.json

**Files:**
- Modify: `src/Foundry.Api/appsettings.json` — add `FeatureManagement` section

**Description:**
Add the feature management configuration with module-level and feature-level gates.

```json
{
    "FeatureManagement": {
        "Modules.Billing": true,
        "Modules.Identity": true,
        "Modules.Storage": true,
        "Modules.Communications": true,
        "Modules.Configuration": true,
        "Modules.Inquiries": false,
        "Modules.Showcases": false
    }
}
```

**Commit:** `feat(api): add FeatureManagement configuration for module gating`

---

### Task 3.3: Wire FeatureManagement into Program.cs and FoundryModules.cs

**Files:**
- Modify: `src/Foundry.Api/Program.cs` — add `builder.Services.AddFeatureManagement()`
- Modify: `src/Foundry.Api/FoundryModules.cs` — replace `IConfiguration.GetValue` with `IFeatureManager.IsEnabledAsync` for module toggles

**Description:**
Register FeatureManagement in the DI container and update `FoundryModules.cs` to use `IFeatureManager` instead of raw config values for module registration. Since `IFeatureManager` is async and module registration happens at startup, use `IFeatureManagerSnapshot` or evaluate feature state from `IConfiguration` section that FeatureManagement reads (the existing pattern already works, just standardize the key names to match the FeatureManagement section).

**Commit:** `feat(api): wire Microsoft.FeatureManagement into module registration`

---

## Phase 4: Reference Implementation — Billing Module Settings

Implement the settings pattern in one module as the reference for all others.

### Task 4.1: Create BillingSettingKeys Registry

**Files:**
- Create: `src/Modules/Billing/Foundry.Billing.Application/Settings/BillingSettingKeys.cs`

**Description:**
Define Billing's code-defined settings and implement the registry.

```csharp
namespace Foundry.Billing.Application.Settings;

public sealed class BillingSettingKeys : SettingRegistryBase
{
    public override string ModuleName => "billing";

    public static readonly SettingDefinition<string> DefaultCurrency =
        new("default_currency", "USD", "Default currency for new invoices");

    public static readonly SettingDefinition<string> InvoicePrefix =
        new("invoice_prefix", "INV-", "Prefix for invoice numbers");

    public static readonly SettingDefinition<string> DateFormat =
        new("date_format", "YYYY-MM-DD", "Date display format");

    public static readonly SettingDefinition<int> PaymentRetryAttempts =
        new("payment_retry_attempts", 3, "Number of payment retry attempts before marking failed");
}
```

**Commit:** `feat(billing): add BillingSettingKeys registry with initial settings`

---

### Task 4.2: Add Settings Tables to BillingDbContext

**Files:**
- Modify: `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/BillingDbContext.cs` — add `DbSet<TenantSettingEntity>` and `DbSet<UserSettingEntity>`, call `ApplySettingsConfigurations()` in `OnModelCreating`

**Description:**
Register the settings entities in Billing's DbContext so they live in the `billing` schema.

**Commit:** `feat(billing): add settings tables to BillingDbContext`

---

### Task 4.3: Create EF Core Migration for Billing Settings Tables

**Files:**
- Create: `src/Modules/Billing/Foundry.Billing.Infrastructure/Migrations/YYYYMMDDHHMMSS_AddSettingsTables.cs` (generated)

**Description:**
Run the EF Core migration command to generate the migration:

```bash
dotnet ef migrations add AddSettingsTables \
    --project src/Modules/Billing/Foundry.Billing.Infrastructure \
    --startup-project src/Foundry.Api \
    --context BillingDbContext
```

**Commit:** `feat(billing): add migration for tenant_settings and user_settings tables`

---

### Task 4.4: Register Settings in Billing Module

**Files:**
- Modify: `src/Modules/Billing/Foundry.Billing.Infrastructure/Extensions/BillingInfrastructureExtensions.cs` — call `services.AddModuleSettings<BillingSettingKeys>()`

**Description:**
Add the one-line settings registration to Billing's infrastructure setup.

**Commit:** `feat(billing): register settings infrastructure in Billing module`

---

### Task 4.5: Create Billing Settings API Controller

**Files:**
- Create: `src/Modules/Billing/Foundry.Billing.Api/Controllers/BillingSettingsController.cs`

**Description:**
API controller implementing the full settings surface for Billing:
- `GET /api/billing/config` — merged settings for frontend (read-only, any authenticated user)
- `GET /api/billing/settings/tenant` — all tenant settings (admin)
- `PUT /api/billing/settings/tenant` — batch update tenant settings (admin)
- `DELETE /api/billing/settings/tenant` — batch delete custom/system keys (admin)
- `GET /api/billing/settings/user` — merged user settings
- `PUT /api/billing/settings/user` — batch update user settings
- `DELETE /api/billing/settings/user` — batch delete custom keys (user)

Use `[Authorize]` for all endpoints. Tenant and system key endpoints require admin role. Inject `ISettingsService`.

**Commit:** `feat(billing): add settings API controller with full CRUD endpoints`

---

### Task 4.6: Write Integration Tests for Billing Settings

**Files:**
- Create: `tests/Modules/Billing/Billing.Integration.Tests/Settings/BillingSettingsTests.cs`

**Description:**
Test the full settings lifecycle:
1. GET config returns code defaults when no overrides exist
2. PUT tenant setting, GET config returns tenant value
3. PUT user setting, GET config returns user value (overrides tenant)
4. DELETE user setting, GET config falls back to tenant value
5. DELETE tenant setting, GET config falls back to code default
6. Custom key bypass validation, stored and returned
7. Unknown non-custom key rejected
8. System key requires platform admin
9. Custom key limit (100) enforced
10. Cache invalidation works (set value, read cached, update value, read reflects change)

**Commit:** `test(billing): add integration tests for settings lifecycle`

---

## Phase 5: Roll Out Settings to Remaining Modules

Apply the same pattern to Identity (with global settings), Storage, and Communications.

### Task 5.1: Create IdentitySettingKeys Registry (Global Settings Owner)

**Files:**
- Create: `src/Modules/Identity/Foundry.Identity.Application/Settings/IdentitySettingKeys.cs`

**Description:**
Identity owns global cross-cutting settings: timezone, locale, theme.

```csharp
public sealed class IdentitySettingKeys : SettingRegistryBase
{
    public override string ModuleName => "identity";

    public static readonly SettingDefinition<string> Timezone =
        new("timezone", "UTC", "User timezone for date/time display");

    public static readonly SettingDefinition<string> Locale =
        new("locale", "en-US", "User locale for formatting");

    public static readonly SettingDefinition<string> DateFormat =
        new("date_format", "YYYY-MM-DD", "Global date display format");

    public static readonly SettingDefinition<string> Theme =
        new("theme", "light", "UI theme preference");
}
```

**Commit:** `feat(identity): add IdentitySettingKeys as global settings owner`

---

### Task 5.2: Add Settings to Identity Module Infrastructure

**Files:**
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Persistence/IdentityDbContext.cs`
- Modify: `src/Modules/Identity/Foundry.Identity.Infrastructure/Extensions/IdentityInfrastructureExtensions.cs`
- Create: `src/Modules/Identity/Foundry.Identity.Api/Controllers/IdentitySettingsController.cs`

**Description:**
Same pattern as Billing: add DbSets, call `ApplySettingsConfigurations()`, register with `AddModuleSettings<IdentitySettingKeys>()`, create API controller. Run EF migration.

```bash
dotnet ef migrations add AddSettingsTables \
    --project src/Modules/Identity/Foundry.Identity.Infrastructure \
    --startup-project src/Foundry.Api \
    --context IdentityDbContext
```

**Commit:** `feat(identity): add settings infrastructure and API endpoints`

---

### Task 5.3: Add Settings to Storage Module

**Files:**
- Create: `src/Modules/Storage/Foundry.Storage.Application/Settings/StorageSettingKeys.cs`
- Modify: `src/Modules/Storage/Foundry.Storage.Infrastructure/Persistence/StorageDbContext.cs`
- Modify: `src/Modules/Storage/Foundry.Storage.Infrastructure/Extensions/StorageInfrastructureExtensions.cs`
- Create: `src/Modules/Storage/Foundry.Storage.Api/Controllers/StorageSettingsController.cs`

**Description:**
Storage settings: max upload size, allowed file types, storage quota.

**Commit:** `feat(storage): add settings infrastructure and API endpoints`

---

### Task 5.4: Add Settings to Communications Module

**Files:**
- Create: `src/Modules/Communications/Foundry.Communications.Application/Settings/CommunicationsSettingKeys.cs`
- Modify: `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/CommunicationsDbContext.cs`
- Modify: `src/Modules/Communications/Foundry.Communications.Infrastructure/Extensions/CommunicationsInfrastructureExtensions.cs`
- Create: `src/Modules/Communications/Foundry.Communications.Api/Controllers/CommunicationsSettingsController.cs`

**Description:**
Communications settings: email sender name, notification preferences.

**Commit:** `feat(communications): add settings infrastructure and API endpoints`

---

## Phase 6: Migrate Custom Field Definitions

Move custom fields from Configuration into owning modules.

### Task 6.1: Move CustomFieldDefinition to Billing Domain

**Files:**
- Create: `src/Modules/Billing/Foundry.Billing.Domain/Entities/CustomFieldDefinition.cs` (copy and adapt from Configuration)
- Create: `src/Modules/Billing/Foundry.Billing.Domain/Identity/CustomFieldDefinitionId.cs`
- Create: `src/Modules/Billing/Foundry.Billing.Domain/Events/CustomFieldDefinitionCreatedEvent.cs`
- Create: `src/Modules/Billing/Foundry.Billing.Domain/Events/CustomFieldDefinitionDeactivatedEvent.cs`
- Create: `src/Modules/Billing/Foundry.Billing.Domain/Exceptions/CustomFieldException.cs`
- Create: `src/Modules/Billing/Foundry.Billing.Application/Contracts/ICustomFieldDefinitionRepository.cs`

**Description:**
Copy the `CustomFieldDefinition` aggregate and supporting types from Configuration to Billing. The entity type filter in `CustomFieldRegistry` should scope to Billing entity types (Invoice, Payment). Adapt namespace and any module-specific logic.

**Commit:** `feat(billing): add CustomFieldDefinition aggregate for billing entity types`

---

### Task 6.2: Add Custom Field Infrastructure to Billing

**Files:**
- Create: `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Configurations/CustomFieldDefinitionConfiguration.cs`
- Create: `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/Repositories/CustomFieldDefinitionRepository.cs`
- Modify: `src/Modules/Billing/Foundry.Billing.Infrastructure/Persistence/BillingDbContext.cs` — add DbSet

**Description:**
Add the EF Core configuration, repository, and DbSet for custom fields in Billing. Run migration.

**Commit:** `feat(billing): add custom field definition persistence and repository`

---

### Task 6.3: Move Custom Field Commands and Queries to Billing

**Files:**
- Create: `src/Modules/Billing/Foundry.Billing.Application/CustomFields/Commands/` (copy from Configuration)
- Create: `src/Modules/Billing/Foundry.Billing.Application/CustomFields/Queries/` (copy from Configuration)
- Create: `src/Modules/Billing/Foundry.Billing.Api/Controllers/BillingCustomFieldsController.cs`

**Description:**
Move the CQRS commands/queries for custom field management into Billing. Create the API controller. Adapt namespaces.

**Commit:** `feat(billing): add custom field CQRS commands, queries, and API controller`

---

### Task 6.4: Add Custom Fields to Other Modules (if needed)

**Description:**
Repeat Tasks 6.1-6.3 for any other modules that support custom fields. Currently, only Billing entity types are registered in `CustomFieldRegistry`. If other modules need custom fields, add them following the same pattern.

**Commit:** Per-module commits as needed.

---

## Phase 7: Retire Configuration Module

Remove the centralized Configuration module entirely.

### Task 7.1: Remove Configuration Module References

**Files:**
- Modify: `src/Foundry.Api/FoundryModules.cs` — remove `AddConfigurationModule` / `InitializeConfigurationModuleAsync`
- Modify: `src/Foundry.Api/Foundry.Api.csproj` — remove project reference to `Foundry.Configuration.Api`
- Modify: any `Shared.Contracts` files that reference Configuration events

**Description:**
Remove all references to the Configuration module from the API host and shared contracts.

**Commit:** `refactor(api): remove Configuration module registration from API host`

---

### Task 7.2: Delete Configuration Module Projects

**Files:**
- Delete: `src/Modules/Configuration/` (entire directory — 4 projects)

**Description:**
Remove the Configuration module's Domain, Application, Infrastructure, and Api projects. Remove the corresponding test projects if they exist.

**Commit:** `chore: remove centralized Configuration module (replaced by per-module settings)`

---

### Task 7.3: Clean Up Solution File and Build

**Files:**
- Modify: `Foundry.sln` — remove Configuration project entries
- Modify: `Directory.Packages.props` — remove any Configuration-only package references

**Description:**
Update the solution file, verify the build passes, and run all tests.

```bash
dotnet build
dotnet test
```

**Commit:** `chore: clean up solution file after Configuration module removal`

---

## Phase 8: Verification and Documentation

### Task 8.1: Run Full Test Suite

**Description:**
Run all tests to verify nothing is broken.

```bash
dotnet test --verbosity normal
```

Fix any failures.

---

### Task 8.2: Update Developer Documentation

**Files:**
- Modify: `docs/DEVELOPER_GUIDE.md` — add section on per-module settings pattern
- Modify: `docs/CONFIGURATION_GUIDE.md` — update to reflect new architecture
- Modify: `CLAUDE.md` — update module list (remove Configuration)

**Description:**
Update documentation to reflect the new decentralized settings architecture and the removal of the Configuration module.

**Commit:** `docs: update documentation for decentralized settings architecture`

---

### Task 8.3: Verify API Endpoints

**Description:**
Start the API and verify the settings endpoints work:

```bash
dotnet run --project src/Foundry.Api
```

Test via curl or HTTP client:
- `GET /api/billing/config`
- `GET /api/billing/settings/tenant`
- `PUT /api/billing/settings/tenant`
- `GET /api/billing/settings/user`
- `PUT /api/billing/settings/user`

---
