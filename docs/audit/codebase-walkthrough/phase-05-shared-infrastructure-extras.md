# Phase 5: Shared Infrastructure Extras

**Scope:** `src/Shared/Wallow.Shared.Infrastructure.BackgroundJobs/`, `src/Shared/Wallow.Shared.Infrastructure.Workflows/`, `src/Shared/Wallow.Shared.Infrastructure.Plugins/`
**Status:** Not Started
**Files:** 18 source files, 0 test files (tests in Wallow.Shared.Infrastructure.Tests, cataloged in Phase 4)

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

## Source Files

### BackgroundJobs (`Wallow.Shared.Infrastructure.BackgroundJobs`)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.BackgroundJobs/BackgroundJobsExtensions.cs` | DI registration for background job services, wiring IJobScheduler to HangfireJobScheduler | `AddWallowBackgroundJobs` registers `IJobScheduler` as singleton `HangfireJobScheduler` | Kernel.BackgroundJobs, Microsoft.Extensions.DependencyInjection | |
| 2 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.BackgroundJobs/HangfireJobScheduler.cs` | Hangfire-backed implementation of IJobScheduler for fire-and-forget and recurring jobs | `Enqueue` delegates to `BackgroundJob.Enqueue`; `AddRecurring`/`RemoveRecurring` delegate to `RecurringJob` | Kernel.BackgroundJobs.IJobScheduler, Hangfire | |

### Workflows - AsyncApi (`Wallow.Shared.Infrastructure.Workflows/AsyncApi`)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 3 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Workflows/AsyncApi/EventFlowDiscovery.cs` | Discovers integration event types and their handlers/sagas across all loaded assemblies | Scans for `IIntegrationEvent` implementations, matches `Handle`/`HandleAsync` methods, detects saga types; produces `EventFlowInfo` list | Contracts.IIntegrationEvent | |
| 4 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Workflows/AsyncApi/AsyncApiDocumentGenerator.cs` | Generates AsyncAPI 3.0 JSON documents from discovered event flows | Builds `info`, `channels`, `operations`, `components/schemas` sections; caches result; RabbitMQ bindings | System.Text.Json.Nodes, System.Reflection | |
| 5 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Workflows/AsyncApi/JsonSchemaGenerator.cs` | Converts C# types to JSON Schema objects for AsyncAPI message payloads | `GenerateSchema` handles all primitive types, Guid, DateTime, enums, collections, nested objects; camelCase property names | System.Text.Json.Nodes, System.Reflection | |
| 6 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Workflows/AsyncApi/MermaidFlowGenerator.cs` | Generates Mermaid flowchart markup from event flows showing producer-exchange-consumer topology | Builds `flowchart LR` with subgraphs per module, RabbitMQ exchange node, saga markers | System.Text (StringBuilder) | |

### Workflows - Elsa (`Wallow.Shared.Infrastructure.Workflows/Workflows`)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 7 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Workflows/Workflows/ElsaExtensions.cs` | DI registration for Elsa workflow engine with EF Core PostgreSQL persistence and auto-discovery | `AddWallowWorkflows` configures Elsa management, runtime, identity, scheduling, HTTP, email; auto-discovers `WorkflowActivityBase` subclasses | Elsa.*, Microsoft.EntityFrameworkCore | |
| 8 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Workflows/Workflows/WorkflowActivityBase.cs` | Abstract base for module-specific workflow activities with scoped logging and module context | `ExecuteAsync` wraps `ExecuteActivityAsync` with structured logging (Module, ActivityType, WorkflowInstanceId) | Elsa.Workflows, Microsoft.Extensions.Logging | |

### Plugins (`Wallow.Shared.Infrastructure.Plugins`)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 9 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginOptions.cs` | Configuration POCO for plugin system settings (directory, auto-discover, permissions, hashes) | `PluginsDirectory`, `AutoDiscover`, `AutoEnable`, `Permissions` dict, `AllowedPluginHashes` dict | None | |
| 10 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginAssemblyLoadContext.cs` | Collectible AssemblyLoadContext for plugin isolation, falling back to default context for shared assemblies | `Load` checks plugin directory first, returns null for fallback to default context; `isCollectible: true` for unloading | System.Runtime.Loader | |
| 11 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginManifestLoader.cs` | Static loader that reads and validates `wallow-plugin.json` manifests from plugin directories | `LoadFromDirectory` scans subdirectories, deserializes JSON, `ValidateManifest` checks required fields | Kernel.Plugins.PluginManifest, System.Text.Json | |
| 12 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginRegistryEntry.cs` | Mutable entry tracking a plugin's manifest, lifecycle state, runtime instance, and load context | `Manifest`, `State`, `Instance` (IWallowPlugin?), `LoadContext` (PluginAssemblyLoadContext?) | Kernel.Plugins | |
| 13 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginRegistry.cs` | Thread-safe concurrent dictionary registry for all known plugin entries | `Register`, `GetEntry`, `GetAll`, `UpdateState`, `SetInstance`, `Remove` on `ConcurrentDictionary` | Kernel.Plugins | |
| 14 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginLoader.cs` | Loads plugin assemblies into isolated contexts, verifies hashes, and creates IWallowPlugin instances | `LoadPlugin` verifies hash, creates `PluginAssemblyLoadContext`, loads assembly, finds single `IWallowPlugin` type, validates manifest ID match | Kernel.Plugins, System.Security.Cryptography | |
| 15 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginLoadException.cs` | Custom exception for plugin loading failures with plugin ID context | `PluginId` property; multiple constructor overloads | None | |
| 16 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginPermissionValidator.cs` | Validates plugin permissions by intersecting manifest requirements with configured grants | `HasPermission` checks both `Permissions` config and manifest `RequiredPermissions`; `GetGrantedPermissions` returns intersection | Kernel.Plugins.IPluginPermissionValidator, PluginOptions | |
| 17 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginLifecycleManager.cs` | Orchestrates full plugin lifecycle: discover, load, enable, disable, uninstall with state validation | `DiscoverPluginsAsync`, `LoadPluginAsync`, `EnablePluginAsync` with permission checks and state transitions; structured logging | Kernel.Plugins, PluginRegistry, PluginLoader, PluginPermissionValidator | |
| 18 | [ ] | `src/Shared/Wallow.Shared.Infrastructure.Plugins/PluginServiceExtensions.cs` | DI registration and WebApplication initialization extensions for the plugin subsystem | `AddWallowPlugins` registers all plugin services; `InitializeWallowPluginsAsync` discovers and optionally auto-enables plugins | Kernel.Plugins, Microsoft.AspNetCore.Builder, Microsoft.Extensions.DependencyInjection | |

## Test Files

All tests for these projects are in `tests/Wallow.Shared.Infrastructure.Tests/` and are cataloged in Phase 4.
