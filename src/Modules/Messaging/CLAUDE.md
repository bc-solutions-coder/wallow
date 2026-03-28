# Messaging Module -- Agent Guide

## Module Purpose

Tenant-scoped conversations and messaging. Supports direct (1:1) and group conversations with read tracking.

## Key File Locations

- **Aggregate root**: `Wallow.Messaging.Domain/Conversations/Entities/Conversation.cs`
- **Child entities**: `Message.cs`, `Participant.cs` (same directory)
- **Domain events**: `Wallow.Messaging.Domain/Conversations/Events/`
- **Commands & handlers**: `Wallow.Messaging.Application/Conversations/Commands/{CreateConversation,SendMessage,MarkConversationRead}/`
- **Queries & handlers**: `Wallow.Messaging.Application/Conversations/Queries/{GetConversations,GetMessages,GetUnreadConversationCount}/`
- **Event handlers (domain -> integration)**: `Wallow.Messaging.Application/Conversations/EventHandlers/`
- **Repository interface**: `Wallow.Messaging.Application/Conversations/Interfaces/IConversationRepository.cs`
- **Query service interface**: `Wallow.Messaging.Application/Conversations/Interfaces/IMessagingQueryService.cs`
- **EF Core config**: `Wallow.Messaging.Infrastructure/Persistence/Configurations/`
- **Module registration**: `Wallow.Messaging.Infrastructure/Extensions/MessagingModuleExtensions.cs`
- **Controller**: `Wallow.Messaging.Api/Controllers/ConversationsController.cs`
- **Integration events**: `src/Shared/Wallow.Shared.Contracts/Messaging/Events/`
- **Tests**: `tests/Modules/Messaging/Wallow.Messaging.Tests/`

## Patterns and Conventions

- **Conversation is the sole aggregate root**. Messages and Participants are child entities accessed through it.
- **Factory methods on entities**: Use `Conversation.CreateDirect()` / `Conversation.CreateGroup()`, `Message.Create()`, `Participant.Create()`. Never use constructors directly.
- **State transitions via aggregate methods**: `SendMessage()`, `MarkReadBy()` on Conversation. Never set properties directly.
- **Conversation types**: Determined by string `"Direct"` or `"Group"` in `CreateConversationCommand.Type`. Direct requires `RecipientId`; Group requires `MemberIds` and `Name`.
- **Validation**: FluentValidation validators exist for `CreateConversationCommand` and `SendMessageCommand`. Body max length is 4000 characters.
- **HTML sanitization**: The controller sanitizes message bodies via `IHtmlSanitizationService` before passing to the command.
- **Participant access checks**: The controller checks `IMessagingQueryService.IsParticipantAsync()` before allowing message read/send operations.
- **Wolverine handler discovery**: Handlers are auto-discovered. Follow the `Handle`/`HandleAsync` static method convention.
- **Logging**: Uses `[LoggerMessage]` source generator pattern with `partial` classes. Never use `logger.LogInformation()` directly.
- **Strongly-typed IDs**: `ConversationId`, `MessageId`, `ParticipantId`. Integration events use plain `Guid` for serialization.

## Cross-Module Communication

- **Publishes** (via Wolverine in-memory messaging):
  - `ConversationCreatedIntegrationEvent` -- includes ConversationId, ParticipantIds, TenantId
  - `MessageSentIntegrationEvent` -- includes ConversationId, MessageId, SenderId, Content, ParticipantIds, TenantId
- **Consumes**: None currently. This module does not handle events from other modules.
- Integration events are defined in `Wallow.Shared.Contracts.Messaging.Events`.

## Database

- Schema: `messaging`
- DbContext: `MessagingDbContext` (extends `TenantAwareDbContext`)
- Auto-migrates in Development/Testing environments
- Uses pooled DbContext factory with tenant-aware scoped resolution
- Separate read DbContext registered via `AddReadDbContext<MessagingDbContext>()`

## Authorization

All endpoints require the `MessagingAccess` permission (`HasPermission(PermissionType.MessagingAccess)`).

## Testing

```bash
./scripts/run-tests.sh messaging
```

Tests cover domain entities, application handlers/validators, event handlers, repository persistence, query service, and API controller.
