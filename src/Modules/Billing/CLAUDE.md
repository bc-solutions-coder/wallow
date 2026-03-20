# Billing Module

## Module Responsibility

Owns invoice lifecycle management, payment processing, and subscription management. Invoices progress through a state machine (Draft -> Issued -> Paid/Overdue/Cancelled). Payments record transactions against invoices. Subscriptions model recurring billing with status transitions (Active -> PastDue/Cancelled/Expired). All entities are tenant-scoped. Domain events are raised on state transitions and published as integration events via RabbitMQ for downstream consumers.

## Layer Rules

- **Domain** (`Wallow.Billing.Domain`): Aggregate roots (`Invoice` with `InvoiceLineItem` child entities, `Payment`, `Subscription`), value objects (`Money` with currency enforcement), strongly-typed IDs (`InvoiceId`, `InvoiceLineItemId`, `PaymentId`, `SubscriptionId`), enums (`InvoiceStatus`, `PaymentStatus`, `PaymentMethod`, `SubscriptionStatus`), domain events, and domain exceptions (`InvalidInvoiceException`, `InvalidPaymentException`, `InvalidSubscriptionStatusTransitionException`). Domain depends only on `Shared.Kernel`.
- **Application** (`Wallow.Billing.Application`): CQRS commands (`CreateInvoice`, `AddLineItem`, `IssueInvoice`, `CancelInvoice`, `ProcessPayment`, `CreateSubscription`, `CancelSubscription`), queries (`GetInvoiceById`, `GetAllInvoices`, `GetInvoicesByUserId`, `GetPaymentById`, `GetPaymentsByInvoiceId`, `GetSubscriptionById`, `GetSubscriptionsByUserId`), Wolverine handlers, domain event handlers that bridge to integration events (`InvoiceCreatedDomainEventHandler`, `InvoicePaidDomainEventHandler`, `InvoiceOverdueDomainEventHandler`, `PaymentReceivedDomainEventHandler`), repository interfaces, mappings, and DTOs. Must not reference Infrastructure or Api.
- **Infrastructure** (`Wallow.Billing.Infrastructure`): `BillingDbContext` (EF Core, `billing` schema), entity configurations, repository implementations (`InvoiceRepository`, `PaymentRepository`, `SubscriptionRepository`), `TenantSaveChangesInterceptor` integration. Auto-migrates on startup.
- **Api** (`Wallow.Billing.Api`): Controllers (`InvoicesController`, `PaymentsController`, `SubscriptionsController`), request/response contracts. Extension methods `AddBillingModule` / `UseBillingModuleAsync` called from `Program.cs`.

## Key Patterns

- **Rich domain model**: `Invoice` is a true aggregate root. State transitions (`Issue`, `MarkAsPaid`, `MarkAsOverdue`, `Cancel`) enforce invariants and raise domain events. Line items can only be added/removed in Draft status. Total is recalculated automatically.
- **Money value object**: Enforces non-negative amounts and ISO 4217 3-letter currency codes. Currency mismatch on arithmetic throws `BusinessRuleException`.
- **Domain-to-integration event bridge**: Domain event handlers in Application layer receive domain events (e.g., `InvoiceCreatedDomainEvent`), enrich them with additional data, and publish integration events (e.g., `InvoiceCreatedEvent`) via `IMessageBus.PublishAsync` to RabbitMQ.
- **Wolverine CQRS**: Static `HandleAsync` methods. Commands mutate aggregate roots and persist via repositories. Queries read via repositories and map to DTOs.
- **Multi-tenancy**: All entities implement `ITenantScoped`. `TenantSaveChangesInterceptor` auto-stamps `TenantId`. Queries filter by tenant.

## Dependencies

- **Depends on**: `Wallow.Shared.Kernel` (base entities, value objects, multi-tenancy, Result pattern), `Wallow.Shared.Contracts` (publishes `Billing.Events.*`: `InvoiceCreatedEvent`, `PaymentReceivedEvent`, `InvoicePaidEvent`, `InvoiceOverdueEvent`).
- **Depended on by**: `Wallow.Api` (registers module). Integration events published to `billing-events` RabbitMQ exchange, consumed by `billing-inbox` queue listeners.

## Constraints

- Do not bypass aggregate root methods for state changes. Always call `Invoice.Issue()`, `Invoice.MarkAsPaid()`, etc. Never set `Status` directly.
- Do not mix currencies in `Money` arithmetic. The value object enforces this, but be aware when creating line items.
- Do not reference other modules. Publish integration events through `Shared.Contracts` for cross-module communication.
- This module uses the `billing` PostgreSQL schema. Do not share tables with other modules.
- Wolverine handler discovery for this module is explicitly registered in `Program.cs` via `opts.Discovery.IncludeAssembly(typeof(CreateInvoiceCommand).Assembly)`. If you add handlers in a new assembly, register it there.
- Integration events use plain `Guid` for IDs (not strongly-typed IDs) for simpler serialization across module boundaries.
