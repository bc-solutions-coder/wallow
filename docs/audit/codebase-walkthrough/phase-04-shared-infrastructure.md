# Phase 4: Shared Infrastructure

**Scope:** `src/Shared/Wallow.Shared.Infrastructure/` and `tests/Wallow.Shared.Infrastructure.Tests/`
**Status:** Not Started
**Files:** 11 source files, 30 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Source Files

### Settings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/CachedSettingsService.cs` | Redis-cached ISettingsService implementation with 3-layer merge: default -> tenant -> user | `GetTenantSettingsAsync`, `GetUserSettingsAsync`, `GetConfigAsync`; 5-minute cache TTL; `UpdateTenantSettingsAsync`/`UpdateUserSettingsAsync`/`DeleteTenantSettingsAsync`/`DeleteUserSettingsAsync` | Kernel.Settings, TenantAwareDbContext, IDistributedCache | |
| 2 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/ISettingRepository.cs` | Generic repository interfaces for tenant and user settings, keyed by DbContext type | `ITenantSettingRepository<TDbContext>` with `GetAllAsync`, `UpsertAsync`, `DeleteAsync`; `IUserSettingRepository<TDbContext>` same shape | Kernel.Identity | |
| 3 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/SettingsCacheInvalidationHandlers.cs` | Wolverine message handlers that invalidate settings cache when settings change events are published | Handlers for settings change integration events; removes tenant/user cache keys | Kernel.Settings.SettingEvents, IDistributedCache | |
| 4 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/SettingsModelBuilderExtensions.cs` | EF Core model builder extension to register tenant and user setting entity configurations | `ApplySettingsConfiguration` applies `TenantSettingEntityConfiguration` and `UserSettingEntityConfiguration` | Microsoft.EntityFrameworkCore | |
| 5 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/SettingsServiceExtensions.cs` | DI registration extension wiring `CachedSettingsService` to `ISettingsService` with module-keyed repositories | `AddSettings<TDbContext, TRegistry>` registers keyed `ISettingRegistry`, scoped repositories, keyed `ISettingsService` | Kernel.Settings, Microsoft.Extensions.DependencyInjection | |
| 6 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/TenantSettingEntity.cs` | EF Core entity for tenant-scoped settings stored per module and key | Properties: TenantId, ModuleName, SettingKey, Value; constructor enforces required fields | Kernel.Identity | |
| 7 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/TenantSettingEntityConfiguration.cs` | EF Core fluent configuration for `TenantSettingEntity` with composite key and index | Primary key `(TenantId, ModuleName, SettingKey)`; table in `settings` schema | Microsoft.EntityFrameworkCore | |
| 8 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/TenantSettingRepository.cs` | EF Core repository implementing `ITenantSettingRepository<TDbContext>` | `GetAllAsync` filters by TenantId + ModuleName; `UpsertAsync` add-or-update; `DeleteAsync` removes by key | TenantAwareDbContext, Kernel.Identity | |
| 9 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/UserSettingEntity.cs` | EF Core entity for user-scoped settings stored per tenant, user, module, and key | Properties: TenantId, UserId, ModuleName, SettingKey, Value | Kernel.Identity | |
| 10 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/UserSettingEntityConfiguration.cs` | EF Core fluent configuration for `UserSettingEntity` with composite key | Primary key `(TenantId, UserId, ModuleName, SettingKey)` | Microsoft.EntityFrameworkCore | |
| 11 | [ ] | `src/Shared/Wallow.Shared.Infrastructure/Settings/UserSettingRepository.cs` | EF Core repository implementing `IUserSettingRepository<TDbContext>` | `GetAllAsync` filters by TenantId + UserId + ModuleName; `UpsertAsync`; `DeleteAsync` | TenantAwareDbContext, Kernel.Identity | |

## Test Files

The `Wallow.Shared.Infrastructure.Tests` project covers all shared infrastructure projects (Core, BackgroundJobs, Workflows, Plugins).

### Root

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 1 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/GlobalUsings.cs` | Global using directives for FluentAssertions | Test infrastructure setup | |

