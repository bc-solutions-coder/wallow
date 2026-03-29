# Messaging

Wallow uses **Wolverine** as its unified messaging infrastructure, serving as both CQRS mediator and in-memory message bus. Modules communicate through integration events published via Wolverine's in-memory transport. A PostgreSQL-backed durable outbox ensures reliable message delivery.

## Architecture

```
Controller / Handler
  |
  v  InvokeAsync (sync, in-process)
IMessageBus ---------> Handler (same process)
  |
  v  PublishAsync (async, in-memory)
Wolverine Outbox -----> Consumer Handler (same process)
```

| Component | Role |
|-----------|------|
| `IMessageBus` | Wolverine's interface for sending commands/queries and publishing events |
| PostgreSQL Outbox | Stores messages durably before delivery to ensure crash resilience |
| Handlers | Classes with `Handle` or `HandleAsync` methods that process messages |

## Wolverine Basics

### IMessageBus

Inject `IMessageBus` to send commands, execute queries, or publish events:

```csharp
public class InvoicesController(IMessageBus bus) : ControllerBase { }
```

### InvokeAsync for Commands/Queries

`InvokeAsync` executes the handler synchronously within the same process and returns a result:

```csharp
// Query
var result = await _bus.InvokeAsync<Result<InvoiceDto>>(
    new GetInvoiceByIdQuery(invoiceId), cancellationToken);

// Command
var result = await _bus.InvokeAsync<Result<InvoiceDto>>(
    new CreateInvoiceCommand(userId, invoiceNumber, currency, dueDate), cancellationToken);
```

Use `InvokeAsync` for queries, commands where you need the result immediately, and operations that must complete before responding to the HTTP request.

### PublishAsync for Events

`PublishAsync` is fire-and-forget from the caller's perspective. Use it to notify other modules about something that happened:

```csharp
await bus.PublishAsync(new InvoiceCreatedEvent
{
    InvoiceId = invoice.Id.Value,
    TenantId = invoice.TenantId.Value,
    UserId = userId,
    UserEmail = userEmail,
    InvoiceNumber = invoice.InvoiceNumber,
    Amount = invoice.TotalAmount,
    Currency = invoice.Currency,
    DueDate = invoice.DueDate
});
```

### Handler Discovery

Wolverine automatically discovers handlers in assemblies that start with `Wallow.`. No manual registration is required. This is configured in `Program.cs`:

```csharp
builder.Host.UseWolverine(opts =>
{
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.GetName().Name?.StartsWith("Wallow.", StringComparison.Ordinal) == true))
    {
        opts.Discovery.IncludeAssembly(assembly);
    }
});
```

## Handler Patterns

### Instance Handlers with Constructor Injection

```csharp
public sealed class GetInvoiceByIdHandler(IInvoiceRepository invoiceRepository)
{
    public async Task<Result<InvoiceDto>> Handle(
        GetInvoiceByIdQuery query,
        CancellationToken cancellationToken)
    {
        InvoiceId invoiceId = InvoiceId.Create(query.InvoiceId);
        Invoice? invoice = await invoiceRepository.GetByIdWithLineItemsAsync(invoiceId, cancellationToken);

        if (invoice is null)
            return Result.Failure<InvoiceDto>(Error.NotFound("Invoice", query.InvoiceId));

        return Result.Success(invoice.ToDto());
    }
}
```

### Static Method Handlers

For handlers that don't need class-level state:

```csharp
public sealed partial class InvoiceCreatedDomainEventHandler
{
    public static async Task HandleAsync(
        InvoiceCreatedDomainEvent domainEvent,
        IInvoiceRepository invoiceRepository,
        IMessageBus bus,
        ILogger<InvoiceCreatedDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        // Enrich with additional data, then publish integration event
        Invoice? invoice = await invoiceRepository.GetByIdAsync(
            InvoiceId.Create(domainEvent.InvoiceId), cancellationToken);

        await bus.PublishAsync(new Wallow.Shared.Contracts.Billing.Events.InvoiceCreatedEvent
        {
            InvoiceId = domainEvent.InvoiceId,
            TenantId = invoice?.TenantId.Value ?? Guid.Empty,
            UserId = domainEvent.UserId,
            UserEmail = string.Empty,
            InvoiceNumber = invoice?.InvoiceNumber ?? string.Empty,
            Amount = domainEvent.TotalAmount,
            Currency = domainEvent.Currency,
            DueDate = invoice?.DueDate ?? DateTime.UtcNow.AddDays(30)
        });
    }
}
```

### Handler Naming Conventions

| Convention | Example |
|------------|---------|
| Handler class name | `{Command/Query}Handler` or `{DomainEvent}Handler` |
| Handler method name | `Handle` or `HandleAsync` |
| Message types | Records ending with `Command`, `Query`, or `Event` |

## Durable Outbox

### PostgreSQL Persistence

Wolverine uses PostgreSQL to store messages durably. This ensures messages survive application crashes. Configuration in `Program.cs`:

