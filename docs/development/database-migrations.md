# Database Migrations in Wallow

This document explains how database table creation and migrations work in Wallow's modular monolith architecture.

## Overview

Wallow uses **EF Core Migrations** for all modules. Each module owns its own PostgreSQL **schema** and has isolated storage.

### Key Principles

1. **One schema per module** - Each module uses a separate PostgreSQL schema (e.g., `inquiries`, `identity`, `notifications`)
2. **Isolated migration history** - Each schema has its own `__EFMigrationsHistory` table
3. **Migrations run out-of-process** - A dedicated `Wallow.MigrationService` worker applies migrations before the API starts. The API does **not** migrate at startup (the only exception is the `Testing` environment; see [Where Migrations Run](#3-where-migrations-run))
4. **Multi-tenancy support** - DbContexts derive from `TenantAwareDbContext<TContext>`, which applies tenant query filters at runtime; design-time factories construct the context without a tenant

## Architecture

```
Module Infrastructure Layer
├── Modules/
│   └── {Module}Module.cs             # IWallowModule: Schema const, DbContextTypes, SchemaName
├── Persistence/
│   ├── {Module}DbContext.cs          # EF Core DbContext (TenantAwareDbContext<T>)
│   ├── {Module}DbContextFactory.cs   # Design-time factory for the dotnet ef CLI
│   └── Configurations/
│       └── {Entity}Configuration.cs  # Entity type configurations
└── Migrations/
    ├── {timestamp}_{Name}.cs              # Migration code
    ├── {timestamp}_{Name}.Designer.cs     # Migration metadata
    └── {Module}DbContextModelSnapshot.cs  # Current model state
```

## How Migrations Work

### 1. DbContext Setup

Each module's DbContext derives from `TenantAwareDbContext<TContext>` (in
`Wallow.Shared.Infrastructure.Core.Persistence`) and sets its schema in `OnModelCreating`.
The base class supplies `ApplyTenantQueryFilters`, which adds a `TenantId` filter to every
entity implementing `ITenantScoped` — modules do not hand-write query filters:

```csharp
public sealed class InquiriesDbContext : TenantAwareDbContext<InquiriesDbContext>
{
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<InquiryComment> InquiryComments => Set<InquiryComment>();

    public InquiriesDbContext(DbContextOptions<InquiriesDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(InquiriesModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InquiriesDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
```

### 2. DI Registration

In `{Module}InfrastructureExtensions.cs`, each module registers a **pooled DbContext factory**
with a **schema-specific migration history table**:

```csharp
services.AddPooledDbContextFactory<InquiriesDbContext>((sp, options) =>
{
    NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
    {
        MaxPoolSize = maxPoolSize,
        MinPoolSize = minPoolSize
    };
    options.UseNpgsql(builder.ConnectionString, npgsql =>
    {
        // CRITICAL: Each module has its own migration history table in its schema
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", InquiriesModule.Schema);
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
        npgsql.CommandTimeout(30);
    });
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});

services.AddTenantAwareScopedContext<InquiriesDbContext>();
```

`InquiriesModule.Schema` is an `internal const string` on the module's `IWallowModule`
implementation, and it is the **one** place the schema name is written: the DbContext's
`HasDefaultSchema`, this `MigrationsHistoryTable` call, the design-time factory and the module's own
`SchemaName` property all resolve to it. `Wallow.MigrationService` does not repeat the string either
— it reads `IWallowModule.SchemaName` off the registry (see §4), so there is nothing to keep in sync
by hand.

### 3. Where Migrations Run

Modules have no startup hook — the per-module `Initialize{Module}ModuleAsync` methods were all no-ops
and have been deleted. Migration is the job of `Wallow.MigrationService`.

`InitializeWallowModulesAsync` in `api/src/Wallow.Api/WallowModules.cs` has exactly one
in-process migration path, guarded by the environment:

```csharp
// In Testing environment, run EF Core migrations inline since the separate
// MigrationService (used in production/Aspire) is not available. The test factory
// spins up a fresh Postgres container with no schema.
if (app.Environment.IsEnvironment("Testing"))
{
    await RunTestMigrationsAsync(app.Services, enabledModules);
}
```

`enabledModules` is the exact set `AddWallowModules` registered. `RunTestMigrationsAsync` migrates
the core modules' contexts sequentially (`IdentityDbContext` — Identity's schema must exist before
seeding), then the two host-owned auditing contexts (`AuditDbContext`, `AuthAuditDbContext`), then
every feature module's `DbContextTypes` in parallel. A disabled module simply is not in
`enabledModules`, so nothing has to probe DI to skip it. Outside the `Testing` environment nothing in
the API touches `Database.MigrateAsync()`.

