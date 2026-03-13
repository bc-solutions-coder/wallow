# Foundry Messaging Guide

This guide covers the messaging infrastructure in Foundry, including Wolverine as the unified CQRS mediator and message bus, RabbitMQ for inter-module async messaging, and PostgreSQL durable outbox for reliability.

## Table of Contents

1. [Overview](#1-overview)
2. [Wolverine Basics](#2-wolverine-basics)
3. [Handler Patterns](#3-handler-patterns)
4. [RabbitMQ Transport](#4-rabbitmq-transport)
5. [In-Memory Transport (Development/Testing)](#5-in-memory-transport-developmenttesting)
6. [Durable Outbox](#6-durable-outbox)
7. [Error Handling](#7-error-handling)
8. [Integration Events](#8-integration-events)
9. [Switching Between RabbitMQ and In-Memory](#9-switching-between-rabbitmq-and-in-memory)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Overview

Foundry uses **Wolverine** as its unified messaging infrastructure, serving three primary roles:

1. **CQRS Mediator** - Routes commands and queries to their handlers within the same process
2. **Message Bus** - Publishes events asynchronously for cross-module communication
3. **Durable Outbox** - Ensures reliable message delivery with PostgreSQL persistence

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Controller                                                      │
│    │                                                             │
│    ▼ InvokeAsync (sync)                                          │
│  IMessageBus ──────────────► Handler (same process)              │
│    │                                                             │
│    ▼ PublishAsync (async)                                        │
│  Wolverine Outbox ──────────► RabbitMQ ──────────► Consumer      │
└─────────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Role |
|-----------|------|
| `IMessageBus` | Wolverine's interface for sending commands/queries and publishing events |
| RabbitMQ | Message broker for durable, cross-module async messaging |
| PostgreSQL Outbox | Stores messages durably before sending to RabbitMQ |
| Handlers | Classes with `Handle` or `HandleAsync` methods that process messages |

---

## 2. Wolverine Basics

### IMessageBus Injection

Inject `IMessageBus` into controllers, services, or handlers to send commands, execute queries, or publish events:

```csharp
using Wolverine;

public class InvoicesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public InvoicesController(IMessageBus bus)
    {
        _bus = bus;
    }
}
```

### InvokeAsync for Commands/Queries (Synchronous, In-Process)

Use `InvokeAsync` when you need an immediate response. This executes the handler synchronously within the same process:

```csharp
// Query - returns data
var result = await _bus.InvokeAsync<Result<InvoiceDto>>(
    new GetInvoiceByIdQuery(invoiceId),
    cancellationToken);

// Command - returns result
var result = await _bus.InvokeAsync<Result<InvoiceDto>>(
    new CreateInvoiceCommand(userId, invoiceNumber, currency, dueDate),
    cancellationToken);

// Command - returns void result
var result = await _bus.InvokeAsync<Result>(
    new CancelInvoiceCommand(invoiceId, userId),
    cancellationToken);
```

**When to use `InvokeAsync`:**
- Queries that return data
- Commands where you need the result immediately
- Operations that must complete before responding to the HTTP request

### PublishAsync for Events (Asynchronous, Cross-Module)

Use `PublishAsync` when you want to notify other modules about something that happened. This is fire-and-forget from the caller's perspective:

```csharp
// Publish an integration event to RabbitMQ
await bus.PublishAsync(new InvoiceCreatedEvent
{
    InvoiceId = invoice.Id.Value,
    UserId = userId,
    UserEmail = userEmail,
    InvoiceNumber = invoice.InvoiceNumber,
    Amount = invoice.TotalAmount,
    Currency = invoice.Currency,
    DueDate = invoice.DueDate
});
```

**When to use `PublishAsync`:**
- Notifying other modules about domain events
- Triggering side effects that don't need immediate completion
- Broadcasting information that multiple consumers might care about

### Handler Discovery by Convention

Wolverine automatically discovers handlers in assemblies that start with `Foundry.`. No manual registration is required:

```csharp
// Program.cs
builder.Host.UseWolverine(opts =>
{
    // Discover handlers in all Foundry assemblies
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.GetName().Name?.StartsWith("Foundry.") == true))
    {
        opts.Discovery.IncludeAssembly(assembly);
    }
});
```

---

## 3. Handler Patterns

### Static Method Handlers (Preferred)

Wolverine's preferred pattern uses static methods with parameters injected by the DI container:

```csharp
public sealed class GetInvoiceByIdHandler(IInvoiceRepository invoiceRepository)
{
    public async Task<Result<InvoiceDto>> Handle(
        GetInvoiceByIdQuery query,
        CancellationToken cancellationToken)
    {
        var invoiceId = InvoiceId.Create(query.InvoiceId);
        var invoice = await invoiceRepository.GetByIdWithLineItemsAsync(invoiceId, cancellationToken);

        if (invoice is null)
            return Result.Failure<InvoiceDto>(Error.NotFound("Invoice", query.InvoiceId));

        return Result.Success(invoice.ToDto());
    }
}
```

### Static Method Handlers with Full Injection

For handlers that don't need class-level state, use fully static methods:

```csharp
public sealed class InvoiceCreatedDomainEventHandler
{
    public static async Task HandleAsync(
        InvoiceCreatedDomainEvent domainEvent,
        IInvoiceRepository invoiceRepository,
        IMessageBus bus,
        ILogger<InvoiceCreatedDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling InvoiceCreatedDomainEvent for Invoice {InvoiceId}",
            domainEvent.InvoiceId);

        // Enrich with additional data
        var invoice = await invoiceRepository.GetByIdAsync(
            InvoiceId.Create(domainEvent.InvoiceId), cancellationToken);

        // Publish integration event for other modules
        await bus.PublishAsync(new InvoiceCreatedEvent
        {
            InvoiceId = domainEvent.InvoiceId,
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

### Instance Handlers with Constructor Injection

When you need instance-level services or state:

```csharp
public sealed class CreateInvoiceHandler(
    IInvoiceRepository invoiceRepository,
    IMessageBus messageBus)
{
    public async Task<Result<InvoiceDto>> Handle(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var invoice = Invoice.Create(
            command.UserId,
            command.InvoiceNumber,
            command.Currency,
            command.UserId,
            command.DueDate);

        invoiceRepository.Add(invoice);
        await invoiceRepository.SaveChangesAsync(cancellationToken);

        await messageBus.PublishAsync(new AuditEntryRequestedEvent
        {
            Action = "Invoice.Created",
            EntityType = "Invoice",
            EntityId = invoice.Id.Value.ToString()
        });

        return Result.Success(invoice.ToDto());
    }
}
```

### Handler Naming Conventions

| Convention | Example |
|------------|---------|
| Handler class name | `{Command/Query}Handler` - e.g., `CreateInvoiceHandler`, `GetInvoiceByIdHandler` |
| Handler method name | `Handle` or `HandleAsync` |
| Message types | Records ending with `Command`, `Query`, or `Event` |
| Integration event handlers | `{EventName}Handler` - e.g., `UserRegisteredEventHandler` |
| Domain event handlers | `{DomainEventName}Handler` - e.g., `InvoiceCreatedDomainEventHandler` |

### Handler Example from Codebase

Here is a complete handler example from the Communications module (in-app notifications channel):

```csharp
// src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/EventHandlers/UserRegisteredEventHandler.cs
public sealed partial class UserRegisteredEventHandler
{
    public static async Task HandleAsync(
        UserRegisteredEvent integrationEvent,
        INotificationRepository notificationRepository,
        INotificationService notificationService,
        ITenantContext tenantContext,
        ILogger<UserRegisteredEventHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling UserRegisteredEvent for User {UserId}",
            integrationEvent.UserId);

        var title = "Welcome to Foundry!";
        var message = $"Hi {integrationEvent.FirstName}, welcome to Foundry!";

        Notification notification = Notification.Create(
            tenantContext.TenantId,
            integrationEvent.UserId,
            NotificationType.SystemAlert,
            title,
            message);

        notificationRepository.Add(notification);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        await notificationService.SendToUserAsync(
            integrationEvent.UserId,
            title,
            message,
            NotificationType.SystemAlert.ToString(),
            cancellationToken);

        logger.LogInformation(
            "Welcome notification created for User {UserId}",
            integrationEvent.UserId);
    }
}
```

---

## 4. RabbitMQ Transport

### Configuration in Program.cs

RabbitMQ is enabled by setting `ModuleMessaging:Transport` to `"RabbitMq"`:

```csharp
builder.Host.UseWolverine(opts =>
{
    string transport = builder.Configuration.GetValue<string>("ModuleMessaging:Transport") ?? "InMemory";

    if (transport.Equals("RabbitMq", StringComparison.OrdinalIgnoreCase))
    {
        string rabbitMqConnection = builder.Configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException(
                "RabbitMq connection string is required when ModuleMessaging:Transport is 'RabbitMq'");

        RabbitMqTransportExpression rabbitMq = opts.UseRabbitMq(new Uri(rabbitMqConnection))
            .AutoProvision()          // Auto-create queues/exchanges
            .UseConventionalRouting(); // Automatic routing based on message types

        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        {
            rabbitMq.AutoPurgeOnStartup(); // Clear queues on startup (dev only)
        }
    }
});
```

### UseConventionalRouting for Automatic Queue/Exchange Creation

`UseConventionalRouting()` automatically creates:
- An exchange for each message type (named after the message type)
- A queue for each handler (named after the handler type)
- Bindings between exchanges and queues

This means you don't need to manually configure routing for most cases.

### Explicit Routing with PublishMessage().ToRabbitExchange()

For cases where you need explicit control over routing:

```csharp
opts.PublishMessage<InvoiceCreatedEvent>()
    .ToRabbitExchange("billing-events");

opts.PublishMessage<UserRegisteredEvent>()
    .ToRabbitExchange("identity-events");
```

### Listening with ListenToRabbitQueue()

To listen on a specific queue:

```csharp
opts.ListenToRabbitQueue("billing-inbox");
opts.ListenToRabbitQueue("communications-inbox");

// In Testing environment, a test queue is declared
if (builder.Environment.IsEnvironment("Testing"))
{
    rabbitMq.DeclareQueue("test-inbox");
    opts.ListenToRabbitQueue("test-inbox");
}
```

### AutoProvision and AutoPurgeOnStartup

| Option | Purpose |
|--------|---------|
| `AutoProvision()` | Automatically creates exchanges, queues, and bindings on startup |
| `AutoPurgeOnStartup()` | Clears all queues on startup (development/testing only) |

---

## 5. In-Memory Transport (Development/Testing)

### How to Use In-Memory Transport

Transport selection is controlled by the `ModuleMessaging:Transport` configuration key. Set it to `"InMemory"` (or omit it, as `"InMemory"` is the default) to use in-memory transport instead of RabbitMQ:

```csharp
// Program.cs — transport switching logic
string transport = builder.Configuration.GetValue<string>("ModuleMessaging:Transport") ?? "InMemory";

if (transport.Equals("RabbitMq", StringComparison.OrdinalIgnoreCase))
{
    // Use RabbitMQ transport
    opts.UseRabbitMq(new Uri(rabbitMqConnection))
        .AutoProvision()
        .UseConventionalRouting();
}
// Otherwise: in-memory transport (Wolverine default)
```

### When to Use In-Memory vs RabbitMQ

| Transport | Use Case |
|-----------|----------|
| **In-Memory** | Unit tests, simple development scenarios, when RabbitMQ is unavailable |
| **RabbitMQ** | Integration tests, staging, production, multi-instance deployments |

### Configuration Approach: ModuleMessaging:Transport

**With RabbitMQ** (`appsettings.Development.json`):
```json
{
  "ModuleMessaging": {
    "Transport": "RabbitMq"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=foundry;...",
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  }
}
```

**With in-memory transport** (default):
```json
{
  "ModuleMessaging": {
    "Transport": "InMemory"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=foundry;..."
  }
}
```

### Testing Environment Behavior

The Testing environment has special handling:

```csharp
// PostgreSQL persistence is disabled in Testing to prevent polling after containers disposed
if (!builder.Environment.IsEnvironment("Testing"))
{
    opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");
}

// In Testing, queues are purged on startup
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    rabbitMq.AutoPurgeOnStartup();
}

// Durable outbox is disabled in Testing
if (!builder.Environment.IsEnvironment("Testing"))
{
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
}
```

---

## 6. Durable Outbox

### PostgreSQL Persistence for Reliability

Wolverine uses PostgreSQL to store messages durably before sending to RabbitMQ. This ensures messages survive application crashes:

```csharp
builder.Host.UseWolverine(opts =>
{
    // PostgreSQL persistence for durable outbox/inbox
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not configured");
        opts.PersistMessagesWithPostgresql(pgConnectionString, "wolverine");
    }
});
```

### How Messages Survive Crashes

1. When you call `PublishAsync`, the message is first written to the PostgreSQL `wolverine` schema
2. A background worker picks up the message and sends it to RabbitMQ
3. Only after successful delivery is the message marked as sent
4. If the app crashes, undelivered messages are resent on startup

### DurableOutboxOnAllSendingEndpoints Policy

This policy ensures all outgoing messages go through the durable outbox:

```csharp
// Durable outbox on all sending endpoints (skip in Testing environment)
if (!builder.Environment.IsEnvironment("Testing"))
{
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
}
```

### Wolverine Schema

Wolverine creates tables in the `wolverine` schema:

| Table | Purpose |
|-------|---------|
| `wolverine.incoming_envelopes` | Messages received but not yet processed |
| `wolverine.outgoing_envelopes` | Messages to be sent (outbox) |
| `wolverine.dead_letter_queue` | Failed messages after all retries |

---

## 7. Error Handling

### Standard Retry Policies via ConfigureStandardErrorHandling

Foundry configures standard error handling in `WolverineErrorHandlingExtensions`:

```csharp
// src/Shared/Foundry.Shared.Kernel/Messaging/WolverineErrorHandlingExtensions.cs
public static void ConfigureStandardErrorHandling(this WolverineOptions opts)
{
    // Timeout exceptions - exponential backoff, then DLQ
    opts.Policies.OnException<TimeoutException>()
        .RetryWithCooldown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250))
        .Then.MoveToErrorQueue();

    // Invalid operation - retry twice, then DLQ
    opts.Policies.OnException<InvalidOperationException>()
        .RetryTimes(2)
        .Then.MoveToErrorQueue();

    // All other exceptions - retry once, then DLQ
    opts.Policies.OnAnyException()
        .RetryTimes(1)
        .Then.MoveToErrorQueue();
}
```

### Dead Letter Queues

Messages that fail all retry attempts are moved to the dead letter queue:
- **PostgreSQL**: Stored in `wolverine.dead_letter_queue` table
- **RabbitMQ**: Moved to `{queue-name}.errors` queue

### Exception Handling in Handlers

Let exceptions bubble up - Wolverine's error handling policies will manage retries. Use `Result<T>` for business validation failures:

```csharp
public async Task<Result<InvoiceDto>> Handle(
    CreateInvoiceCommand command,
    CancellationToken cancellationToken)
{
    // Business validation - return Result.Failure instead of throwing
    var exists = await invoiceRepository.ExistsByInvoiceNumberAsync(
        command.InvoiceNumber, cancellationToken);

    if (exists)
    {
        return Result.Failure<InvoiceDto>(
            Error.Conflict($"Invoice '{command.InvoiceNumber}' already exists"));
    }

    // Infrastructure exceptions (DB connection, etc.) will be caught by error policies
    var invoice = Invoice.Create(...);
    invoiceRepository.Add(invoice);
    await invoiceRepository.SaveChangesAsync(cancellationToken);

    return Result.Success(invoice.ToDto());
}
```

---

## 8. Integration Events

### Defined in Shared.Contracts

Integration events are the public contract for cross-module communication. They live in `Foundry.Shared.Contracts`:

```csharp
// src/Shared/Foundry.Shared.Contracts/Billing/Events/InvoiceCreatedEvent.cs
namespace Foundry.Shared.Contracts.Billing.Events;

/// <summary>
/// Published when an invoice is created.
/// Consumers: Communications (send email, notify user)
/// </summary>
public sealed record InvoiceCreatedEvent : IntegrationEvent
{
    public required Guid InvoiceId { get; init; }
    public required Guid UserId { get; init; }
    public required string UserEmail { get; init; }
    public required string InvoiceNumber { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTime DueDate { get; init; }
}
```

### Base Types

```csharp
// src/Shared/Foundry.Shared.Contracts/IIntegrationEvent.cs
namespace Foundry.Shared.Contracts;

/// <summary>
/// Marker interface for integration events.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base record for integration events with default implementations.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
```

### Browsing All Events

Foundry auto-generates an event catalog from the codebase. In development, visit `http://localhost:5000/asyncapi` to browse all integration events, their schemas, and which modules publish/consume them. See the [AsyncAPI Guide](ASYNCAPI_GUIDE.md) for details.

### Past-Tense Naming

Events describe what happened, not what should happen:

| Correct | Incorrect |
|---------|-----------|
| `InvoiceCreatedEvent` | `CreateInvoiceEvent` |
| `UserRegisteredEvent` | `RegisterUserEvent` |
| `PaymentReceivedEvent` | `ProcessPaymentEvent` |

### Use Primitives, Not Strongly-Typed IDs

Integration events use plain `Guid` for IDs, not domain strongly-typed IDs. This simplifies serialization across module boundaries:

```csharp
// Correct - use primitives
public required Guid InvoiceId { get; init; }
public required Guid UserId { get; init; }
public required string Currency { get; init; }
public required decimal Amount { get; init; }

// Incorrect - don't use domain types
// public required InvoiceId InvoiceId { get; init; }  // Wrong!
// public required Money Amount { get; init; }         // Wrong!
```

### Publishing from Domain Event Handlers

The common pattern is to bridge domain events to integration events:

```csharp
// src/Modules/Billing/Foundry.Billing.Application/EventHandlers/InvoiceCreatedDomainEventHandler.cs
public sealed class InvoiceCreatedDomainEventHandler
{
    public static async Task HandleAsync(
        InvoiceCreatedDomainEvent domainEvent,  // Internal domain event
        IInvoiceRepository invoiceRepository,
        IMessageBus bus,
        ILogger<InvoiceCreatedDomainEventHandler> logger,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Handling InvoiceCreatedDomainEvent for Invoice {InvoiceId}",
            domainEvent.InvoiceId);

        // Enrich with additional data
        var invoice = await invoiceRepository.GetByIdAsync(
            InvoiceId.Create(domainEvent.InvoiceId), cancellationToken);

        // Publish integration event for other modules
        await bus.PublishAsync(new InvoiceCreatedEvent  // Public integration event
        {
            InvoiceId = domainEvent.InvoiceId,
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

---

## 9. Switching Between RabbitMQ and In-Memory

### Configuration-Based Switching

The transport is selected based on the `ModuleMessaging:Transport` configuration key (`"InMemory"` or `"RabbitMq"`). `"InMemory"` is the default when the key is absent:

```csharp
// Program.cs — transport switching logic
string transport = builder.Configuration.GetValue<string>("ModuleMessaging:Transport") ?? "InMemory";

if (transport.Equals("RabbitMq", StringComparison.OrdinalIgnoreCase))
{
    // Use RabbitMQ
    opts.UseRabbitMq(new Uri(rabbitMqConnection))
        .AutoProvision()
        .UseConventionalRouting();
}
// Otherwise: in-memory transport (Wolverine default)
```

### Development: Set ModuleMessaging:Transport to Switch

**With RabbitMQ** (`appsettings.Development.json`):
```json
{
  "ModuleMessaging": {
    "Transport": "RabbitMq"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=foundry;Username=postgres;Password=postgres",
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  }
}
```

**With in-memory transport** (default — omit key or set to `"InMemory"`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=foundry;Username=postgres;Password=postgres"
  }
}
```

### Production: Always Use RabbitMQ for Durability

**Production configuration** should always use RabbitMQ transport:

```json
{
  "ModuleMessaging": {
    "Transport": "RabbitMq"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.example.com;Database=foundry;...",
    "RabbitMq": "amqps://user:pass@rabbitmq.example.com:5671"
  }
}
```

### Implications of Each Approach

| Aspect | In-Memory | RabbitMQ |
|--------|-----------|----------|
| **Message persistence** | Messages lost on crash | Messages survive crashes |
| **Multi-instance** | Not supported | Full support |
| **Message ordering** | Guaranteed within process | Per-queue ordering |
| **Visibility** | No management UI | RabbitMQ Management UI |
| **Performance** | Faster (no network) | Network overhead |
| **Use case** | Dev, unit tests | Integration tests, staging, production |

---

## 10. Troubleshooting

### Checking RabbitMQ Management UI

Access the RabbitMQ management UI at `http://localhost:15672` (default credentials: `guest` / `guest`).

**Key things to check:**
- **Connections**: Is your app connected?
- **Queues**: Are messages piling up?
- **Exchanges**: Are exchanges created?
- **Bindings**: Are queues bound to exchanges?

### Messages Stuck in Queues

**Symptoms:**
- Queue depth increasing
- No consumers visible
- Messages not being processed

**Common causes and solutions:**

1. **No consumer for the message type**
   - Check that a handler exists for the message type
   - Verify the handler assembly is included in discovery:
     ```csharp
     opts.Discovery.IncludeAssembly(typeof(YourHandler).Assembly);
     ```

2. **Handler throwing exceptions**
   - Check application logs for exceptions
   - Messages may be in retry or moved to DLQ

3. **Consumer not connected**
   - Check RabbitMQ connections in management UI
   - Verify connection string is correct

### Handler Not Being Discovered

**Symptoms:**
- `InvokeAsync` throws "No handler for message type"
- Events not being processed

**Checklist:**

1. **Assembly discovery**: Ensure the assembly is included
   ```csharp
   opts.Discovery.IncludeAssembly(typeof(YourHandler).Assembly);
   ```

2. **Handler method signature**: Must follow Wolverine conventions
   ```csharp
   // Correct
   public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct)

   // Also correct (static)
   public static async Task HandleAsync(TEvent event, IDependency dep, CancellationToken ct)
   ```

3. **Namespace**: Handler must be in a namespace that's being scanned

4. **Handler class**: Must be `public` (not `internal`)

### Checking Dead Letter Queue

Messages that failed all retries are in the dead letter queue:

**PostgreSQL:**
```sql
SELECT * FROM wolverine.dead_letter_queue
ORDER BY received_at DESC
LIMIT 10;
```

**RabbitMQ:**
Check the `.errors` queues in the management UI.

### Logging Message Processing

Enable message logging to see what's happening:

```csharp
// Already configured in Program.cs
opts.ConfigureMessageLogging(); // Logs message start at Debug level
```

Then set the log level in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Wolverine": "Debug"
    }
  }
}
```

### Common Error Messages

| Error | Cause | Solution |
|-------|-------|----------|
| "No handler for message type X" | Handler not discovered | Check assembly discovery |
| "Connection refused" | RabbitMQ not running | Start RabbitMQ or use in-memory |
| "Queue not found" | Queue not auto-provisioned | Enable `AutoProvision()` |
| "Message rejected" | Handler threw exception | Check logs, review error handling |

### Useful Commands

**Check Wolverine schema exists:**
```sql
SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'wolverine';
```

**Check outbox messages:**
```sql
SELECT * FROM wolverine.outgoing_envelopes ORDER BY created_at DESC LIMIT 10;
```

**Check incoming messages:**
```sql
SELECT * FROM wolverine.incoming_envelopes ORDER BY received_at DESC LIMIT 10;
```

**RabbitMQ CLI (if installed):**
```bash
# List queues
rabbitmqctl list_queues

# List connections
rabbitmqctl list_connections

# Purge a queue (careful!)
rabbitmqctl purge_queue queue_name
```

---

## Quick Reference

### Adding a New Command

1. Create command record in `Commands/{Action}/`:
   ```csharp
   public sealed record MyCommand(Guid Id, string Data);
   ```

2. Create handler:
   ```csharp
   public sealed class MyCommandHandler(IMyRepository repo)
   {
       public async Task<Result<MyDto>> Handle(MyCommand cmd, CancellationToken ct)
       {
           // Implementation
       }
   }
   ```

3. Create validator (optional):
   ```csharp
   public sealed class MyCommandValidator : AbstractValidator<MyCommand>
   {
       public MyCommandValidator()
       {
           RuleFor(x => x.Data).NotEmpty();
       }
   }
   ```

### Adding a New Integration Event

1. Add event to `Shared.Contracts/{Module}/Events/`:
   ```csharp
   public sealed record MyEvent : IntegrationEvent
   {
       public required Guid EntityId { get; init; }
       public required string Data { get; init; }
   }
   ```

2. Publish from handler:
   ```csharp
   await bus.PublishAsync(new MyEvent { EntityId = id, Data = data });
   ```

3. Create consumer in target module:
   ```csharp
   public sealed class MyEventHandler
   {
       public static async Task HandleAsync(MyEvent evt, IDependency dep, CancellationToken ct)
       {
           // Handle event
       }
   }
   ```

### Controller Integration

```csharp
[ApiController]
[Route("api/billing/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IMessageBus _bus;

    public InvoicesController(IMessageBus bus) => _bus = bus;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateInvoiceCommand(
            GetCurrentUserId(),
            request.InvoiceNumber,
            request.Currency,
            request.DueDate);

        var result = await _bus.InvokeAsync<Result<InvoiceDto>>(command, cancellationToken);

        return result.Map(ToInvoiceResponse)
            .ToCreatedResult($"/api/billing/invoices/{result.Value?.Id}");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _bus.InvokeAsync<Result<InvoiceDto>>(
            new GetInvoiceByIdQuery(id), cancellationToken);

        return result.Map(ToInvoiceResponse).ToActionResult();
    }
}
```

### Key Points

- **Never add business logic to controllers** - delegate to Wolverine immediately
- **Use `InvokeAsync<T>` for synchronous request/response** - waits for handler to complete
- **Use `PublishAsync` for fire-and-forget events** - returns immediately
- **Handlers return `Result<T>`** - controllers map to HTTP responses
