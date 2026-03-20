# Phase 11: Messaging Module

**Scope:** `src/Modules/Messaging/`
**Status:** Not Started
**Files:** 25 source files, 19 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

---

## Domain Layer

### Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Entities/Conversation.cs` | Aggregate root for a conversation (direct or group) | `CreateDirect` (2 participants, raises `ConversationCreatedDomainEvent`); `CreateGroup` (creator + members, deduplicates, raises event); `SendMessage` (validates active status and sender participation, creates Message child, raises `MessageSentDomainEvent`); `MarkReadBy` (delegates to Participant) | `AggregateRoot`, `ITenantScoped`, `ConversationStatus`, `Message`, `Participant`, domain events, `ConversationException` | |
| 2 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Entities/Message.cs` | Child entity representing a single sent message | `Create` factory sets `ConversationId`, `SenderId`, `Body`, `SentAt` from `TimeProvider`, status `Sent` | `Entity<MessageId>`, `MessageStatus`, `ConversationId` | |
| 3 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Entities/Participant.cs` | Child entity tracking a user's membership in a conversation | `Create` factory; `MarkRead` updates `LastReadAt`; `Leave` sets `IsActive = false` | `Entity<ParticipantId>`, `ConversationId` | |

### Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 4 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Enums/ConversationStatus.cs` | Conversation lifecycle states | `Active = 0`, `Archived = 1` | None | |
| 5 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Enums/MessageStatus.cs` | Message delivery states | `Sent = 0`, `Read = 1` | None | |

### Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 6 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Events/ConversationCreatedDomainEvent.cs` | Raised when a conversation is created | Record with `ConversationId`, `TenantId` | `DomainEvent` base | |
| 7 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Events/MessageSentDomainEvent.cs` | Raised when a message is sent in a conversation | Record with `ConversationId`, `MessageId`, `SenderId`, `TenantId` | `DomainEvent` base | |

### Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 8 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Exceptions/ConversationException.cs` | Domain exception for conversation business rule violations | Wraps message with code `"Messaging.Conversation"` | `BusinessRuleException` base | |

### Identity (Strongly-Typed IDs)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 9 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Identity/ConversationId.cs` | Strongly-typed ID for Conversation | `readonly record struct` with `Create` and `New` factory methods | `IStronglyTypedId` | |
| 10 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Identity/MessageId.cs` | Strongly-typed ID for Message | `readonly record struct` with `Create` and `New` factory methods | `IStronglyTypedId` | |
| 11 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/Conversations/Identity/ParticipantId.cs` | Strongly-typed ID for Participant | `readonly record struct` with `Create` and `New` factory methods | `IStronglyTypedId` | |

