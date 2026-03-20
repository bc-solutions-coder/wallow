# Module Creation Guide

## Module Registration

Modules are registered using standard .NET extension methods. Each module provides `AddXxxModule()` and `InitializeXxxModuleAsync()` methods in its Infrastructure layer.

**Program.cs** calls into a central registry:
```csharp
// Service registration
WallowModules.AddWallowModules(builder.Services, builder.Configuration);

// Wolverine with automatic handler discovery
builder.Host.UseWolverine(opts =>
{
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.GetName().Name?.StartsWith("Wallow.") == true))
    {
        opts.Discovery.IncludeAssembly(assembly);
    }

    opts.UseRabbitMq(...)
        .AutoProvision()
        .UseConventionalRouting();
});

// Module initialization (runs migrations)
await WallowModules.InitializeWallowModulesAsync(app);
```

**WallowModules.cs** (`src/Wallow.Api/WallowModules.cs`) explicitly lists all modules, gated by feature flags:
```csharp
internal static class WallowModules
{
    public static IServiceCollection AddWallowModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddFeatureManagement();
        ServiceProvider tempProvider = services.BuildServiceProvider();
        IFeatureManager featureManager = tempProvider.GetRequiredService<IFeatureManager>();

        if (featureManager.IsEnabledAsync("Modules.Identity").GetAwaiter().GetResult())
            services.AddIdentityModule(configuration);

        if (featureManager.IsEnabledAsync("Modules.Billing").GetAwaiter().GetResult())
            services.AddBillingModule(configuration);

        if (featureManager.IsEnabledAsync("Modules.Notifications").GetAwaiter().GetResult())
            services.AddNotificationsModule(configuration);

        if (featureManager.IsEnabledAsync("Modules.Messaging").GetAwaiter().GetResult())
            services.AddMessagingModule(configuration);

        if (featureManager.IsEnabledAsync("Modules.Announcements").GetAwaiter().GetResult())
            services.AddAnnouncementsModule(configuration);

        if (featureManager.IsEnabledAsync("Modules.Storage").GetAwaiter().GetResult())
            services.AddStorageModule(configuration);

        if (featureManager.IsEnabledAsync("Modules.Inquiries").GetAwaiter().GetResult())
            services.AddInquiriesModule(configuration);

        services.AddWallowPlugins(configuration);
        return services;
    }

    public static async Task InitializeWallowModulesAsync(this WebApplication app)
    {
        IFeatureManager featureManager = app.Services.GetRequiredService<IFeatureManager>();

        if (await featureManager.IsEnabledAsync("Modules.Identity"))
            await app.InitializeIdentityModuleAsync();

        if (await featureManager.IsEnabledAsync("Modules.Billing"))
            await app.InitializeBillingModuleAsync();

        if (await featureManager.IsEnabledAsync("Modules.Notifications"))
            await app.InitializeNotificationsModuleAsync();

        if (await featureManager.IsEnabledAsync("Modules.Messaging"))
            await app.InitializeMessagingModuleAsync();

        if (await featureManager.IsEnabledAsync("Modules.Announcements"))
            await app.InitializeAnnouncementsModuleAsync();

        if (await featureManager.IsEnabledAsync("Modules.Storage"))
            await app.InitializeStorageModuleAsync();

        if (await featureManager.IsEnabledAsync("Modules.Inquiries"))
            await app.InitializeInquiriesModuleAsync();

        await app.InitializeWallowPluginsAsync();
    }
}
```

## Creating a New Module

1. Create four projects:
   - `Wallow.{Module}.Domain`
   - `Wallow.{Module}.Application`
   - `Wallow.{Module}.Infrastructure`
   - `Wallow.{Module}.Api`

2. Create module extension methods in Infrastructure:

```csharp
// src/Modules/{Module}/Wallow.{Module}.Infrastructure/Extensions/{Module}ModuleExtensions.cs
public static class {Module}ModuleExtensions
{
    public static IServiceCollection Add{Module}Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Add{Module}Application();
        services.Add{Module}Infrastructure(configuration);
        return services;
    }

    public static async Task<WebApplication> Initialize{Module}ModuleAsync(
        this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        {Module}DbContext db = scope.ServiceProvider.GetRequiredService<{Module}DbContext>();
        await db.Database.MigrateAsync();
        return app;
    }
}
```

3. Register the module in `src/Wallow.Api/WallowModules.cs`:

```csharp
using Wallow.{Module}.Infrastructure.Extensions;

// In AddWallowModules():
services.Add{Module}Module(configuration);

// In InitializeWallowModulesAsync():
await app.Initialize{Module}ModuleAsync();
```

## Module Types

| Type | AddModule | InitializeModule | Notes |
|------|-----------|------------------|-------|
| **Standard** | Registers services, DbContext | Runs EF migrations | Most modules |
| **Stateless** | Registers services only | No-op or omit | No database |

## Handler Discovery

Wolverine automatically discovers handlers in all `Wallow.*` assemblies. No manual registration needed. Create handlers following Wolverine conventions:

```csharp
public static class CreateInvoiceHandler
{
    public static async Task<Result<InvoiceDto>> HandleAsync(
        CreateInvoiceCommand command, IInvoiceRepository repo, CancellationToken ct)
    {
        // Implementation
    }
}
```

## RabbitMQ Routing

Wolverine's `UseConventionalRouting()` automatically creates queues and exchanges. No manual `ConfigureMessaging` required.

## SMS Channel Patterns

The Notifications module provides a reusable SMS channel at `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/`. Use this as the canonical reference when adding SMS capabilities.

**Domain layer** (`Domain/Channels/Sms/`):

