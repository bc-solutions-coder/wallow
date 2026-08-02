# Wallow.Shared.Contracts

Integration event contracts and cross-module service interfaces.

## Purpose

Defines the inter-module communication boundary. Modules reference this package to communicate via events or query other modules' data. This enables module autonomy while maintaining loose coupling.

## Integration Events

All integration events implement `IIntegrationEvent` and extend the `IntegrationEvent` base record, which provides `EventId` (Guid) and `OccurredAt` (DateTime).

### Events by Namespace

**Identity**: `UserRegisteredEvent`, `UserRoleChangedEvent`, `OrganizationCreatedEvent`, `OrganizationMemberAddedEvent`, `OrganizationMemberRemovedEvent`, `PasswordResetRequestedEvent`, `EmailVerificationRequestedEvent`, `EmailVerifiedEvent`, `InvitationCreatedEvent`, `MagicLinkRequestedEvent`, `OtpCodeRequestedEvent`, and others.

**Announcements**: `AnnouncementPublishedEvent`.

**Inquiries**: `InquirySubmittedEvent`, `InquiryStatusChangedEvent`, `InquiryCommentAddedEvent`.

**Delivery** (`EmailSentEvent`, `PushSentEvent`, `SmsSentEvent`) and **Notifications** (`NotificationCreatedEvent`) are contract namespaces, not modules. These events are declared but nothing publishes or consumes them yet.

## Cross-Module Query Services

Modules expose read-only interfaces implemented in their Infrastructure layer:
- `IUserQueryService` (Identity)

## Real-time Messaging

- `RealtimeEnvelope` - Module-specific message wrapper
- `IRealtimeDispatcher` - Push events to connected clients
- `ISseDispatcher` - Server-Sent Events dispatcher
- `IPresenceService` - User presence tracking

## Other Contracts

Additional contract subdirectories exist for: Annotations, Announcements, ApiKeys, Delivery, Identity, Inquiries, Notifications, Realtime, Setup, and Storage.

## Conventions

### Event Design Rules
1. **Past tense naming**: `UserRegisteredEvent`, not `RegisterUserEvent`
2. **Primitive types only**: No domain entities or value objects (serialization-friendly)
3. **Include context**: TenantId, UserId, EntityId for downstream handlers
4. **Immutable records**: Events are facts, never modified

### Service Interface Rules
1. **Read-only**: Query services do not mutate state
2. **DTOs only**: Return data transfer objects, not domain entities
3. **Async**: All methods return `Task<T>`

## Dependencies

None. Intentionally zero dependencies for maximum portability.