### Marker Interface

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 12 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Domain/IMessagingDomainMarker.cs` | Assembly marker interface for Wolverine discovery | Empty interface, no logic | None | |

---

## Application Layer

### Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 13 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/CreateConversation/CreateConversationCommand.cs` | Command record to create a new conversation | Fields: `InitiatorId`, `RecipientId?`, `MemberIds?`, `Type` (`"Direct"` or `"Group"`), `Name?` | None | |
| 14 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/CreateConversation/CreateConversationHandler.cs` | Handles `CreateConversationCommand` to persist a new conversation | Switches on `Type` to call `Conversation.CreateDirect` or `Conversation.CreateGroup`; adds to repo; returns `Result<Guid>` with new conversation ID | `IConversationRepository`, `ITenantContext`, `TimeProvider` | |
| 15 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/CreateConversation/CreateConversationValidator.cs` | FluentValidation validator for `CreateConversationCommand` | Requires non-empty `InitiatorId`; `Type` must be `"Direct"` or `"Group"`; Direct requires `RecipientId`; Group requires `MemberIds` and `Name` | `AbstractValidator`, `FluentValidation` | |
| 16 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/MarkConversationRead/MarkConversationReadCommand.cs` | Command record to mark a conversation as read by a user | Fields: `ConversationId`, `UserId` | None | |
| 17 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/MarkConversationRead/MarkConversationReadHandler.cs` | Handles `MarkConversationReadCommand`; updates participant's `LastReadAt` | Loads conversation; verifies user is active participant via `IMessagingQueryService.IsParticipantAsync`; calls `conversation.MarkReadBy`; returns 401 if not participant | `IConversationRepository`, `IMessagingQueryService`, `TimeProvider` | |
| 18 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/SendMessage/SendMessageCommand.cs` | Command record to send a message in a conversation | Fields: `ConversationId`, `SenderId`, `Body` | None | |
| 19 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/SendMessage/SendMessageHandler.cs` | Handles `SendMessageCommand`; appends message to conversation | Loads conversation; returns `NotFound` if absent; calls `conversation.SendMessage`; returns `Result<Guid>` with new message ID | `IConversationRepository`, `TimeProvider` | |
| 20 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Commands/SendMessage/SendMessageValidator.cs` | FluentValidation validator for `SendMessageCommand` | `ConversationId` and `SenderId` non-empty; `Body` non-empty and max 4000 chars | `AbstractValidator`, `FluentValidation` | |

### Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 21 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Queries/GetConversations/GetConversationsQuery.cs` | Query record to list conversations for a user | Fields: `UserId`, `Page`, `PageSize` | None | |
| 22 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Queries/GetConversations/GetConversationsHandler.cs` | Handles `GetConversationsQuery`; returns paginated conversation list | Delegates to `IMessagingQueryService.GetConversationsAsync`; wraps in `Result.Success` | `IMessagingQueryService` | |
| 23 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Queries/GetMessages/GetMessagesQuery.cs` | Query record to fetch messages in a conversation (cursor-based pagination) | Fields: `ConversationId`, `UserId`, `CursorMessageId?`, `PageSize` | None | |
| 24 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Queries/GetMessages/GetMessagesHandler.cs` | Handles `GetMessagesQuery`; returns ordered message page | Delegates to `IMessagingQueryService.GetMessagesAsync`; wraps in `Result.Success` | `IMessagingQueryService` | |
| 25 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Queries/GetUnreadConversationCount/GetUnreadConversationCountQuery.cs` | Query record to count conversations with unread messages for a user | Field: `UserId` | None | |
| 26 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Queries/GetUnreadConversationCount/GetUnreadConversationCountHandler.cs` | Handles `GetUnreadConversationCountQuery`; returns integer unread count | Delegates to `IMessagingQueryService.GetUnreadConversationCountAsync`; wraps in `Result.Success` | `IMessagingQueryService` | |

### Event Handlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 27 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/EventHandlers/ConversationCreatedEventHandler.cs` | Handles `ConversationCreatedDomainEvent`; publishes integration event to RabbitMQ | Reloads conversation; maps participant IDs; publishes `ConversationCreatedIntegrationEvent` via Wolverine `IMessageBus`; logs each step | `IConversationRepository`, `IMessageBus`, `ILogger`, `Shared.Contracts` | |
| 28 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/EventHandlers/MessageSentEventHandler.cs` | Handles `MessageSentDomainEvent`; publishes integration event excluding the sender | Reloads conversation; finds message by ID; collects active participant IDs (excluding sender); publishes `MessageSentIntegrationEvent` via Wolverine; logs each step | `IConversationRepository`, `IMessageBus`, `ILogger`, `Shared.Contracts` | |

### DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 29 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/DTOs/ConversationDto.cs` | Read model for a conversation in list views | Fields: `Id`, `Type`, `Participants`, `LastMessage?`, `UnreadCount`, `LastActivityAt` | `ParticipantDto`, `MessageDto` | |
| 30 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/DTOs/MessageDto.cs` | Read model for a single message | Fields: `Id`, `ConversationId`, `SenderId`, `Body`, `SentAt`, `Status` | None | |
| 31 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/DTOs/ParticipantDto.cs` | Read model for a conversation participant | Fields: `UserId`, `JoinedAt`, `LastReadAt?`, `IsActive`; annotated `[UsedImplicitly]` for Dapper | `JetBrains.Annotations` | |

### Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 32 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Interfaces/IConversationRepository.cs` | Repository contract for `Conversation` aggregate | `Add`, `GetByIdAsync` (with participants included), `SaveChangesAsync` | `Conversation`, `ConversationId` | |
| 33 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/Conversations/Interfaces/IMessagingQueryService.cs` | Dapper read-side service contract | `IsParticipantAsync`, `GetUnreadConversationCountAsync`, `GetMessagesAsync` (cursor-paged), `GetConversationsAsync` (offset-paged) | `ConversationDto`, `MessageDto` | |

### Marker Interface

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 34 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Application/IMessagingApplicationMarker.cs` | Assembly marker interface for Wolverine discovery | Empty interface, no logic | None | |

---

## Infrastructure Layer

