# Phase 4: Shared Infrastructure

**Scope:** `src/Shared/Foundry.Shared.Infrastructure/` and `tests/Foundry.Shared.Infrastructure.Tests/`
**Status:** Not Started
**Files:** 0 source files (project exists but contains no custom .cs files), 30 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Source Files

The `Foundry.Shared.Infrastructure` project contains no custom source files (only auto-generated `obj/` files). It serves as a dependency aggregation project. All shared infrastructure implementations are in `Foundry.Shared.Infrastructure.Core`, `Foundry.Shared.Infrastructure.BackgroundJobs`, `Foundry.Shared.Infrastructure.Workflows`, and `Foundry.Shared.Infrastructure.Plugins`.

## Test Files

The `Foundry.Shared.Infrastructure.Tests` project covers all shared infrastructure projects (Core, BackgroundJobs, Workflows, Plugins).

### Root

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 1 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/GlobalUsings.cs` | Global using directives for FluentAssertions | Test infrastructure setup | |

### AsyncApi

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 2 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/AsyncApi/AsyncApiDocumentGeneratorTests.cs` | Tests AsyncAPI 3.0 document generation from event flow info | `AsyncApiDocumentGenerator.GenerateDocument` produces valid schema with channels, operations, components | |
| 3 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/AsyncApi/AsyncApiIntegrationTests.cs` | Integration tests discovering real Contracts assembly events and generating full AsyncAPI docs | End-to-end flow: `EventFlowDiscovery` -> `AsyncApiDocumentGenerator` using actual `UserRegisteredEvent` assembly | |
| 4 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/AsyncApi/EventFlowDiscoveryTests.cs` | Tests event flow discovery scanning assemblies for IIntegrationEvent types and handlers | `EventFlowDiscovery.Discover` finds event types, matches handlers, detects saga triggers | |
| 5 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/AsyncApi/JsonSchemaGeneratorTests.cs` | Tests C# type to JSON Schema conversion for AsyncAPI message payloads | `JsonSchemaGenerator.GenerateSchema` and `GetPropertySchema` for all primitive and complex types | |
| 6 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/AsyncApi/MermaidFlowGeneratorTests.cs` | Tests Mermaid flowchart generation from event flow info | `MermaidFlowGenerator.Generate` produces correct flowchart LR syntax with subgraphs | |

### Auditing

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 7 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Auditing/AuditEntryTests.cs` | Tests AuditEntry entity construction and JSON serialization of old/new values | Default properties, JSONB value roundtrip | |
| 8 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Auditing/AuditInterceptorTests.cs` | Tests AuditInterceptor capturing Insert/Update/Delete changes with user/tenant context | Uses Testcontainers PostgreSQL; verifies audit entries created with correct action, values, userId, tenantId | |
| 9 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Auditing/AuditTenantIsolationTests.cs` | Tests audit entries are stamped with correct tenant ID for multi-tenant isolation | Uses Testcontainers PostgreSQL; verifies TenantId populated from ITenantContext | |
| 10 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Auditing/AuditingExtensionsTests.cs` | Tests DI registration and database migration of auditing services | `AddFoundryAuditing` registers AuditDbContext; `InitializeAuditingAsync` applies migrations | |

### BackgroundJobs

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 11 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/BackgroundJobs/BackgroundJobsExtensionsTests.cs` | Tests DI registration of background job services | `AddFoundryBackgroundJobs` registers `IJobScheduler` as `HangfireJobScheduler` | |
| 12 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/BackgroundJobs/HangfireJobSchedulerTests.cs` | Tests that HangfireJobScheduler implements IJobScheduler interface | Interface implementation verification | |

### Messaging

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 13 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Messaging/TenantRestoringMiddlewareTests.cs` | Tests tenant context restoration from Wolverine message headers | `Before` with valid/missing/invalid X-Tenant-Id header values | |
| 14 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Messaging/TenantStampingMiddlewareTests.cs` | Tests tenant ID stamping into outgoing Wolverine message headers | `Before` stamps header when resolved, skips when not resolved | |

### Middleware

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 15 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Middleware/WolverineModuleTaggingMiddlewareTests.cs` | Tests module name extraction from message namespace and activity tagging | `Before` sets `foundry.module` tag from namespace regex; uses real Billing/Identity message types | |

### Persistence

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 16 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Persistence/DictionaryValueComparerTests.cs` | Tests dictionary equality comparison using JSON serialization | Null handling, same/different dictionaries, hash code consistency, snapshot independence | |

### Plugins

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 17 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/MismatchedManifestPlugin.cs` | Test fixture: IFoundryPlugin with mismatched manifest ID for error path testing | Used by PluginLoaderTests to verify manifest ID mismatch detection | |
| 18 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginAssemblyLoadContextTests.cs` | Tests isolated assembly loading and unloading for plugin sandboxing | `PluginAssemblyLoadContext` loading from temp directory, fallback to default context | |
| 19 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginConfigurationTests.cs` | Tests PluginOptions default values and configuration binding | `SectionName`, `PluginsDirectory`, `AutoDiscover`, `AutoEnable` defaults | |
| 20 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginLifecycleManagerTests.cs` | Tests plugin discovery, loading, enabling, and disabling lifecycle transitions | `DiscoverPluginsAsync`, `LoadPluginAsync`, `EnablePluginAsync` state transitions and permission checks | |
| 21 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginLoadExceptionTests.cs` | Tests PluginLoadException construction with plugin ID and messages | Constructor variants, `PluginId` property, inner exception propagation | |
| 22 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginLoaderTests.cs` | Tests assembly loading, IFoundryPlugin discovery, hash verification, and error paths | Uses AssemblyBuilder to create test assemblies; verifies load, manifest mismatch, missing implementation errors | |
| 23 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginManifestLoaderTests.cs` | Tests manifest JSON deserialization from filesystem directories | `LoadFromDirectory` with valid/invalid/missing manifests; validation of required fields | |
| 24 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginPermissionValidatorTests.cs` | Tests permission grant checking against configured and manifest-declared permissions | `HasPermission` with matching/missing configs; `GetGrantedPermissions` intersection logic | |
| 25 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginRegistryTests.cs` | Tests concurrent plugin registration, state updates, and removal | `Register`, `GetEntry`, `GetAll`, `UpdateState`, `SetInstance`, `Remove` operations | |
| 26 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Plugins/PluginServiceExtensionsTests.cs` | Tests DI registration and app initialization of the plugin system | `AddFoundryPlugins` registers PluginRegistry, PluginLoader, PluginLifecycleManager; `InitializeFoundryPluginsAsync` | |

### Resilience

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 27 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Resilience/ResilienceExtensionsLoggingTests.cs` | Tests resilience handler configuration and structured logging callbacks | `AddFoundryResilienceHandler` profile configuration; retry and circuit breaker logging | |

### Services

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 28 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Services/HtmlSanitizationServiceTests.cs` | Tests HTML sanitization with allowed/blocked tags, attributes, and XSS payloads | Safe tags preserved, script tags stripped, XSS payloads neutralized, null/empty handling | |

### Workflows

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 29 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Workflows/ElsaExtensionsTests.cs` | Tests Elsa workflow DI registration with connection string and signing key | `AddFoundryWorkflows` registers Elsa services; missing connection string throws | |
| 30 | [ ] | `tests/Foundry.Shared.Infrastructure.Tests/Workflows/WorkflowActivityBaseTests.cs` | Tests workflow activity base class execution with module-scoped logging | `ExecuteActivityAsync` called with correct context; module name and activity type logged | |
