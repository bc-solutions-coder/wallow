# Database Development Guide

This guide covers database development patterns and practices in Wallow. The platform uses PostgreSQL as its primary database with EF Core for all modules, plus Dapper for complex read queries.

## Overview

Wallow uses EF Core as the primary ORM for all modules:

| Approach | Technology | Use Case | Modules |
|----------|------------|----------|---------|
| **Relational (EF Core)** | EF Core + PostgreSQL | Standard CRUD, writes, change tracking | Identity, Storage, Notifications, Announcements, Inquiries, ApiKeys, Branding |
| **Read Queries (Dapper)** | Dapper + PostgreSQL | Complex reporting, aggregations | Cross-module reporting services |

All modules share a single PostgreSQL instance but use separate schemas for isolation.

## EF Core Usage

### DbContext Pattern

Each module has its own DbContext with automatic multi-tenancy filtering:

```csharp
// api/src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/NotificationsDbContext.cs
public sealed class NotificationsDbContext : TenantAwareDbContext<NotificationsDbContext>
{
    // Email
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<EmailPreference> EmailPreferences => Set<EmailPreference>();

    // SMS
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<SmsPreference> SmsPreferences => Set<SmsPreference>();

    // InApp Notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    // Preferences
    public DbSet<ChannelPreference> ChannelPreferences => Set<ChannelPreference>();

    // Push
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<TenantPushConfiguration> TenantPushConfigurations => Set<TenantPushConfiguration>();
    public DbSet<PushMessage> PushMessages => Set<PushMessage>();

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

        // Inherited from TenantAwareDbContext: automatic tenant filtering for all ITenantScoped entities
        ApplyTenantQueryFilters(modelBuilder);
    }
}
```

### Entity Configuration with Fluent API

Entity configurations are stored separately in the `Persistence/Configurations` folder:

```csharp
// api/src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        // Primary key with strongly-typed ID
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasConversion(new StronglyTypedIdConverter<NotificationId>())
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Tenant ID (required for multi-tenancy)
        builder.Property(n => n.TenantId)
            .HasConversion(new StronglyTypedIdConverter<TenantId>())
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.HasIndex(n => n.TenantId);

        builder.Property(n => n.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Message)
            .HasColumnName("message")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(n => n.IsRead)
            .HasColumnName("is_read")
            .IsRequired();

        builder.Property(n => n.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false)
            .IsRequired();

        // Ignore domain events (not persisted)
        builder.Ignore(n => n.DomainEvents);

        // Indexes
        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => n.CreatedAt);
    }
}
```

### Strongly-Typed IDs

Wallow uses strongly-typed IDs to prevent mixing different entity IDs:

```csharp
// Define ID type in Domain layer
public readonly record struct NotificationId(Guid Value) : IStronglyTypedId<NotificationId>
{
    public static NotificationId New() => new(Guid.NewGuid());
    public static NotificationId Create(Guid value) => new(value);
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
// api/src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Interfaces/INotificationRepository.cs
public interface INotificationRepository
{
    void Add(Notification notification);
    Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken = default);
    Task<PagedResult<Notification>> GetByUserIdPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, DateTime readAt, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**Implementation (Infrastructure Layer):**
```csharp
// api/src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/NotificationRepository.cs
public sealed class NotificationRepository(NotificationsDbContext context) : INotificationRepository
{
    public void Add(Notification notification)
    {
        context.Notifications.Add(notification);
    }

    public Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken = default)
    {
        return context.Notifications
            .AsTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Notification>> GetByUserIdPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;

        IQueryable<Notification> query = context.Notifications
            .Where(n => n.UserId == userId && !n.IsArchived && (n.ExpiresAt == null || n.ExpiresAt > utcNow))
            .OrderByDescending(n => n.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);
        List<Notification> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Notification>(items, totalCount, page, pageSize);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
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
// Example read-optimized query using IReadDbContext
public sealed class NotificationReportService(IReadDbContext<NotificationsDbContext> readDbContext) : INotificationReportService
{
    public async Task<IReadOnlyList<NotificationSummaryRow>> GetNotificationsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        return await readDbContext.Context.Notifications
            .Where(n => n.CreatedAt >= from && n.CreatedAt < to)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationSummaryRow(n.Id, n.Title, n.Type, n.CreatedAt))
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
| Storage | `storage` |
| Notifications | `notifications` |
| Announcements | `announcements` |
| Inquiries | `inquiries` |
| ApiKeys | `apikeys` |
| Branding | `branding` |
| Audit (Shared) | `audit` |

This is configured in each DbContext:
```csharp
modelBuilder.HasDefaultSchema("notifications");
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
    --project api/src/Modules/Notifications/Wallow.Notifications.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context NotificationsDbContext

# Apply migrations
dotnet ef database update \
    --project api/src/Modules/Notifications/Wallow.Notifications.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context NotificationsDbContext
```

Migrations run automatically on startup via module initialization:

```csharp
public static Task<WebApplication> InitializeNotificationsModuleAsync(
    this WebApplication app)
{
    return Task.FromResult(app);
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
\dt notifications.*
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
context.Notifications.Add(notification);
context.EmailMessages.Add(emailMessage);
await context.SaveChangesAsync(ct);  // Single transaction
```

**Cross-DbContext (use with caution):**
```csharp
using IDbContextTransaction transaction = await notificationsContext.Database.BeginTransactionAsync();
try
{
    notificationsContext.Notifications.Add(notification);
    await notificationsContext.SaveChangesAsync();

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
api/src/Modules/{Module}/
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
    --project api/src/Modules/{Module}/Wallow.{Module}.Infrastructure \
    --startup-project api/src/Wallow.Api \
    --context {Module}DbContext

# Start database
cd docker && docker compose up -d postgres

# View logs
docker logs -f wallow-postgres
```