### 4. The Migration Service

`api/src/Wallow.MigrationService/` is a worker project (`Microsoft.NET.Sdk.Worker`) that:

1. Reads the `DefaultConnection` connection string and throws if it is missing.
2. Registers the two host-owned auditing contexts (`AuditDbContext`, `AuthAuditDbContext`) by hand —
   they belong to no module — and then calls `ModuleMigrations.AddModuleDbContexts`, which walks
   `WallowModuleRegistry.All` and registers every module's `DbContextTypes` pinned to that module's
   own `SchemaName`. There is **no hand-maintained list of contexts or schema strings** in this
   project.
3. Groups them into `CoreMigrationRunners` (`ModuleMigrations.CreateRunners(isCore: true, …)` — that
   is Identity — plus the two auditing contexts) and `FeatureMigrationRunners`
   (`CreateRunners(isCore: false, …)` — the other six modules).
4. Runs `MigrationWorker`, which migrates core contexts **sequentially**, then all feature
   contexts **in parallel** via `Task.WhenAll`, and finally calls
   `lifetime.StopApplication()` so the process exits.

Each runner is a `DbContextMigrationRunner<TContext>` that resolves the context from a fresh
scope and calls `Database.MigrateAsync(cancellationToken)`.

This host reads the registry **unfiltered** — it deliberately ignores `FeatureManagement:Modules.*`
and migrates every module the platform ships, so a disabled module can be switched back on without a
migration step.

Under Aspire (`pnpm backend` / `dotnet run --project api/src/Wallow.AppHost`) the ordering is
wired explicitly in `api/src/Wallow.AppHost/Program.cs`:

```csharp
IResourceBuilder<ProjectResource> migrations = builder.AddProject<Projects.Wallow_MigrationService>("wallow-migrations")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WaitFor(postgres);

IResourceBuilder<ProjectResource> seeder = builder.AddProject<Projects.Wallow_SeederService>("wallow-seeder")
    .WithReference(postgres, connectionName: "DefaultConnection")
    .WaitForCompletion(migrations);
```

The API in turn uses `.WaitForCompletion(seeder)`, so the chain is
Postgres → migrations → seeder → API.

### 5. Design-Time Factory

For `dotnet ef` CLI commands to work, each module needs an `IDesignTimeDbContextFactory<T>`.
The real implementations take no tenant context — `TenantAwareDbContext` defaults its tenant
to `default` and query filters are irrelevant at design time:

```csharp
public sealed class InquiriesDbContextFactory : IDesignTimeDbContextFactory<InquiriesDbContext>
{
    public InquiriesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<InquiriesDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql(
            $"Host=localhost;Database=wallow;Username=wallow;Password={password}",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inquiries"));

        return new InquiriesDbContext(optionsBuilder.Options);
    }
}
```

Override the local password with the `WALLOW_DB_PASSWORD` environment variable when your dev
Postgres does not use the default credentials.

## Creating Migrations

### Command

```bash
dotnet ef migrations add {MigrationName} \
    --project api/src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context {Module}DbContext
```

### Example

```bash
# Add initial migration for Inquiries module
dotnet ef migrations add InitialCreate \
    --project api/src/Modules/Inquiries/Wallow.Inquiries.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context InquiriesDbContext

# Add a new migration for schema changes
dotnet ef migrations add AddSubmissionStatusField \
    --project api/src/Modules/Inquiries/Wallow.Inquiries.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context InquiriesDbContext
```

## Module Status Reference

### EF Core Modules

| Module | Schema | Has Migrations | Has Factory |
|--------|--------|----------------|-------------|
| Identity | `identity` | Yes | Yes |
| Storage | `storage` | Yes | Yes |
| Notifications | `notifications` | Yes | Yes |
| Announcements | `announcements` | Yes | Yes |
| Inquiries | `inquiries` | Yes | Yes |
| ApiKeys | `apikeys` | Yes | Yes |
| Branding | `branding` | Yes | Yes |

