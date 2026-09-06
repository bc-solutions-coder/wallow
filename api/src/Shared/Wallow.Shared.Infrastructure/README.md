# Wallow.Shared.Infrastructure

Shared infrastructure utilities, settings framework, and module coordination.

## Purpose

Provides the settings framework for tenant- and user-scoped configuration, and serves as the coordination point that aggregates the other shared infrastructure packages (Core, BackgroundJobs, Plugins).

## Key Components

### Settings Framework (`Settings/`)

- `TenantSettingEntity` / `UserSettingEntity` - EF Core entities for persisted settings
- `ISettingRepository` / `TenantSettingRepository` / `UserSettingRepository` - Repository abstractions and implementations
- `CachedSettingsService` - Settings access with caching
- `SettingsCacheInvalidationHandlers` - Wolverine handlers for cache invalidation on settings changes
- `SettingsModelBuilderExtensions` - EF Core model builder configuration helpers
- `SettingsServiceExtensions` - DI registration extensions

### Fixed-window counting (`RateLimiting/`)

`AddFixedWindowCounter` registers `IFixedWindowCounter` with a Redis implementation.
`IncrementAsync` starts the expiry on the first attempt and leaves it unchanged on later
attempts. `GetRetryAfterAsync` returns the positive remaining TTL or the caller's window.
Callers supply a positive window and retain their own thresholds and storage-failure policy.

## Dependencies

**Internal:**
- Wallow.Shared.Kernel
- Wallow.Shared.Contracts
- Wallow.Shared.Infrastructure.Core
- Wallow.Shared.Infrastructure.BackgroundJobs
- Wallow.Shared.Infrastructure.Plugins

**External Packages:**
- Microsoft.EntityFrameworkCore / Npgsql.EntityFrameworkCore.PostgreSQL
- WolverineFx
- Hangfire.Core
- Serilog.AspNetCore
- HtmlSanitizer
- Audit.EntityFramework.Core
