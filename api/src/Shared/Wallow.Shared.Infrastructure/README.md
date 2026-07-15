# Wallow.Shared.Infrastructure

Shared infrastructure utilities, settings framework, and module coordination.

## Purpose

Provides the settings framework for tenant- and user-scoped configuration, and serves as the coordination point that aggregates the other shared infrastructure packages (Core, BackgroundJobs, Workflows, Plugins).

## Key Components

### Settings Framework (`Settings/`)

- `TenantSettingEntity` / `UserSettingEntity` - EF Core entities for persisted settings
- `ISettingRepository` / `TenantSettingRepository` / `UserSettingRepository` - Repository abstractions and implementations
- `CachedSettingsService` - Settings access with caching
- `SettingsCacheInvalidationHandlers` - Wolverine handlers for cache invalidation on settings changes
- `SettingsModelBuilderExtensions` - EF Core model builder configuration helpers
- `SettingsServiceExtensions` - DI registration extensions

## Dependencies

**Internal:**
- Wallow.Shared.Kernel
- Wallow.Shared.Contracts
- Wallow.Shared.Infrastructure.Core
- Wallow.Shared.Infrastructure.BackgroundJobs
- Wallow.Shared.Infrastructure.Workflows
- Wallow.Shared.Infrastructure.Plugins

**External Packages:**
- Microsoft.EntityFrameworkCore / Npgsql.EntityFrameworkCore.PostgreSQL
- WolverineFx
- Hangfire.Core
- Serilog.AspNetCore
- HtmlSanitizer
- Elsa (workflow engine)
- Audit.EntityFramework.Core