### Shared Infrastructure

| Context | Schema | Has Migrations | Notes |
|---------|--------|----------------|-------|
| AuditDbContext | `audit` | Yes | Audit interceptor in `Wallow.Shared.Infrastructure.Core` |
| AuthAuditDbContext | `auth_audit` | Yes | Authentication audit trail |

## Troubleshooting

### "Table already exists" error

The schema was created by `EnsureCreatedAsync()` but now you're trying to run migrations:
- Drop the schema (dev) or fake the initial migration (prod)

### "No migrations have been applied"

Missing migrations folder or migration history table:
- Run `dotnet ef migrations add InitialCreate` to generate migrations
- Ensure the module's DI registration passes its `{Module}Module.Schema` constant to
  `MigrationsHistoryTable()`, and that `SchemaName` on its `IWallowModule` returns the same constant
  — the migration host derives its own history table from `SchemaName`

### "Could not load type for DbContext"

Missing `IDesignTimeDbContextFactory`:
- Create a factory class implementing `IDesignTimeDbContextFactory<TContext>` next to the
  DbContext, following `InquiriesDbContextFactory`

### A new DbContext never gets migrated

Both hosts derive the contexts they migrate from the module registry, so there is nothing to register
in `Wallow.MigrationService/Program.cs`. Check instead that:

- the context is listed in the owning module's `IWallowModule.DbContextTypes`, and
- the module itself is an entry in `WallowModuleRegistry.All`
  (`api/src/Wallow.Modules.Registry/WallowModuleRegistry.cs`).

`Wallow.Modules.Registry` already references every module's Infrastructure project, so no
`ProjectReference` needs adding to `Wallow.MigrationService.csproj` either.

## Production Migrations

In production and staging, migrations are **not** applied by the application. The
`wallow-migrations` container runs `Wallow.MigrationService` to completion before the app
services start; app services depend on it with `condition: service_completed_successfully`.

### How the Image Is Built

There is **no Dockerfile** for the migration service. The image comes from the .NET SDK's
built-in container support — `Wallow.MigrationService.csproj` declares:

```xml
<ContainerRepository>wallow-migrations</ContainerRepository>
<ContainerBaseImage>mcr.microsoft.com/dotnet/aspnet:10.0</ContainerBaseImage>
```

and `.github/workflows/deploy.yml` publishes it directly:

```bash
dotnet publish api/src/Wallow.MigrationService/Wallow.MigrationService.csproj \
    -c Release --no-build /t:PublishContainer \
    -p:ContainerImageTag=test \
    -p:ContainerRuntimeIdentifier=linux-x64
```

The workflow repeats this for `linux-arm64`, then tags and pushes both architectures to
`ghcr.io/<owner>/<repo>-migrations` and joins them into a multi-arch manifest.

### Environments

| Environment | Migration Strategy |
|-------------|-------------------|
| Development (Aspire, `pnpm backend`) | `wallow-migrations` project resource; seeder and API wait for its completion |
| Development (`dotnet run --project api/src/Wallow.Api` alone) | None — run `Wallow.MigrationService` or `dotnet ef database update` yourself |
| Testing (Testcontainers) | Inline `RunTestMigrationsAsync` in `WallowModules` |
| E2E (`docker-compose.test.yml`) | `wallow-migrations` service (`service_completed_successfully`) |
| Staging / Production | `wallow-migrations` service (`service_completed_successfully`) |

### Building the Migration Image Locally

```bash
dotnet publish api/src/Wallow.MigrationService/Wallow.MigrationService.csproj \
    -c Release /t:PublishContainer \
    -p:ContainerImageTag=local
```

### Running Migrations Manually

The service reads the standard ASP.NET Core configuration key `ConnectionStrings:DefaultConnection`,
so the environment variable is `ConnectionStrings__DefaultConnection`:

```bash
docker run --rm --network wallow \
    -e ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=wallow;Username=wallow;Password=wallow" \
    wallow-migrations:local
```

Or without containers:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=wallow;Username=wallow;Password=wallow" \
    dotnet run --project api/src/Wallow.MigrationService
```
