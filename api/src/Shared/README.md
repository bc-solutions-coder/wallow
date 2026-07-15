# Shared Libraries

## Overview

Foundational libraries providing the building blocks for all Wallow modules:

- **Wallow.Shared.Kernel**: DDD base classes, multi-tenancy, Result pattern, custom fields
- **Wallow.Shared.Contracts**: Cross-module communication contracts (integration events, real-time abstractions)
- **Wallow.Shared.Infrastructure**: Settings framework, module registration, and shared infrastructure utilities
- **Wallow.Shared.Infrastructure.Core**: Core middleware, caching, messaging, auditing, and services
- **Wallow.Shared.Infrastructure.BackgroundJobs**: Hangfire job scheduling integration
- **Wallow.Shared.Infrastructure.Workflows**: Elsa workflow engine integration
- **Wallow.Shared.Infrastructure.Plugins**: Plugin system abstractions
- **Wallow.Shared.Api**: Shared API utilities

## Wallow.Shared.Kernel

Domain-driven design primitives and cross-cutting concerns shared by all modules.

### Key Concepts

**Entity Hierarchy**: Progressive enrichment for different aggregate types.
- `Entity<TId>`: Base class with identity and equality
- `AuditableEntity<TId>`: Adds CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
- `AggregateRoot<TId>`: Aggregate boundary with domain event publishing

**Strongly-Typed IDs**: `IStronglyTypedId<T>` marker interface prevents ID mix-ups across entities (e.g., `InvoiceId`, `TenantId`).

**Value Objects**: Immutable, equality-based domain concepts via `ValueObject` base class.

**Domain Events**: `IDomainEvent` marker interface for module-internal events raised by aggregate roots. Never cross module boundaries (use integration events instead).

**Result Pattern**: `Result<T>` with `Map`/`Bind` for composable operations. Eliminates exception-based control flow for expected failures.

**Multi-Tenancy**: `ITenantContext`, `ITenantScoped`, and `TenantSaveChangesInterceptor` for automatic tenant isolation at the persistence layer.

**Custom Fields**: `CustomFieldRegistry`, `ICustomFieldValidator`, `CustomFieldType`, and `IHasCustomFields` for tenant-defined dynamic fields.

### Dependencies

- FluentValidation
- Microsoft.EntityFrameworkCore (for strongly-typed ID converters)

## Wallow.Shared.Contracts

Cross-module communication contracts and abstractions. Intentionally dependency-free.

### Integration Events

Events published by one module and consumed by others via Wolverine in-memory messaging. All events extend `IntegrationEvent` (which implements `IIntegrationEvent`).

**Identity**: `UserRegisteredEvent`, `OrganizationCreatedEvent`, `UserRoleChangedEvent`, `PasswordResetRequestedEvent`, `OrganizationMemberAddedEvent`, and others.

**Billing**: `InvoiceCreatedEvent`, `InvoicePaidEvent`, `InvoiceOverdueEvent`, `PaymentReceivedEvent`.

**Delivery**: `EmailSentEvent`.

**Notifications**: `NotificationCreatedEvent`.

**Metering**: `QuotaThresholdReachedEvent`, `UsageFlushedEvent`.

### Cross-Module Query Services

Modules expose read-only interfaces implemented in their Infrastructure layer:
- `IUserQueryService` (Identity)
- `IInvoiceQueryService`, `ISubscriptionQueryService`, `IRevenueReportService` (Billing)
- `IMeteringQueryService`, `IUsageReportService` (Metering)

### Real-time Abstractions

- `IRealtimeDispatcher`: Sends real-time notifications to clients
- `ISseDispatcher`: Server-Sent Events dispatcher
- `IPresenceService`: Tracks online/offline user presence
- `RealtimeEnvelope`: Module-specific message wrapper

### Dependencies

None. Intentionally dependency-free for maximum portability.

## Wallow.Shared.Infrastructure

Settings framework and module coordination.

### Key Components

- **Settings Framework** (`Settings/`): `TenantSettingEntity`, `UserSettingEntity`, `CachedSettingsService`, and repository implementations for tenant- and user-scoped settings with cache invalidation
- **Module Registration**: Central module registration entry point used by `WallowModules.cs` in the API host

### Dependencies

- Wallow.Shared.Kernel
- Wallow.Shared.Contracts
- Wallow.Shared.Infrastructure.Core
- Wallow.Shared.Infrastructure.BackgroundJobs
- Wallow.Shared.Infrastructure.Workflows
- Wallow.Shared.Infrastructure.Plugins
- EF Core, Wolverine, Hangfire, Elsa, Serilog

## Dependency Rules

**Kernel**: Referenced by Domain, Application, and Infrastructure layers of all modules. Never references any module directly.

**Contracts**: Referenced by Application (for integration event DTOs) and Infrastructure (for event consumers). Never references any module, Kernel, or external packages.

**Cross-Module Communication**: Always via integration events through Wolverine. Never direct assembly references between modules.