- `SmsMessage` -- Aggregate root (`AggregateRoot<SmsMessageId>, ITenantScoped`) with lifecycle: `Create()` -> `MarkAsSent()` / `MarkAsFailed()` -> optional `ResetForRetry()`. Raises `SmsSentDomainEvent` and `SmsFailedDomainEvent`. Enforces a 1600-character body limit.
- `PhoneNumber` -- Value object with E.164 validation via `[GeneratedRegex]`. Factory method `PhoneNumber.Create(string)` throws `InvalidPhoneNumberException` on invalid input.
- `SmsStatus` -- Enum: `Pending = 0`, `Sent = 1`, `Failed = 2`.

**Application layer** (`Application/Channels/Sms/`):

- `ISmsProvider` -- Provider interface returning `SmsDeliveryResult(bool Success, string? MessageSid, string? ErrorMessage)`. Infrastructure implements this (e.g., `TwilioSmsProvider`, `NullSmsProvider`).
- `ISmsMessageRepository` -- Standard repository with `Add()` and `SaveChangesAsync()`.
- `SendSmsCommand` / `SendSmsHandler` -- Internal command that creates the `SmsMessage` aggregate, calls `ISmsProvider.SendAsync()`, and updates status.
- `SendSmsRequestedEventHandler` -- Consumes the cross-module `SendSmsRequestedEvent` from `Shared.Contracts` and dispatches `SendSmsCommand` via Wolverine's `IMessageBus`.

**Cross-module triggering via Shared.Contracts** (`Shared.Contracts/Notifications/Sms/Events/`):

Other modules publish `SendSmsRequestedEvent` to request SMS delivery without referencing the Communications module:

```csharp
// In Shared.Contracts
public sealed record SendSmsRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string To { get; init; }      // E.164 format
    public required string Body { get; init; }
    public string? SourceModule { get; init; }
    public Guid? CorrelationId { get; init; }
}

// Publishing from another module
await bus.PublishAsync(new SendSmsRequestedEvent
{
    TenantId = tenantId,
    To = "+15551234567",
    Body = "Your verification code is 123456",
    SourceModule = "Identity"
});
```

## Messaging Patterns

The Messaging module provides user-to-user messaging at `src/Modules/Messaging/Wallow.Messaging.Domain/Messaging/`. This supports both direct (1:1) and group conversations with inbox threading.

**Domain layer** (`Domain/Messaging/`):

- `Conversation` -- Aggregate root (`AggregateRoot<ConversationId>, ITenantScoped`) that owns `Participant` and `Message` collections. Two factory methods: `CreateDirect(tenantId, initiatorId, recipientId)` and `CreateGroup(tenantId, creatorId, subject, memberIds)`. Raises `ConversationCreatedDomainEvent`, `MessageSentDomainEvent`, and `ParticipantAddedDomainEvent`.
- `Message` -- Entity (`Entity<MessageId>`) owned by `Conversation`. Created via `Conversation.SendMessage(senderId, body)`, which enforces that the sender is an active participant and the conversation is not archived.
- `Participant` -- Entity (`Entity<ParticipantId>`) tracking `UserId`, `JoinedAt`, `LastReadAt`, and `IsActive`. Supports `MarkRead()` and `Leave()`.
- `ConversationStatus` -- Enum: `Active = 0`, `Archived = 1`.
- `MessageStatus` -- Enum: `Sent = 0`, `Read = 1`.

**Application layer** (`Application/Messaging/`):

Commands:
- `CreateConversationCommand` / `CreateConversationHandler` -- Creates direct or group conversations.
- `SendMessageCommand` / `SendMessageHandler` -- Sends a message within an existing conversation.
- `MarkConversationReadCommand` / `MarkConversationReadHandler` -- Updates the participant's `LastReadAt`.

Queries (Dapper-based via `IMessagingQueryService`):
- `GetConversationsQuery` -- Paginated inbox listing for a user.
- `GetMessagesQuery` -- Cursor-based message history for a conversation.
- `GetUnreadConversationCountQuery` -- Count of conversations with unread messages.

Interfaces:
- `IConversationRepository` -- EF Core write repository (`Add`, `GetByIdAsync`, `SaveChangesAsync`).
- `IMessagingQueryService` -- Dapper read service for paginated queries.

**Integration events** (`Shared.Contracts/Messaging/Events/`):

```csharp
public sealed record ConversationCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }
    public required IReadOnlyList<Guid> ParticipantIds { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required Guid TenantId { get; init; }
}

public sealed record MessageSentIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }
    public required Guid MessageId { get; init; }
    public required Guid SenderId { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset SentAt { get; init; }
    public required Guid TenantId { get; init; }
}
```

Domain event handlers (`ConversationCreatedEventHandler`, `MessageSentEventHandler`) translate domain events into these integration events for cross-module consumption.

## Shared Infrastructure Capabilities

Cross-cutting concerns in `Shared.Infrastructure` available to all modules:

- **Audit.NET interceptor** (`Shared.Infrastructure/Auditing/`) -- EF Core `SaveChangesInterceptor` that captures entity change audits. Registered globally; modules opt in via their DbContext.
- **IJobScheduler / Hangfire** (`Shared.Infrastructure/BackgroundJobs/`) -- `IJobScheduler` abstraction over Hangfire for enqueuing, scheduling, and recurring background jobs. Modules depend on the interface; Hangfire implementation is wired at the composition root.
- **Elsa 3 Workflows** (`Shared.Infrastructure/Workflows/`) -- Elsa 3 workflow engine integration with `WorkflowActivityBase` for defining custom activities. Modules define workflows without directly depending on Elsa internals.