```csharp
// PostgreSQL persistence for durable outbox/inbox (disabled in Testing)
if (!builder.Environment.IsEnvironment("Testing"))
{
    string pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Database connection string not configured");
    opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");
}
```

When you call `PublishAsync`, the message is first written to the PostgreSQL `wolverine` schema. A background worker delivers the message to consumers. If the app crashes, undelivered messages are resent on startup.

Both durable inbox and outbox are enabled on all endpoints (except in Testing):

```csharp
if (!builder.Environment.IsEnvironment("Testing"))
{
    opts.Policies.UseDurableInboxOnAllListeners();
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
}
```

### Wolverine Schema

| Table | Purpose |
|-------|---------|
| `wolverine.incoming_envelopes` | Messages received but not yet processed |
| `wolverine.outgoing_envelopes` | Messages to be sent (outbox) |
| `wolverine.dead_letter_queue` | Failed messages after all retries |

## Error Handling

Standard error handling is configured via `ConfigureStandardErrorHandling` in `WolverineErrorHandlingExtensions` (`src/Shared/Wallow.Shared.Infrastructure.Core/Messaging/WolverineErrorHandlingExtensions.cs`):

- **TimeoutException**: Retry with exponential backoff (50ms, 100ms, 250ms), then move to dead letter queue
- **InvalidOperationException**: Retry twice, then move to dead letter queue
- **All other exceptions**: Retry once, then move to dead letter queue

Failed messages are stored in the `wolverine.dead_letter_queue` PostgreSQL table.

Let exceptions bubble up from handlers -- Wolverine's error handling policies manage retries. Use `Result<T>` for business validation failures rather than throwing exceptions.

## Integration Events

### Defined in Shared.Contracts

Integration events are the public contract for cross-module communication. They live in `src/Shared/Wallow.Shared.Contracts/{Module}/Events/`:

```csharp
// src/Shared/Wallow.Shared.Contracts/Billing/Events/InvoiceCreatedEvent.cs
public sealed record InvoiceCreatedEvent : IntegrationEvent
{
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string UserEmail { get; init; }
    public required string InvoiceNumber { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTime DueDate { get; init; }
}
```

### Base Types

All integration events extend `IntegrationEvent` (in `src/Shared/Wallow.Shared.Contracts/IIntegrationEvent.cs`), which provides `EventId` and `OccurredAt` fields.

### Event Catalog

Wallow auto-generates an event catalog from the codebase. In development, visit `http://localhost:5000/asyncapi` to browse all integration events, their schemas, and which modules publish/consume them. See the [AsyncAPI Guide](../integrations/asyncapi.md) for details.

### Conventions

**Past-tense naming** -- events describe what happened:

| Correct | Incorrect |
|---------|-----------|
| `InvoiceCreatedEvent` | `CreateInvoiceEvent` |
| `UserRegisteredEvent` | `RegisterUserEvent` |
| `PaymentReceivedEvent` | `ProcessPaymentEvent` |

**Use primitives, not strongly-typed IDs** -- integration events use plain `Guid` for IDs to simplify serialization across module boundaries.

### Publishing from Domain Event Handlers

The common pattern bridges domain events to integration events. Domain event handlers in the Application layer enrich the event with additional data and publish the corresponding integration event via `IMessageBus.PublishAsync`.

## Quick Reference

### Adding a New Command

1. Create a command record
2. Create a handler with a `Handle` or `HandleAsync` method returning `Result<T>`
3. Optionally add a `FluentValidation` validator

### Adding a New Integration Event

1. Add the event record to `src/Shared/Wallow.Shared.Contracts/{Module}/Events/`, extending `IntegrationEvent`
2. Publish from a handler via `await bus.PublishAsync(new MyEvent { ... })`
3. Create a consumer handler in the target module

### Key Rules

- Never add business logic to controllers -- delegate to Wolverine handlers via `IMessageBus`
- Use `InvokeAsync<T>` for synchronous request/response within the same process
- Use `PublishAsync` for fire-and-forget events consumed by other modules
- Handlers return `Result<T>` -- controllers map results to HTTP responses

## Troubleshooting

### Handler Not Being Discovered

**Checklist:**

1. Ensure the handler assembly starts with `Wallow.` and is loaded at startup
2. Handler method signature must follow Wolverine conventions (`Handle` or `HandleAsync`)
3. Handler class must be `public` (not `internal`)

### Checking Dead Letter Queue

```sql
SELECT * FROM wolverine.dead_letter_queue
ORDER BY received_at DESC
LIMIT 10;
```

### Logging

Message logging is enabled via `ConfigureMessageLogging()` in `Program.cs`. Set the Wolverine log level to `Debug` in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Wolverine": "Debug"
    }
  }
}
```

### Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| "No handler for message type X" | Handler not discovered | Check assembly naming and handler visibility |
| "Message rejected" | Handler threw exception | Check logs, review error handling |
