# Shared Libraries

## Overview

Two foundational libraries providing the building blocks for all Wallow modules:

- **Wallow.Shared.Kernel**: DDD base classes, multi-tenancy, Result pattern, Wolverine error handling
- **Wallow.Shared.Contracts**: Cross-module communication contracts (integration events, real-time abstractions)

These libraries are the backbone of Wallow's architecture, enabling consistent patterns across all modules while maintaining loose coupling and clear boundaries.

## Wallow.Shared.Kernel

The Kernel library provides domain-driven design primitives and cross-cutting concerns shared by all modules.

### Key Concepts

**Entity Hierarchy**: Progressive enrichment for different aggregate types
- `Entity<TId>`: Base class with identity and equality
- `AuditableEntity<TId>`: Adds CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
- `AggregateRoot<TId>`: Aggregate boundary with domain event publishing

**Strongly-Typed IDs**: Prevents ID mix-ups across different entities
- `IStronglyTypedId<T>`: Marker interface for type-safe IDs
- Examples: `InvoiceId`, `UserId`, `ProductId` (each a distinct type)
- Prevents accidental passing of wrong ID type to domain logic

**Value Objects**: Immutable, equality-based, reusable domain concepts
- Examples: `Money`, `EmailAddress`, `Address`
- Encapsulates validation and business logic
- Multiple value objects can share same underlying type without collision

**Domain Events**: Module-internal events representing state changes
- `IDomainEvent`: Marker interface for events within an aggregate
- Raised by aggregate roots during command processing
- Dispatched immediately to in-process handlers
- Never cross module boundaries (use Integration Events instead)

**Result Pattern**: Railway-oriented programming for error handling
- `Result<T>`: Represents success or failure
- `Map`/`Bind`: Composable operations that short-circuit on error
- `Error`: Encapsulates error information (Code, Message, Details)
- Eliminates exception-based control flow for expected failures

**Multi-Tenancy**: Built-in isolation at the persistence layer
- `ITenantContext`: Provides current tenant ID
- `ITenantScoped`: Marks entities as tenant-isolated
- `TenantSaveChangesInterceptor`: Automatically filters queries and enforces isolation
- `TenantQueryExtensions`: Simplifies tenant-filtered queries

### Key Types

**DDD Primitives**
- `Entity<TId>` – Base class for all entities
- `AuditableEntity<TId>` – Entity with audit timestamps and creators
- `AggregateRoot<TId>` – Aggregate boundary with event publication
- `ValueObject` – Base class for immutable value objects
- `DomainException` – Exception for domain violations

**Strongly-Typed IDs**
- `IStronglyTypedId<T>` – Interface for type-safe identifiers
- `TenantId` – Multi-tenant system identifier

**Result Pattern**
- `Result<T>` – Success or failure
- `Result<T>.Success(value)` – Create successful result
- `Result<T>.Failure(error)` – Create failure result
- `Error` – Error information container

**Multi-Tenancy**
- `ITenantContext` – Provides current tenant
- `ITenantScoped` – Marks tenant-isolated entities
- `TenantSaveChangesInterceptor` – EF Core interceptor for isolation
- `TenantQueryExtensions.ForTenant()` – Filter queries by tenant

**Events**
- `IDomainEvent` – Marker interface for domain events
- `DomainEvent` – Base class for domain events

### Dependencies

- **WolverineFx**: Mediator and error handling integration
- **FluentValidation**: Validation rules for value objects and entities
- **Microsoft.EntityFrameworkCore**: Entity configurations and multi-tenancy interceptors

## Wallow.Shared.Contracts

The Contracts library defines cross-module communication contracts and abstractions without any external dependencies.

### Key Concepts

**Integration Events**: Immutable, asynchronous communication between modules
- Events published by one module and consumed by others
- Represent facts about what happened (past tense)
- Sent via RabbitMQ through Wolverine message bus
- Never modify events or add handlers to others' events
- Each module owns its integration event namespace

**Real-time Abstractions**: Decoupled real-time messaging without SignalR coupling
- `IRealtimeDispatcher`: Sends real-time notifications to clients
- `IPresenceService`: Tracks online/offline user presence
- Implementations provided by platform (e.g., SignalR hub)
- Modules reference only the abstraction, never the implementation

**Cross-Module DTOs**: Lightweight references for integration
- Examples of data structures shared between bounded contexts
- Keep these minimal and stable

### Integration Events by Module

**Identity**
- `UserRegisteredEvent` – New user account created
- `UserRoleChangedEvent` – User roles updated
- `OrganizationCreatedEvent` – Organization established

**Billing**
- `InvoiceCreatedEvent` – Invoice generated
- `InvoicePaidEvent` – Payment received
- `InvoiceOverdueEvent` – Payment deadline missed

**Communications**
- `EmailSentEvent` – Email message delivered
- `NotificationCreatedEvent` – Notification generated

### Dependencies

**None** – Intentionally dependency-free for maximum portability. This library contains only:
- Immutable record definitions
- Interface abstractions
- DTOs and contracts

This ensures modules can reference Contracts without pulling in heavy dependencies, and the library can be shared across service boundaries.

## Dependency Rules

**Kernel Usage**
- Referenced by: Domain, Application, and Infrastructure layers of all modules
- Never referenced by: Web controllers (use Application DTOs instead)
- Never references: Any module directly

**Contracts Usage**
- Referenced by: Application (for integration event DTOs) and Infrastructure (for event consumers)
- Never referenced by: Domain layer (domain events only), Web layer directly
- Never references: Any module directly, Kernel, or external packages

**Layering**
```
Web Controllers
     ↓ (depends on)
Application (DTOs, Handlers)
     ↓ (depends on)
Domain + Kernel/Contracts
     ↓ (depends on)
Infrastructure (EF Core, Dapper, Consumers)
```

**Cross-Module Communication**
- Never direct assembly references between modules
- Always use Integration Events (via RabbitMQ/Wolverine)
- Always use Real-time Abstractions for push notifications
- Contracts library enables this decoupling
