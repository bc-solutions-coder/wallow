# Messaging Module

## Overview

The Messaging module provides tenant-scoped, real-time-style conversation and messaging capabilities. It supports both direct (1:1) and group conversations with participant management, read tracking, and unread counts.

The module follows Clean Architecture with CQRS patterns, using Wolverine for command/query handling and domain event dispatching. All conversations are tenant-scoped via EF Core query filters.

## Key Features

- **Direct Conversations**: 1:1 messaging between two users
- **Group Conversations**: Named conversations with multiple participants
- **Read Tracking**: Per-participant last-read timestamps and unread counts
- **Cursor-Based Pagination**: Messages paginated via cursor for efficient scrolling
- **HTML Sanitization**: Message bodies are sanitized before persistence
- **Multi-tenancy**: Automatic tenant isolation via EF Core query filters
- **Event-Driven**: Domain events bridged to integration events for cross-module communication

## Architecture

```
src/Modules/Messaging/
+-- Wallow.Messaging.Domain         # Entities, Enums, Domain Events, Identity types
+-- Wallow.Messaging.Application    # Commands, Queries, Handlers, DTOs, Validators
+-- Wallow.Messaging.Infrastructure # EF Core, Repositories, Query Services
+-- Wallow.Messaging.Api            # Controllers, Request/Response Contracts
```

**Database Schema**: `messaging` (PostgreSQL)

## Domain Entities

### Conversation (Aggregate Root)

The central aggregate. Created as either `Direct` or `Group`. Contains participants and messages.

- **Direct**: Exactly two participants, no subject
- **Group**: Multiple participants, requires a subject/name

**Statuses**: `Active`, `Archived` (archived conversations reject new messages)

### Message (Entity)

A single message within a conversation. Owned by a sender, tracks sent time and status.

**Statuses**: `Sent`, `Read`

**Validation**: Body is required and limited to 4000 characters.

### Participant (Entity)

Links a user to a conversation. Tracks join time, last-read timestamp, and active status. Participants can leave (soft deactivation).

## Strongly-Typed IDs

`ConversationId`, `MessageId`, `ParticipantId` -- all `readonly record struct` implementing `IStronglyTypedId`.

## Commands

| Command | Description |
|---------|-------------|
| `CreateConversationCommand` | Create a direct or group conversation |
| `SendMessageCommand` | Send a message to a conversation |
| `MarkConversationReadCommand` | Mark a conversation as read for a user |

## Queries

| Query | Returns |
|-------|---------|
| `GetConversationsQuery` | Paginated list of conversations for a user |
| `GetMessagesQuery` | Cursor-paginated messages in a conversation |
| `GetUnreadConversationCountQuery` | Count of conversations with unread messages |

## Domain Events

| Event | Raised When |
|-------|-------------|
| `ConversationCreatedDomainEvent` | Conversation created |
| `MessageSentDomainEvent` | Message sent to a conversation |

## Integration Events

Events published via Wolverine in-memory messaging for cross-module communication. Defined in `Wallow.Shared.Contracts.Messaging.Events`.

| Event | When |
|-------|------|
| `ConversationCreatedIntegrationEvent` | Conversation created (includes participant IDs) |
| `MessageSentIntegrationEvent` | Message sent (includes content and recipient participant IDs) |

## API Endpoints

All endpoints require authentication and `MessagingAccess` permission. Route prefix: `/api/v1/conversations`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/v1/conversations` | Create a conversation |
| `GET` | `/api/v1/conversations` | List conversations (paginated) |
| `GET` | `/api/v1/conversations/{id}/messages` | Get messages (cursor-paginated) |
| `POST` | `/api/v1/conversations/{id}/messages` | Send a message |
| `POST` | `/api/v1/conversations/{id}/read` | Mark conversation as read |
| `GET` | `/api/v1/conversations/unread-count` | Get unread conversation count |

## Configuration

The module uses the shared `DefaultConnection` connection string. No additional configuration is required. The module auto-migrates its schema on startup in Development and Testing environments.

## Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, strongly-typed IDs, multi-tenancy, Result pattern |
| `Wallow.Shared.Contracts` | Integration event definitions |
| `Wallow.Shared.Infrastructure.Core` | Tenant-aware DbContext, read DbContext registration |

## Testing

```bash
./scripts/run-tests.sh messaging
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/Messaging/Wallow.Messaging.Infrastructure \
    --startup-project src/Wallow.Api \
    --context MessagingDbContext
```
