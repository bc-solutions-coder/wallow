# Wallow.Shared.Kernel

## Module Responsibility

Provides the foundational base classes, abstractions, and cross-cutting infrastructure that all modules inherit from. This is the DDD building block library: base entity types, aggregate root with domain event support, value objects, strongly-typed IDs, the Result pattern, multi-tenancy primitives, domain exception hierarchy, and Wolverine error handling configuration. Every module's Domain layer depends on this project.

## Layer Rules

This is a **shared library**, not a module. It has no layers of its own.

- **May** be referenced by any module's Domain, Application, or Infrastructure layer.
- **Must not** reference any module (no circular dependencies).
- **Must not** reference `Wallow.Shared.Contracts` (Kernel is lower-level than Contracts).
- **Must not** contain module-specific business logic. Only generic, reusable building blocks belong here.

## Key Patterns

- **Entity hierarchy**: `Entity<TId>` (identity + equality) -> `AuditableEntity<TId>` (adds `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`) -> `AggregateRoot<TId>` (adds domain event collection with `RaiseDomainEvent` / `ClearDomainEvents`). All entities use strongly-typed IDs.
- **Strongly-typed IDs**: `IStronglyTypedId<T>` interface with `Create(Guid)` and `New()` static abstract methods. Modules define IDs as `readonly record struct` (e.g., `InvoiceId`, `NotificationId`). `StronglyTypedIdConverter` provides EF Core value conversion.
- **Value objects**: `ValueObject` base class with `GetEqualityComponents()` for structural equality. Used for types like `Money`, `EmailAddress`.
- **Domain exceptions**: `DomainException` (abstract, has `Code` property) -> `EntityNotFoundException` (404), `BusinessRuleException` (422). The API layer's `GlobalExceptionHandler` maps these to RFC 7807 Problem Details.
- **Result pattern**: `Result` and `Result<TValue>` for operation outcomes without exceptions. Supports `Map` and `Bind` for chaining. `Error` record for structured error codes.
- **Multi-tenancy**: `ITenantContext` (current tenant), `TenantContext` (mutable implementation set by middleware), `ITenantScoped` (interface for tenant-owned entities), `TenantId` (strongly-typed ID), `TenantSaveChangesInterceptor` (auto-stamps `TenantId` on EF Core save), `TenantQueryExtensions` (filters queries by tenant).
- **Wolverine extensions**: `ConfigureStandardErrorHandling` (retry policies with exponential backoff, dead letter queue), `ConfigureMessageLogging`. Called from `Program.cs` during Wolverine setup.
- **Background jobs**: `IRecurringJobRegistration` interface for modules to register Hangfire recurring jobs.

## Dependencies

- **Depends on**: WolverineFx, FluentValidation, EF Core (for `StronglyTypedIdConverter` and `TenantSaveChangesInterceptor`), `Microsoft.Extensions.Logging.Abstractions`.
- **Depended on by**: Every module's Domain layer, plus Application and Infrastructure layers. Also referenced by `Wallow.Api`.

## Constraints

- Do not add module-specific types here. If a type is only used by one module, it belongs in that module.
- Do not add HTTP/ASP.NET Core dependencies. This library must remain framework-agnostic (except EF Core for ID converters and tenant interceptor).
- Do not add integration event types here. Those belong in `Wallow.Shared.Contracts`.
- Every new entity must use a strongly-typed ID implementing `IStronglyTypedId<T>`. Do not use raw `Guid` or `int` for entity IDs.
- Every tenant-owned entity must implement `ITenantScoped`. The `TenantSaveChangesInterceptor` depends on this interface.
- Domain events (`IDomainEvent`) are internal to a module. They are distinct from integration events (`IIntegrationEvent` in Contracts) which cross module boundaries.
