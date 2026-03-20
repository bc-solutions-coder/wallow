# Wallow.Shared.Contracts

## Module Responsibility

The sole shared contract surface between modules. Contains integration event definitions, cross-module DTOs, and real-time messaging abstractions. This is the **only** project that modules may reference to communicate with each other. When module A needs to notify module B that something happened, the event type is defined here.

## Layer Rules

This is a **shared contract library**, not a module. It has no layers of its own.

- **May** be referenced by any module's Application or Infrastructure layer (for consuming/publishing integration events).
- **Must not** reference any module.
- **Must not** reference `Wallow.Shared.Kernel` (Contracts has zero package dependencies by design).
- **Must not** contain implementation logic, only type definitions (records, interfaces, enums).

## Key Patterns

- **Integration events**: `IIntegrationEvent` marker interface with `EventId` and `OccurredAt`. `IntegrationEvent` abstract record provides defaults. All cross-module events extend this. Events are organized by source module namespace:
  - `Identity.Events`: `UserRegisteredEvent`, `UserRoleChangedEvent`, `OrganizationCreatedEvent`, `OrganizationMemberAddedEvent`, `OrganizationMemberRemovedEvent`, `PasswordResetRequestedEvent`
  - `Billing.Events`: `InvoiceCreatedEvent`, `PaymentReceivedEvent`, `InvoicePaidEvent`, `InvoiceOverdueEvent`
  - `Communications.Events`: `EmailSentEvent`, `SendEmailRequestedEvent`, `NotificationCreatedEvent`, `AnnouncementPublishedEvent`
  - `Metering.Events`: `UsageFlushedEvent`, `QuotaThresholdReachedEvent`
- **Real-time abstractions**: `IPresenceService` (track user connections, page context), `IRealtimeDispatcher` (send messages to users/groups/all), `RealtimeEnvelope` (standard wrapper with `Source`, `Type`, `Payload`, `Timestamp`), `UserPresence` (connection state DTO). Implemented in `Wallow.Api` via SignalR + Redis.
- **Naming convention**: Events use past tense (`UserRegisteredEvent`, `InvoicePaidEvent`). They describe what happened, not what should happen.

## Dependencies

- **Depends on**: Nothing. Zero NuGet packages, zero project references. This is intentional -- contracts must be dependency-free for maximum portability.
- **Depended on by**: All modules' Application layers (to consume/publish events), `Wallow.Api` (for real-time abstractions and RabbitMQ routing), `Wallow.Shared.Kernel` does NOT depend on this.

## Constraints

- Do not add NuGet package references. This project must remain dependency-free.
- Do not add implementation classes (services, handlers, repositories). Only interfaces, records, and enums.
- Use only primitive types and simple DTOs in event properties. No domain entities, no strongly-typed IDs, no value objects. Use `Guid` for IDs, `string` for names, `decimal` for amounts.
- Events are immutable records. Do not add mutable properties.
- Namespace convention: `Wallow.Shared.Contracts.{SourceModule}.Events.{EventName}`. The source module is where the event originates, not where it is consumed.
- When adding a new integration event type, you must also register its `PublishMessage` routing in `Program.cs` and ensure a RabbitMQ queue is listening for it.
- Do not put domain events here. Domain events (`IDomainEvent` from Kernel) are internal to a module. Only integration events belong in Contracts.
- Real-time abstractions (`IPresenceService`, `IRealtimeDispatcher`) are defined here so modules can dispatch real-time messages without depending on SignalR directly.
