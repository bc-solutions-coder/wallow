# Foundry.Shared.Contracts

Integration event contracts and cross-module service interfaces.

## Purpose

Defines the **inter-module communication boundary**. Modules reference ONLY this package to communicate via events or query other modules' data. This enables module autonomy and supports future microservice extraction.

## Key Components

### Integration Events

Base contract:
```csharp
public record IntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
```

**Published Events (~30 total):**
- **Identity:** UserRegisteredEvent, OrganizationCreatedEvent, UserRoleChangedEvent, etc.
- **Billing:** InvoiceCreatedEvent, InvoicePaidEvent, PaymentReceivedEvent, etc.
- **Sales:** OrderPlacedEvent, PaymentCompletedEvent, OrderFulfilledEvent, etc.
- **Notifications:** NotificationCreatedEvent
- **Metering:** QuotaThresholdReachedEvent, UsageFlushedEvent
- **And more...**

### Cross-Module Query Services

Modules expose read-only interfaces implemented in their Infrastructure layer:
- `IUserQueryService` (Identity)
- `IInvoiceQueryService`, `ISubscriptionQueryService`, `IRevenueReportService` (Billing)
- `IOrderQueryService`, `IOrderReportService` (Sales)
- `IMeteringQueryService` (Metering)

### Real-time Messaging

- `RealtimeEnvelope` - Module-specific SignalR message wrapper
- SignalR methods: `ReceiveBilling`, `ReceiveNotifications`, etc.
- Modules remain decoupled from SignalR implementation

## Conventions

### Event Design Rules
1. **Past tense naming:** `InvoiceCreatedEvent`, not `CreateInvoiceEvent`
2. **Primitive types only:** No domain entities or value objects (serialization-friendly)
3. **Include context:** TenantId, UserId, EntityId, EntityName for downstream handlers
4. **Immutable records:** Events are facts, never modified

### Service Interface Rules
1. **Read-only:** Query services should NOT mutate state
2. **DTOs only:** Return data transfer objects, not domain entities
3. **Async:** All methods return `Task<T>`

## Dependencies

**NuGet Packages:**
- None (intentionally zero dependencies)

**Internal:**
- None (this is the contract layer)

## Usage Example

### Publishing Events (from module)
```csharp
// In domain event handler
public class InvoiceCreatedDomainEventHandler
{
    private readonly IMessageBus _bus;

    public async Task Handle(InvoiceCreatedDomainEvent domainEvent)
    {
        await _bus.PublishAsync(new InvoiceCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            InvoiceId = domainEvent.InvoiceId,
            TenantId = domainEvent.TenantId,
            UserId = domainEvent.UserId,
            Total = domainEvent.Total
        });
    }
}
```

### Consuming Events (in another module)
```csharp
public class InvoiceCreatedNotificationHandler
{
    public async Task HandleAsync(InvoiceCreatedEvent evt, NotificationService service)
    {
        await service.NotifyAsync(evt.UserId, $"Invoice created for {evt.Total}");
    }
}
```

## Module Communication Flow

```
┌─────────────┐      Domain Event      ┌──────────────────┐
│   Billing   │ ──────────────────────> │ Event Handler    │
│   Module    │                         │ (same module)    │
└─────────────┘                         └──────────────────┘
                                                 │
                                                 │ Publish
                                                 ▼
                                        ┌──────────────────┐
                                        │ RabbitMQ         │
                                        │ (Wolverine)      │
                                        └──────────────────┘
                                                 │
                                                 │ Subscribe
                                                 ▼
┌──────────────────┐  Integration Event  ┌──────────────────┐
│  Communications  │ <────────────────── │ Consumer         │
│     Module       │                     │ (Communications) │
└──────────────────┘                     └──────────────────┘
```

## NuGet Potential

**High** - This package is ready for extraction as a NuGet package for microservice implementations of Foundry modules.