### Persistence — DbContext

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 35 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Persistence/MessagingDbContext.cs` | EF Core DbContext for the messaging schema | Sets default schema `"messaging"`; applies configurations from assembly; calls `ApplyTenantQueryFilters`; default tracking behavior `NoTracking` | `TenantAwareDbContext`, `ITenantContext`, `Conversation`, `Message`, `Participant` | |

### Persistence — Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 36 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Persistence/Configurations/ConversationConfiguration.cs` | EF mapping for `Conversation` | Table `conversations`; maps all properties including `StronglyTypedIdConverter` for `Id` and `TenantId`; status stored as string; indexes on `tenant_id` and `created_at` (descending); ignores `DomainEvents` | `IEntityTypeConfiguration`, `StronglyTypedIdConverter` | |
| 37 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Persistence/Configurations/MessageConfiguration.cs` | EF mapping for `Message` | Table `messages`; maps `Id`, `ConversationId`, `SenderId`, `Body` (text column), `Status` (string), `SentAt`; indexes on `conversation_id` and `sender_id` | `IEntityTypeConfiguration`, `StronglyTypedIdConverter` | |
| 38 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Persistence/Configurations/ParticipantConfiguration.cs` | EF mapping for `Participant` | Table `participants`; maps all properties; unique composite index on `(conversation_id, user_id)` | `IEntityTypeConfiguration`, `StronglyTypedIdConverter` | |

### Persistence — Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 39 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Persistence/Repositories/ConversationRepository.cs` | EF Core implementation of `IConversationRepository` | `Add` tracks entity; `GetByIdAsync` uses `AsTracking().Include(c => c.Participants)`; `SaveChangesAsync` delegates to DbContext | `MessagingDbContext`, `IConversationRepository` | |

### Persistence — Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 40 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Migrations/20260312201411_InitialCreate.cs` | Initial EF migration creating conversations, messages, participants tables | Creates tables and indexes in `messaging` schema | EF Core Migrations | |
| 41 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Migrations/20260312201411_InitialCreate.Designer.cs` | Auto-generated migration snapshot metadata | EF-generated; not hand-edited | EF Core Migrations | |
| 42 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Migrations/MessagingDbContextModelSnapshot.cs` | EF Core model snapshot for migration tracking | Auto-generated; describes current model state | EF Core Migrations | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 43 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Services/MessagingQueryService.cs` | Dapper-based implementation of `IMessagingQueryService` | `IsParticipantAsync` — EXISTS query validating active participant in tenant; `GetUnreadConversationCountAsync` — COUNT of conversations with messages after `last_read_at`; `GetMessagesAsync` — cursor-paged DESC query with optional cursor filter; `GetConversationsAsync` — CTE with CTEs for `user_conversations`, `last_messages`, `unread_counts`, plus second query to load participants in batch | `MessagingDbContext`, `ITenantContext`, Dapper | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 44 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Infrastructure/Extensions/MessagingModuleExtensions.cs` | DI registration and startup initialization for the Messaging module | `AddMessagingModule`: registers `MessagingDbContext` (Npgsql with retry/timeout, `TenantSaveChangesInterceptor`), `IConversationRepository`, `IMessagingQueryService`; `InitializeMessagingModuleAsync`: auto-migrates in Development/Testing; logs warning on failure | `IServiceCollection`, `WebApplication`, `MessagingDbContext`, EF Core | |

---

## Api Layer

### Contracts — Requests

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 45 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Api/Contracts/Messaging/Requests/CreateConversationRequest.cs` | HTTP request body for creating a conversation | Fields: `ParticipantIds` (list), `Subject?`; API infers Direct vs Group from count | None | |
| 46 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Api/Contracts/Messaging/Requests/SendMessageRequest.cs` | HTTP request body for sending a message | Field: `Body` | None | |

### Contracts — Responses

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 47 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Api/Contracts/Messaging/Responses/ConversationResponse.cs` | HTTP response shape for a conversation | Fields: `Id`, `Type`, `Participants`, `LastMessage?`, `UnreadCount`, `LastActivityAt`; annotated `[UsedImplicitly]` | `ParticipantDto`, `MessageDto` | |
| 48 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Api/Contracts/Messaging/Responses/MessagePageResponse.cs` | HTTP response envelope for paginated messages (cursor-based) | Fields: `Items`, `NextCursor?`, `HasMore` | `MessageResponse` | |
| 49 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Api/Contracts/Messaging/Responses/MessageResponse.cs` | HTTP response shape for a single message | Fields: `Id`, `ConversationId`, `SenderId`, `Body`, `Status`, `SentAt`; annotated `[UsedImplicitly]` | None | |
| 50 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Api/Contracts/Messaging/Responses/UnreadCountResponse.cs` | HTTP response for the unread conversation count | Field: `Count` | None | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 51 | [ ] | `src/Modules/Messaging/Wallow.Messaging.Api/Controllers/ConversationsController.cs` | REST controller for conversation and messaging endpoints | `POST /conversations` — creates Direct or Group based on participant count; `GET /conversations` — paginated list for current user; `GET /conversations/{id}/messages` — cursor-paged messages (checks `IsParticipant` first); `POST /conversations/{id}/messages` — sends message (checks `IsParticipant`, sanitizes body via `IHtmlSanitizationService`); `POST /conversations/{id}/read` — marks read; `GET /conversations/unread-count` — badge count; all endpoints require `MessagingAccess` permission | `IMessageBus`, `IHtmlSanitizationService`, `ICurrentUserService`, `IMessagingQueryService`, `[HasPermission]` | |

