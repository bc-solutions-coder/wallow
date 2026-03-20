# Wallow.Shared.Kernel

Core domain building blocks and abstractions for the Wallow platform.

## Purpose

Provides reusable DDD primitives, multi-tenancy infrastructure, and cross-cutting domain patterns used across all modules. This package contains **zero external dependencies** and defines the architectural foundation.

## Key Components

### Entity Hierarchy
```
Entity<TId>                    (identity + equality)
  AuditableEntity<TId>         (+ CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
    AggregateRoot<TId>         (+ domain events collection)
```

### Multi-Tenancy System
- `ITenantContext` - Current tenant state (injected via middleware)
- `TenantId` - Strongly-typed wrapper around Guid
- `ITenantScoped` - Marker interface for tenant-owned entities
- `TenantSaveChangesInterceptor` - Auto-stamps TenantId, prevents cross-tenant updates
- `TenantQueryExtensions` - Automatic EF Core query filters

### Result Pattern
Railway-oriented programming with `Result<T>`:
- `Map`, `Bind` for functional composition
- Used throughout for business rule violations (no exceptions in domain)

### Domain Events
- `IDomainEvent` - Marker interface
- `AggregateRoot<TId>.RaiseDomainEvent()` - Collects events for dispatch
- Events dispatched via Wolverine message bus

### Custom Fields System
- `CustomFieldDefinition` - Schema for tenant-defined fields
- `CustomFieldValue` - Type-safe storage (Text, Number, Date, Dropdown, etc.)
- `CustomFieldValidator` - Runtime validation

### Background Jobs
- `IRecurringJobRegistration` - Interface for Hangfire job setup
- `BackgroundJobAttribute` - Marks background operations

## Dependencies

**NuGet Packages:**
- None (intentionally zero dependencies)

**Internal:**
- None (this is the foundation)

## Usage Example

```csharp
public class Invoice : AggregateRoot<InvoiceId>, ITenantScoped
{
    public TenantId TenantId { get; private set; }
    public Money Total { get; private set; }
    public InvoiceStatus Status { get; private set; }

    public Result Pay(Money amount)
    {
        if (amount != Total)
            return Result.Failure("Payment amount must match invoice total");

        Status = InvoiceStatus.Paid;
        RaiseDomainEvent(new InvoicePaidDomainEvent(Id, TenantId));
        return Result.Success();
    }
}
```

## Extension Points

- Implement `IEntity<TId>` for custom entity types
- Inherit from `AggregateRoot<TId>` for domain aggregates
- Use `ITenantScoped` for tenant-isolated data
- Register `TenantSaveChangesInterceptor` in DbContext

## NuGet Potential

**High** - This package is ready for extraction as a standalone NuGet package for other modular monolith projects.
