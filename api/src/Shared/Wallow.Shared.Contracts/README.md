# Wallow.Shared.Contracts

Integration event contracts and cross-module service interfaces.

## Purpose

Defines the inter-module communication boundary. Modules reference this package to communicate via events or query other modules' data. This enables module autonomy while maintaining loose coupling.

## Integration Events

All integration events implement `IIntegrationEvent` and extend the `IntegrationEvent` base record, which provides `EventId` (Guid) and `OccurredAt` (DateTime).

### Events by Namespace

This README is the **canonical integration-event catalogue**. Module READMEs link here rather than
restating it, because partial copies have drifted.

**Identity** (`Identity/Events/`, 29 events — `MembershipTransition.cs` is a supporting type, not an event):

- *User lifecycle*: `UserRegisteredEvent`, `UserRoleChangedEvent`, `UserLoginSucceededEvent`,
  `UserLoginFailedEvent`, `UserAccountLockedOutEvent`, `UserSessionEvictedEvent`,
  `PasswordChangedEvent`, `PasswordResetRequestedEvent`, `UserEmailChangedEvent`,
  `UserEmailChangeRequestedEvent`, `EmailVerificationRequestedEvent`, `EmailVerifiedEvent`
- *Passwordless*: `MagicLinkRequestedEvent`, `OtpCodeRequestedEvent`
- *MFA*: `UserMfaEnabledEvent`, `UserMfaDisabledEvent`, `UserMfaLockedOutEvent`,
  `UserMfaLockoutClearedEvent`, `UserMfaBackupCodesRegeneratedEvent`
- *Organization*: `OrganizationCreatedEvent`, `OrganizationArchivedEvent`,
  `OrganizationReactivatedEvent`, `OrganizationDeletedEvent`, `OrganizationSettingsUpdatedEvent`
- *Membership and access*: `OrganizationMemberAddedEvent`, `OrganizationMemberRemovedEvent`,
  `MembershipTransitionedEvent`, `AccessRequestedEvent`, `InvitationCreatedEvent`

**Announcements**: `AnnouncementPublishedEvent`.

**Inquiries**: `InquirySubmittedEvent`, `InquiryStatusChangedEvent`, `InquiryCommentAddedEvent`.

**Delivery** (`EmailSentEvent`, `PushSentEvent`, `SmsSentEvent`) and **Notifications** (`NotificationCreatedEvent`) are contract namespaces, not modules. These events are declared but nothing publishes or consumes them yet.

## Cross-Module Service Interfaces

Implemented by whichever project the Owner column names — a module's Infrastructure layer for most,
and `Wallow.Api/Services/` for the four the host owns. The full set is:

| Interface | Owner | Purpose |
|-----------|-------|---------|
| `IUserQueryService` | Identity | Read-only user lookups |
| `IUserService` | Identity | User operations other modules need |
| `IScopeSubsetValidator` | Identity | Validates a requested scope set against a grantor's |
| `ISetupStatusProvider` | Identity | First-run setup state |
| `IApiKeyService` | ApiKeys | API key issue and validation |
| `IStorageProvider` | Storage | File storage abstraction |
| `IEmailService` | Notifications | Email dispatch |
| `IRealtimeDispatcher` | Api host | Push events to connected clients |
| `IRealtimeAccessRevoker` | Api host | Force-disconnect a principal |
| `ISseDispatcher` | Api host | Server-Sent Events dispatcher |
| `IPresenceService` | Api host | User presence tracking |

Note that `IUserService`, `IStorageProvider`, `IApiKeyService` and `IEmailService` are not read-only,
so the "Read-only" rule below applies to the *query* services specifically.

## Real-time Messaging

- `RealtimeEnvelope` - Module-specific message wrapper

## Shared Commands

Not everything here is an event or an interface: `Storage/Commands/UploadFileCommand.cs` is a command
record that lives in Contracts while its handler and validator live in `Wallow.Storage.Application`.

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