---

## Test Files

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|--------------|------------|
| 52 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Domain/Entities/ConversationCreateTests.cs` | Domain entity creation tests | `CreateDirect` / `CreateGroup` produce correct state (status, participant count, deduplication), raise `ConversationCreatedDomainEvent`, set timestamps | |
| 53 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Domain/Entities/ConversationStateTests.cs` | Domain entity state-transition tests | `SendMessage` validates archived guard, non-participant guard, appends message, raises event; `MarkReadBy` updates `LastReadAt` | |
| 54 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Domain/Entities/MessageCreateTests.cs` | Message entity creation tests | `Message.Create` sets correct fields (body, sender, conversation ID, status `Sent`, `SentAt` timestamp) | |
| 55 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Domain/Entities/MessageStateTests.cs` | Message entity state tests | Status transitions and invariants on `Message` | |
| 56 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/Commands/CreateConversation/CreateConversationHandlerTests.cs` | Handler unit tests for `CreateConversationHandler` | Direct and Group paths call `repository.Add` and `SaveChangesAsync`; returns `Result.Success` with non-empty GUID; uses NSubstitute mocks | |
| 57 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/Commands/CreateConversation/CreateConversationValidatorTests.cs` | Validator tests for `CreateConversationValidator` | Error conditions (missing InitiatorId, invalid type, missing RecipientId for Direct, missing MemberIds/Name for Group); valid scenarios produce no errors | |
| 58 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/Commands/MarkConversationRead/MarkConversationReadHandlerTests.cs` | Handler unit tests for `MarkConversationReadHandler` | `NotFound` when conversation absent; unauthorized when not participant; success path calls `MarkReadBy` and saves | |
| 59 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/Commands/SendMessage/SendMessageHandlerTests.cs` | Handler unit tests for `SendMessageHandler` | `NotFound` when conversation absent; success path appends message and returns message GUID | |
| 60 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/Commands/SendMessage/SendMessageValidatorTests.cs` | Validator tests for `SendMessageValidator` | Empty `ConversationId`/`SenderId` errors; empty body error; body exceeding 4000 chars error; valid command passes | |
| 61 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/EventHandlers/ConversationCreatedEventHandlerTests.cs` | Event handler unit tests for `ConversationCreatedEventHandler` | Publishes `ConversationCreatedIntegrationEvent` with correct participant IDs; no-op when conversation not found | |
| 62 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/EventHandlers/MessageSentEventHandlerTests.cs` | Event handler unit tests for `MessageSentEventHandler` | Publishes `MessageSentIntegrationEvent` excluding sender from participant IDs; no-op when conversation/message not found | |
| 63 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/Queries/GetConversations/GetConversationsHandlerTests.cs` | Handler unit tests for `GetConversationsHandler` | Delegates to query service and returns `Result.Success` wrapping the list | |
| 64 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/Queries/GetMessages/GetMessagesHandlerTests.cs` | Handler unit tests for `GetMessagesHandler` | Delegates to query service and returns `Result.Success` wrapping the list | |
| 65 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/Application/Queries/GetUnreadConversationCount/GetUnreadConversationCountHandlerTests.cs` | Handler unit tests for `GetUnreadConversationCountHandler` | Delegates to query service and returns `Result.Success` wrapping the integer count | |
| 66 | [ ] | `tests/Modules/Messaging/Wallow.Messaging.Tests/MessagingTestsMarker.cs` | Assembly marker for test project | No test logic; used for assembly scanning | |
| 67 | [ ] | `tests/Wallow.Messaging.IntegrationTests/Fixtures/MessagingTestFixture.cs` | Integration test fixture for Messaging module | Extends `WallowApiFactory`; registers `MessageTracker`, `CrossModuleEventTracker`, `MessageWaiter` singletons; enables RabbitMQ transport for end-to-end event flow tests | |
| 68 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Messaging/TenantRestoringMiddlewareTests.cs` | Unit tests for tenant-restoring Wolverine middleware (shared infrastructure) | Verifies tenant context is restored from message envelope headers | |
| 69 | [ ] | `tests/Wallow.Shared.Infrastructure.Tests/Messaging/TenantStampingMiddlewareTests.cs` | Unit tests for tenant-stamping Wolverine middleware (shared infrastructure) | Verifies tenant ID is stamped onto outgoing message envelope headers | |
| 70 | [ ] | `tests/Wallow.Shared.Kernel.Tests/Messaging/WolverineErrorHandlingExtensionsTests.cs` | Unit tests for Wolverine error handling extension methods (shared kernel) | Verifies retry/dead-letter policies configured correctly via extension methods | |
