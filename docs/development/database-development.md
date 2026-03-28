# Database Development Guide

This guide covers database development patterns and practices in Wallow. The platform uses PostgreSQL as its primary database with EF Core for all modules, plus Dapper for complex read queries.

## Overview

Wallow uses EF Core as the primary ORM for all modules:

| Approach | Technology | Use Case | Modules |
|----------|------------|----------|---------|
| **Relational (EF Core)** | EF Core + PostgreSQL | Standard CRUD, writes, change tracking | Identity, Billing, Storage, Notifications, Messaging, Announcements, Inquiries, ApiKeys, Branding |
| **Read Queries (Dapper)** | Dapper + PostgreSQL | Complex reporting, aggregations | Cross-module reporting services |

All modules share a single PostgreSQL instance but use separate schemas for isolation.

## EF Core Usage

### DbContext Pattern

Each module has its own DbContext with automatic multi-tenancy filtering:

```csharp
// src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/BillingDbContext.cs
public sealed class BillingDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public BillingDbContext(
        DbContextOptions<BillingDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Each module uses its own schema
        modelBuilder.HasDefaultSchema("billing");
        
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);

        // Automatic tenant filtering for all ITenantScoped entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var tenantId = Expression.Property(
                    Expression.Constant(_tenantContext),
                    nameof(ITenantContext.TenantId));
                var equals = Expression.Equal(property, tenantId);
                var lambda = Expression.Lambda(equals, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}
```

### Entity Configuration with Fluent API

Entity configurations are stored separately in the `Persistence/Configurations` folder:

```csharp
// src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Configurations/InvoiceConfiguration.cs
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        // Primary key with strongly-typed ID
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasConversion(new StronglyTypedIdConverter<InvoiceId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Tenant ID (required for multi-tenancy)
        builder.Property(i => i.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .HasColumnName("tenant_id")
            .IsRequired();

        // Value object (owned entity)
        builder.OwnsOne(i => i.TotalAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("total_amount")
                .HasPrecision(18, 2)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        // JSONB column for flexible data
        builder.Property(i => i.CustomFields)
            .HasColumnName("custom_fields")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonOptions))
            .Metadata.SetValueComparer(new DictionaryValueComparer());

        // Ignore domain events (not persisted)
        builder.Ignore(i => i.DomainEvents);

        // Relationships
        builder.HasMany(i => i.LineItems)
            .WithOne()
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => i.Status);
    }
}
```

### Strongly-Typed IDs

Wallow uses strongly-typed IDs to prevent mixing different entity IDs:

```csharp
// Define ID type in Domain layer
public readonly record struct InvoiceId(Guid Value) : IStronglyTypedId<InvoiceId>
{
    public static InvoiceId New() => new(Guid.NewGuid());
    public static InvoiceId Create(Guid value) => new(value);
}

// Generic converter for EF Core
public class StronglyTypedIdConverter<TId> : ValueConverter<TId, Guid>
    where TId : struct, IStronglyTypedId<TId>
{
    public StronglyTypedIdConverter()
        : base(
            id => id.Value,
            guid => TId.Create(guid))
    {
    }
}
```

### Multi-Tenancy

All tenant-scoped entities implement `ITenantScoped`:

```csharp
public interface ITenantScoped
{
    TenantId TenantId { get; set; }
}
```

The DbContext automatically applies query filters based on the current tenant context. This ensures complete data isolation between tenants without explicit filtering in every query.

### Repository Pattern

Repositories abstract data access and follow this structure:

**Interface (Application Layer):**
```csharp
// src/Modules/Billing/Wallow.Billing.Application/Interfaces/IInvoiceRepository.cs
public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken ct = default);
    Task<Invoice?> GetByIdWithLineItemsAsync(InvoiceId id, CancellationToken ct = default);
    Task<IReadOnlyList<Invoice>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, CancellationToken ct = default);
    void Add(Invoice invoice);
    void Update(Invoice invoice);
    void Remove(Invoice invoice);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**Implementation (Infrastructure Layer):**
```csharp
// src/Modules/Billing/Wallow.Billing.Infrastructure/Persistence/Repositories/InvoiceRepository.cs
public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly BillingDbContext _context;

    public InvoiceRepository(BillingDbContext context)
    {
        _context = context;
    }

    public async Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken ct = default)
    {
        return await _context.Invoices.FindAsync([id], ct);
    }

    public async Task<Invoice?> GetByIdWithLineItemsAsync(InvoiceId id, CancellationToken ct = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public void Add(Invoice invoice)
    {
        _context.Invoices.Add(invoice);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
```

### Query Patterns: EF Core vs Dapper

**Use EF Core for:**
- Standard CRUD operations
- Loading entities with related data
- Write operations that need change tracking
- Simple queries with filters

**Use Dapper for:**
- Complex reporting queries
- Aggregations and analytics
- Performance-critical read paths
- Queries spanning multiple schemas

**Read-optimized query via IReadDbContext:**

For reporting queries, the codebase uses `IReadDbContext<TContext>` which provides a read-only DbContext instance (potentially routed to a read replica):

```csharp
// src/Modules/Billing/Wallow.Billing.Infrastructure/Services/InvoiceReportService.cs
public sealed class InvoiceReportService(IReadDbContext<BillingDbContext> readDbContext) : IInvoiceReportService
{
    public async Task<IReadOnlyList<InvoiceReportRow>> GetInvoicesAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await readDbContext.Context.Invoices
            .Where(i => i.Status != InvoiceStatus.Draft)
            .Where(i => i.CreatedAt >= from && i.CreatedAt < to)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceReportRow(...))
            .ToListAsync(ct);
    }
}
```

Dapper can still be used for raw SQL queries when needed, particularly for complex aggregations or cross-schema joins.

## Database Schema Management

### Module Schema Separation

Each module uses its own PostgreSQL schema:

| Module | Schema |
|--------|--------|
| Identity | `identity` |
| Billing | `billing` |
| Storage | `storage` |
| Notifications | `notifications` |
| Messaging | `messaging` |
| Announcements | `announcements` |
| Inquiries | `inquiries` |
| ApiKeys | `apikeys` |
| Branding | `branding` |
| Audit (Shared) | `audit` |

This is configured in each DbContext:
```csharp
modelBuilder.HasDefaultSchema("billing");
```

### Connection String Configuration

**appsettings.Development.json:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wallow;Username=wallow;Password=wallow;SSL Mode=Disable",
    "Redis": "localhost:6379,password=WallowValkey123!,abortConnect=false"
  }
}
```