### AsyncApi

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 2 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/AsyncApi/AsyncApiDocumentGeneratorTests.cs` | Tests AsyncAPI 3.0 document generation from event flow info | `AsyncApiDocumentGenerator.GenerateDocument` produces valid schema with channels, operations, components | |
| 3 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/AsyncApi/AsyncApiIntegrationTests.cs` | Integration tests discovering real Contracts assembly events and generating full AsyncAPI docs | End-to-end flow: `EventFlowDiscovery` -> `AsyncApiDocumentGenerator` using actual `UserRegisteredEvent` assembly | |
| 4 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/AsyncApi/EventFlowDiscoveryTests.cs` | Tests event flow discovery scanning assemblies for IIntegrationEvent types and handlers | `EventFlowDiscovery.Discover` finds event types, matches handlers, detects saga triggers | |
| 5 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/AsyncApi/JsonSchemaGeneratorTests.cs` | Tests C# type to JSON Schema conversion for AsyncAPI message payloads | `JsonSchemaGenerator.GenerateSchema` and `GetPropertySchema` for all primitive and complex types | |
| 6 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/AsyncApi/MermaidFlowGeneratorTests.cs` | Tests Mermaid flowchart generation from event flow info | `MermaidFlowGenerator.Generate` produces correct flowchart LR syntax with subgraphs | |

### Auditing

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 7 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Auditing/AuditEntryTests.cs` | Tests AuditEntry entity construction and JSON serialization of old/new values | Default properties, JSONB value roundtrip | |
| 8 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Auditing/AuditInterceptorTests.cs` | Tests AuditInterceptor capturing Insert/Update/Delete changes with user/tenant context | Uses Testcontainers PostgreSQL; verifies audit entries created with correct action, values, userId, tenantId | |
| 9 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Auditing/AuditTenantIsolationTests.cs` | Tests audit entries are stamped with correct tenant ID for multi-tenant isolation | Uses Testcontainers PostgreSQL; verifies TenantId populated from ITenantContext | |
| 10 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Auditing/AuditingExtensionsTests.cs` | Tests DI registration and database migration of auditing services | `AddWallowAuditing` registers AuditDbContext; `InitializeAuditingAsync` applies migrations | |

### BackgroundJobs

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 11 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/BackgroundJobs/BackgroundJobsExtensionsTests.cs` | Tests DI registration of background job services | `AddWallowBackgroundJobs` registers `IJobScheduler` as `HangfireJobScheduler` | |
| 12 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/BackgroundJobs/HangfireJobSchedulerTests.cs` | Tests that HangfireJobScheduler implements IJobScheduler interface | Interface implementation verification | |

### Messaging

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 13 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Messaging/TenantRestoringMiddlewareTests.cs` | Tests tenant context restoration from Wolverine message headers | `Before` with valid/missing/invalid X-Tenant-Id header values | |
| 14 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Messaging/TenantStampingMiddlewareTests.cs` | Tests tenant ID stamping into outgoing Wolverine message headers | `Before` stamps header when resolved, skips when not resolved | |

### Middleware

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 15 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Middleware/WolverineModuleTaggingMiddlewareTests.cs` | Tests module name extraction from message namespace and activity tagging | `Before` sets `wallow.module` tag from namespace regex; uses real Billing/Identity message types | |

### Persistence

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 16 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Persistence/DictionaryValueComparerTests.cs` | Tests dictionary equality comparison using JSON serialization | Null handling, same/different dictionaries, hash code consistency, snapshot independence | |

### Plugins

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 17 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/MismatchedManifestPlugin.cs` | Test fixture: IWallowPlugin with mismatched manifest ID for error path testing | Used by PluginLoaderTests to verify manifest ID mismatch detection | |
| 18 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginAssemblyLoadContextTests.cs` | Tests isolated assembly loading and unloading for plugin sandboxing | `PluginAssemblyLoadContext` loading from temp directory, fallback to default context | |
| 19 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginConfigurationTests.cs` | Tests PluginOptions default values and configuration binding | `SectionName`, `PluginsDirectory`, `AutoDiscover`, `AutoEnable` defaults | |
| 20 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginLifecycleManagerTests.cs` | Tests plugin discovery, loading, enabling, and disabling lifecycle transitions | `DiscoverPluginsAsync`, `LoadPluginAsync`, `EnablePluginAsync` state transitions and permission checks | |
| 21 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginLoadExceptionTests.cs` | Tests PluginLoadException construction with plugin ID and messages | Constructor variants, `PluginId` property, inner exception propagation | |
| 22 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginLoaderTests.cs` | Tests assembly loading, IWallowPlugin discovery, hash verification, and error paths | Uses AssemblyBuilder to create test assemblies; verifies load, manifest mismatch, missing implementation errors | |
| 23 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginManifestLoaderTests.cs` | Tests manifest JSON deserialization from filesystem directories | `LoadFromDirectory` with valid/invalid/missing manifests; validation of required fields | |
| 24 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginPermissionValidatorTests.cs` | Tests permission grant checking against configured and manifest-declared permissions | `HasPermission` with matching/missing configs; `GetGrantedPermissions` intersection logic | |
| 25 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginRegistryTests.cs` | Tests concurrent plugin registration, state updates, and removal | `Register`, `GetEntry`, `GetAll`, `UpdateState`, `SetInstance`, `Remove` operations | |
| 26 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Plugins/PluginServiceExtensionsTests.cs` | Tests DI registration and app initialization of the plugin system | `AddWallowPlugins` registers PluginRegistry, PluginLoader, PluginLifecycleManager; `InitializeWallowPluginsAsync` | |

### Resilience

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 27 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Resilience/ResilienceExtensionsLoggingTests.cs` | Tests resilience handler configuration and structured logging callbacks | `AddWallowResilienceHandler` profile configuration; retry and circuit breaker logging | |

### Services

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 28 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Services/HtmlSanitizationServiceTests.cs` | Tests HTML sanitization with allowed/blocked tags, attributes, and XSS payloads | Safe tags preserved, script tags stripped, XSS payloads neutralized, null/empty handling | |

### Workflows

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 29 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Workflows/ElsaExtensionsTests.cs` | Tests Elsa workflow DI registration with connection string and signing key | `AddWallowWorkflows` registers Elsa services; missing connection string throws | |
| 30 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Workflows/WorkflowActivityBaseTests.cs` | Tests workflow activity base class execution with module-scoped logging | `ExecuteActivityAsync` called with correct context; module name and activity type logged | |
