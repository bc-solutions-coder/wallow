# Wallow.Shared.Kernel

Core domain building blocks and abstractions for the Wallow platform.

## Purpose

Provides reusable DDD primitives, multi-tenancy infrastructure, and cross-cutting domain patterns used across all modules.

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

### Result Pattern

Railway-oriented programming with `Result<T>`. Supports `Map` and `Bind` for functional composition. Used throughout for business rule violations instead of exceptions.

### Domain Events

- `IDomainEvent` - Marker interface
- `DomainEvent` - Base class
- `AggregateRoot<TId>.RaiseDomainEvent()` - Collects events for dispatch via Wolverine

### Custom Fields System

- `CustomFieldRegistry` - Schema registry for tenant-defined fields
- `ICustomFieldValidator` - Runtime validation interface
- `CustomFieldType` - Supported field types (Text, Number, Date, Dropdown, etc.)
- `IHasCustomFields` - Marker for entities supporting custom fields
- `CustomFieldOption` / `FieldValidationRules` - Field configuration

### Other Utilities

- `DomainException` - Base exception for domain violations
- `IStronglyTypedId<T>` - Interface for type-safe identifiers
- Pagination, persistence, configuration, and extension helpers

## Dependencies

- FluentValidation
- Microsoft.EntityFrameworkCore (for strongly-typed ID converters)