### EF Core Migrations

Create migrations per module:

```bash
# Create a new migration
dotnet ef migrations add MigrationName \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext

# Apply migrations
dotnet ef database update \
    --project src/Modules/Billing/Wallow.Billing.Infrastructure \
    --startup-project src/Wallow.Api \
    --context BillingDbContext
```

Migrations run automatically on startup via module initialization:

```csharp
public static async Task<WebApplication> InitializeBillingModuleAsync(this WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await db.Database.MigrateAsync();
    return app;
}
```

## Local Development Setup

### Docker Infrastructure

Start required services:

```bash
cd docker
docker compose up -d
```

This provides:
- **PostgreSQL 18** on port 5432
- **Valkey** (Redis-compatible) on port 6379
- **GarageHQ** (S3-compatible) on port 3900
- **Mailpit** on ports 1025 (SMTP) / 8025 (web UI)
- **Grafana LGTM** on port 3001

### Database Access

```bash
# Connect via psql
docker exec -it wallow-postgres psql -U wallow -d wallow

# List schemas
\dn

# List tables in a schema
\dt billing.*
```

## Best Practices

### When to Use Each Technology

| Scenario | Technology | Reasoning |
|----------|------------|-----------|
| Simple CRUD | EF Core | Standard patterns, change tracking |
| Complex joins/aggregates | Dapper | Raw SQL performance |
| Audit-critical data | EF Core + Audit interceptor | Interceptor-based audit trail in Shared.Infrastructure.Core |
| High-throughput reads | Dapper + Materialized Views | Maximum performance |

### Performance Considerations

1. **Indexes** - Always add indexes for foreign keys and frequently queried columns
2. **JSONB queries** - Create GIN indexes for JSONB columns when querying inside JSON
3. **Eager loading** - Use `.Include()` to avoid N+1 queries
4. **Projections** - Keep projections focused; create specialized projections for different query patterns
5. **Connection pooling** - Let the connection string configure pooling appropriately

### Transaction Handling

**EF Core:**
```csharp
// Implicit transaction via SaveChangesAsync
_context.Invoices.Add(invoice);
_context.Payments.Add(payment);
await _context.SaveChangesAsync(ct);  // Single transaction
```

**Cross-DbContext (use with caution):**
```csharp
using var transaction = await _billingContext.Database.BeginTransactionAsync();
try
{
    _billingContext.Invoices.Add(invoice);
    await _billingContext.SaveChangesAsync();
    
    // Other operations...
    
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### Testing Patterns

**Integration tests with Testcontainers:**

Integration tests use `WallowApiFactory` which manages PostgreSQL and Valkey containers automatically. See the [testing guide](testing.md) for details on `WallowApiFactory` and container lifecycle.

## Quick Reference

### File Locations

```
src/Modules/{Module}/
├── Wallow.{Module}.Domain/
│   ├── Entities/           # Domain entities
│   └── Enums/              # Domain enumerations
├── Wallow.{Module}.Application/
│   ├── Interfaces/         # Repository interfaces
│   └── Services/           # Domain services
└── Wallow.{Module}.Infrastructure/
    ├── Persistence/
    │   ├── {Module}DbContext.cs
    │   ├── Configurations/  # Entity configurations
    │   └── Repositories/    # Repository implementations
    ├── Migrations/          # EF Core migrations
    └── Services/            # Query services (Dapper)
```

### Common Commands

```bash
# Run tests
./scripts/run-tests.sh

# Create migration
dotnet ef migrations add MigrationName \
    --project src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project src/Wallow.Api \
    --context {Module}DbContext

# Start database
cd docker && docker compose up -d postgres

# View logs
docker logs -f wallow-postgres
```
