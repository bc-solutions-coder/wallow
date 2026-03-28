# Database Migrations in Wallow

This document explains how database table creation and migrations work in Wallow's modular monolith architecture.

## Overview

Wallow uses **EF Core Migrations** for all modules. Each module owns its own PostgreSQL **schema** and has isolated storage.

### Key Principles

1. **One schema per module** - Each module uses a separate PostgreSQL schema (e.g., `billing`, `identity`, `notifications`)
2. **Isolated migration history** - Each EF Core schema has its own `__EFMigrationsHistory` table
3. **Auto-migration at startup** - Migrations run automatically when the API starts
4. **Multi-tenancy support** - Migrations use a `DesignTimeTenantContext` mock for design-time operations

## Architecture

```
Module Infrastructure Layer
├── Persistence/
│   ├── {Module}DbContext.cs          # EF Core DbContext
│   ├── {Module}DbContextFactory.cs   # Design-time factory for CLI
│   ├── DesignTimeTenantContext.cs    # Mock tenant for migrations
│   └── Configurations/
│       └── {Entity}Configuration.cs  # Entity type configurations
└── Migrations/
    ├── {timestamp}_{Name}.cs         # Migration code
    ├── {timestamp}_{Name}.Designer.cs # Migration metadata
    └── {Module}DbContextModelSnapshot.cs # Current model state
```

## How Migrations Work

### 1. DbContext Setup

Each module's DbContext sets its schema in `OnModelCreating`:

```csharp
public sealed class BillingDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DbSet<Invoice> Invoices => Set<Invoice>();
    // ... other DbSets

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Set the schema for all tables in this module
        modelBuilder.HasDefaultSchema("billing");

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);

        // Add tenant query filters
        modelBuilder.Entity<Invoice>()
            .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
    }
}
```

### 2. DI Registration

In `InfrastructureExtensions.cs`, each module registers its DbContext with a **schema-specific migration history table**:

```csharp
services.AddDbContext<BillingDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        // CRITICAL: Each module has its own migration history table in its schema
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "billing");
    });
    options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
});
```

### 3. Auto-Migration at Startup

In `{Module}ModuleExtensions.cs`, migrations run automatically:

```csharp
public static async Task<WebApplication> UseBillingModuleAsync(this WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        // Apply any pending migrations
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("BillingModule");
        logger.LogWarning(ex, "Billing module startup failed.");
    }
    return app;
}
```

### 4. Design-Time Factory

For `dotnet ef` CLI commands to work, each module needs a **design-time factory**:

```csharp
public class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>();

        // Hardcoded connection for design-time
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=wallow;Username=postgres;Password=postgres");

        // Mock tenant context (required by DbContext constructor)
        var mockTenantContext = new DesignTimeTenantContext();

        return new BillingDbContext(optionsBuilder.Options, mockTenantContext);
    }
}
```

### 5. Design-Time Tenant Context

A mock `ITenantContext` for design-time operations:

```csharp
internal sealed class DesignTimeTenantContext : ITenantContext
{
    public TenantId TenantId => new(Guid.Parse("00000000-0000-0000-0000-000000000000"));
    public string TenantName => "design-time";
    public bool IsResolved => true;

    public void SetTenant(TenantId tenantId, string tenantName = "") { }
    public void Clear() { }
}
```

## Creating Migrations

### Command

```bash
dotnet ef migrations add {MigrationName} \
    --project src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {Module}DbContext
```

### Example

```bash
# Add initial migration for Billing module
dotnet ef migrations add InitialCreate \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext

# Add a new migration for schema changes
dotnet ef migrations add AddPaymentRefundField \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext
```

## Module Status Reference

### EF Core Modules

| Module | Schema | Has Migrations | Has Factory |
|--------|--------|----------------|-------------|
| Identity | `identity` | Yes | Yes |
| Billing | `billing` | Yes | Yes |
| Storage | `storage` | Yes | Yes |
| Notifications | `notifications` | Yes | Yes |
| Messaging | `messaging` | Yes | Yes |
| Announcements | `announcements` | Yes | Yes |
| Inquiries | `inquiries` | Yes | Yes |
| ApiKeys | `apikeys` | Yes | Yes |
| Branding | `branding` | Yes | Yes |

### Shared Infrastructure

| Context | Schema | Has Migrations | Notes |
|---------|--------|----------------|-------|
| AuditDbContext | `audit` | Yes | Audit interceptor in Shared.Infrastructure.Core |

## Troubleshooting

### "Table already exists" error

The schema was created by `EnsureCreatedAsync()` but now you're trying to run migrations:
- Drop the schema (dev) or fake the initial migration (prod)

### "No migrations have been applied"

Missing migrations folder or migration history table:
- Run `dotnet ef migrations add InitialCreate` to generate migrations
- Ensure `MigrationsHistoryTable()` is configured in DI registration

### "Could not load type for DbContext"

Missing `IDesignTimeDbContextFactory`:
- Create a factory class implementing `IDesignTimeDbContextFactory<TContext>`

### "ITenantContext not resolved"

Missing design-time tenant context:
- Create `DesignTimeTenantContext` implementing `ITenantContext`
- Use it in your `DbContextFactory`

