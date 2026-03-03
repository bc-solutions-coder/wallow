# Communications Module

## Module Responsibility

Owns all communication channels (Email, SMS, In-App notifications), user-to-user messaging (direct and group conversations), announcements/changelog, and notification preferences. All entities are tenant-scoped. Consumes cross-module events from `Shared.Contracts` to trigger notifications (e.g., `SendSmsRequestedEvent`, `SendEmailRequestedEvent`).

## Layer Rules

- **Domain** (`Foundry.Communications.Domain`): Organized by subdomain:
  - `Channels/Email/` -- `EmailMessage` aggregate root, `EmailAddress` value object, `EmailStatus` enum, domain events.
  - `Channels/Sms/` -- `SmsMessage` aggregate root with lifecycle (`Create` -> `MarkAsSent`/`MarkAsFailed` -> `ResetForRetry`), `PhoneNumber` value object (E.164 validation), `SmsStatus` enum. 1600-char body limit.
  - `Channels/InApp/` -- `Notification` aggregate root, `NotificationStatus`/`NotificationPriority` enums, domain events.
  - `Messaging/` -- `Conversation` aggregate root (owns `Participant` and `Message` child entities), factory methods `CreateDirect` and `CreateGroup`, `ConversationStatus`/`MessageStatus` enums.
  - `Announcements/` -- `Announcement` and `ChangelogEntry` entities.
  - `Preferences/` -- `NotificationPreference` entity for user channel preferences.
- **Application** (`Foundry.Communications.Application`): CQRS commands/queries per subdomain. Event handlers bridge cross-module events (`SendSmsRequestedEventHandler`, etc.) to internal commands. `IMessagingQueryService` for Dapper-based read queries.
- **Infrastructure** (`Foundry.Communications.Infrastructure`): `CommunicationsDbContext` (EF Core, `communications` schema), providers (`TwilioSmsProvider`, `NullSmsProvider`, email providers), Dapper query services, background jobs.
- **Api** (`Foundry.Communications.Api`): Controllers for each subdomain (messaging, notifications, email preferences, announcements).

## Key Patterns

- **Channel provider abstraction**: `ISmsProvider`, `IEmailProvider` interfaces in Application; Infrastructure implements with real (Twilio, SMTP) and null providers.
- **Cross-module event consumption**: Other modules publish events like `SendSmsRequestedEvent` to `Shared.Contracts`. This module's event handlers consume them and dispatch internal commands via `IMessageBus`.
- **Conversation aggregate**: `Conversation` enforces that only active participants can send messages and that archived conversations reject new messages.
- **Dapper for reads**: `IMessagingQueryService` uses Dapper for paginated inbox, cursor-based message history, and unread counts.

## Dependencies

- **Depends on**: `Foundry.Shared.Kernel`, `Foundry.Shared.Contracts` (consumes `SendSmsRequestedEvent`, `SendEmailRequestedEvent` and other cross-module events; publishes `ConversationCreatedIntegrationEvent`, `MessageSentIntegrationEvent`).
- **Depended on by**: `Foundry.Api` (registers module). Other modules trigger communications by publishing events to `Shared.Contracts`.

## Constraints

- Do not reference other modules directly. All cross-module communication via `Shared.Contracts` events.
- This module uses the `communications` PostgreSQL schema.
- SMS body limit is 1600 characters (enforced in domain).
- Phone numbers must be E.164 format (validated by `PhoneNumber` value object).
