# Phase 10: Communications Module

**Scope:** Complete Communications module - Domain, Application, Infrastructure, Api layers + all tests
**Status:** Not Started
**Files:** 216 source files, 104 test files

## How to Use This Document
- Work through layers bottom-up: Domain -> Application -> Infrastructure -> Api -> Tests
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

---

## Domain Layer

### Announcements / Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 1 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Entities/Announcement.cs` | Announcement aggregate root with tenant scoping | Create, Update, Publish, Archive lifecycle; supports pinning, dismissibility, action URLs, targeting | `AggregateRoot<AnnouncementId>`, `ITenantScoped`, Shared.Kernel | |
| 2 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Entities/AnnouncementDismissal.cs` | Tracks per-user dismissals of announcements | Create factory method linking announcement to user with timestamp | `Entity<AnnouncementDismissalId>`, `AnnouncementId`, `UserId` | |
| 3 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Entities/ChangelogEntry.cs` | Changelog release entry with version, title, content | Create/Publish lifecycle; owns collection of ChangelogItems | `AggregateRoot<ChangelogEntryId>`, `ITenantScoped` | |
| 4 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Entities/ChangelogItem.cs` | Individual changelog line item (added, fixed, etc.) | Create factory method with description and change type | `Entity<ChangelogItemId>`, `ChangeType` enum | |

### Announcements / Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 5 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Enums/AnnouncementStatus.cs` | Announcement lifecycle states | Draft, Published, Archived | None | |
| 6 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Enums/AnnouncementTarget.cs` | Targeting scope for announcements | All, Role, User targeting options | None | |
| 7 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Enums/AnnouncementType.cs` | Visual type/category of announcement | Info, Warning, Success, etc. | None | |
| 8 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Enums/ChangeType.cs` | Type of changelog change | Added, Changed, Fixed, Removed, etc. | None | |

### Announcements / Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 9 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Identity/AnnouncementDismissalId.cs` | Strongly-typed ID for AnnouncementDismissal | GUID-backed value object with Create factory | `StronglyTypedId<>` | |
| 10 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Identity/AnnouncementId.cs` | Strongly-typed ID for Announcement | GUID-backed value object with Create factory | `StronglyTypedId<>` | |
| 11 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Identity/ChangelogEntryId.cs` | Strongly-typed ID for ChangelogEntry | GUID-backed value object with Create factory | `StronglyTypedId<>` | |
| 12 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Announcements/Identity/ChangelogItemId.cs` | Strongly-typed ID for ChangelogItem | GUID-backed value object with Create factory | `StronglyTypedId<>` | |

### Channels / Email / Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 13 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/Entities/EmailMessage.cs` | EmailMessage aggregate root for outbound emails | Create, MarkAsSent, MarkAsFailed lifecycle; stores to/from/subject/body/status | `AggregateRoot<EmailMessageId>`, `ITenantScoped` | |
| 14 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/Entities/EmailPreference.cs` | Per-user email notification preferences | Create factory; toggle enabled/disabled per notification type | `Entity<EmailPreferenceId>`, `UserId` | |

### Channels / Email / Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 15 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/Enums/EmailStatus.cs` | Email delivery status states | Pending, Sent, Failed | None | |

### Channels / Email / Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 16 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/Events/EmailFailedDomainEvent.cs` | Domain event raised when email delivery fails | Carries EmailMessageId and error details | Domain event record | |
| 17 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/Events/EmailSentDomainEvent.cs` | Domain event raised when email is sent successfully | Carries EmailMessageId | Domain event record | |

### Channels / Email / Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 18 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/Exceptions/InvalidEmailAddressException.cs` | Domain exception for invalid email addresses | Thrown during EmailAddress value object validation | Domain exception base | |

### Channels / Email / Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 19 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/Identity/EmailMessageId.cs` | Strongly-typed ID for EmailMessage | GUID-backed value object | `StronglyTypedId<>` | |
| 20 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/Identity/EmailPreferenceId.cs` | Strongly-typed ID for EmailPreference | GUID-backed value object | `StronglyTypedId<>` | |

### Channels / Email / ValueObjects

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 21 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/ValueObjects/EmailAddress.cs` | Email address value object with validation | Format validation, throws InvalidEmailAddressException | Value object | |
| 22 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Email/ValueObjects/EmailContent.cs` | Email body content value object | Encapsulates subject and HTML/text body content | Value object | |

### Channels / InApp / Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 23 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/InApp/Entities/Notification.cs` | In-app notification aggregate root | Create, MarkAsRead, Archive lifecycle; tracks read/archived state | `AggregateRoot<NotificationId>`, `ITenantScoped` | |

### Channels / InApp / Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 24 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/InApp/Events/NotificationCreatedDomainEvent.cs` | Domain event when notification is created | Carries NotificationId and UserId for real-time push | Domain event record | |
| 25 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/InApp/Events/NotificationReadDomainEvent.cs` | Domain event when notification is marked as read | Carries NotificationId | Domain event record | |

### Channels / InApp / Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 26 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/InApp/Identity/NotificationId.cs` | Strongly-typed ID for Notification | GUID-backed value object | `StronglyTypedId<>` | |

### Channels / Sms / Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 27 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/Entities/SmsMessage.cs` | SMS message aggregate root | Create/MarkAsSent/MarkAsFailed/ResetForRetry lifecycle; 1600-char body limit | `AggregateRoot<SmsMessageId>`, `ITenantScoped` | |
| 28 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/Entities/SmsPreference.cs` | Per-user SMS notification preferences | Create factory; toggle enabled/disabled | `Entity<SmsPreferenceId>`, `UserId` | |

### Channels / Sms / Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 29 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/Enums/SmsStatus.cs` | SMS delivery status states | Pending, Sent, Failed | None | |

### Channels / Sms / Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 30 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/Events/SmsFailedDomainEvent.cs` | Domain event raised when SMS delivery fails | Carries SmsMessageId and error details | Domain event record | |
| 31 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/Events/SmsSentDomainEvent.cs` | Domain event raised when SMS is sent successfully | Carries SmsMessageId | Domain event record | |

### Channels / Sms / Exceptions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 32 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/Exceptions/InvalidPhoneNumberException.cs` | Domain exception for invalid phone numbers | Thrown during PhoneNumber value object E.164 validation | Domain exception base | |

### Channels / Sms / Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 33 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/Identity/SmsMessageId.cs` | Strongly-typed ID for SmsMessage | GUID-backed value object | `StronglyTypedId<>` | |
| 34 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/Identity/SmsPreferenceId.cs` | Strongly-typed ID for SmsPreference | GUID-backed value object | `StronglyTypedId<>` | |

### Channels / Sms / ValueObjects

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 35 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Channels/Sms/ValueObjects/PhoneNumber.cs` | Phone number value object with E.164 validation | Validates E.164 format, throws InvalidPhoneNumberException | Value object | |

### Enums (Root)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 36 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Enums/NotificationType.cs` | Notification type categories | TaskAssigned, TaskCompleted, BillingInvoice, SystemNotification | None | |

### Exceptions (Root)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 37 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Exceptions/ConversationException.cs` | Domain exception for conversation rule violations | Thrown when conversation invariants are violated | Domain exception base | |

### Messaging / Entities

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 38 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Entities/Conversation.cs` | Conversation aggregate root for direct/group messaging | CreateDirect/CreateGroup factories; enforces active participants, rejects archived conversations | `AggregateRoot<ConversationId>`, `ITenantScoped` | |
| 39 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Entities/Message.cs` | Message entity within a conversation | Create factory with sender, body, timestamp; child of Conversation | `Entity<MessageId>` | |
| 40 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Entities/Participant.cs` | Conversation participant entity | Tracks user membership and read state within a conversation | `Entity<ParticipantId>` | |

### Messaging / Enums

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 41 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Enums/ConversationStatus.cs` | Conversation lifecycle states | Active, Archived | None | |
| 42 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Enums/MessageStatus.cs` | Message delivery states | Sent, Delivered, Read | None | |

### Messaging / Events

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 43 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Events/ConversationCreatedDomainEvent.cs` | Domain event when conversation is created | Carries ConversationId and participant info | Domain event record | |
| 44 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Events/MessageSentDomainEvent.cs` | Domain event when message is sent in a conversation | Carries MessageId, ConversationId, SenderId | Domain event record | |

### Messaging / Identity

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 45 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Identity/ConversationId.cs` | Strongly-typed ID for Conversation | GUID-backed value object | `StronglyTypedId<>` | |
| 46 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Identity/MessageId.cs` | Strongly-typed ID for Message | GUID-backed value object | `StronglyTypedId<>` | |
| 47 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Messaging/Identity/ParticipantId.cs` | Strongly-typed ID for Participant | GUID-backed value object | `StronglyTypedId<>` | |

### Preferences

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 48 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Preferences/ChannelType.cs` | Communication channel type enum | Email, Sms, InApp, Push channel types | None | |
| 49 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Preferences/Entities/ChannelPreference.cs` | Per-user per-channel notification preference entity | Create factory; toggle enabled/disabled per channel type | `Entity<ChannelPreferenceId>`, `ITenantScoped` | |
| 50 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Preferences/Events/ChannelPreferenceCreatedEvent.cs` | Domain event when a channel preference is created | Carries preference details | Domain event record | |
| 51 | [ ] | `src/Modules/Communications/Foundry.Communications.Domain/Preferences/Identity/ChannelPreferenceId.cs` | Strongly-typed ID for ChannelPreference | GUID-backed value object | `StronglyTypedId<>` | |

---

## Application Layer

### Announcements / Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 52 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/ArchiveAnnouncement/ArchiveAnnouncementCommand.cs` | Command + handler to archive an announcement | Loads announcement by ID, calls Archive(), saves | `IAnnouncementRepository`, `TimeProvider` | |
| 53 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/ArchiveAnnouncement/ArchiveAnnouncementValidator.cs` | FluentValidation for ArchiveAnnouncementCommand | Validates announcement ID is not empty | `AbstractValidator<>` | |
| 54 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/CreateAnnouncement/CreateAnnouncementCommand.cs` | Command + handler to create a new announcement | Maps DTO to domain entity via Announcement.Create(), saves | `IAnnouncementRepository`, `AnnouncementDto` | |
| 55 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/CreateAnnouncement/CreateAnnouncementValidator.cs` | FluentValidation for CreateAnnouncementCommand | Validates title, content, type, target fields | `AbstractValidator<>` | |
| 56 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/CreateChangelogEntry/CreateChangelogEntryCommand.cs` | Command + handler to create a changelog entry | Maps DTO to ChangelogEntry.Create(), adds items, saves | `IChangelogRepository`, `ChangelogEntryDto` | |
| 57 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/CreateChangelogEntry/CreateChangelogEntryValidator.cs` | FluentValidation for CreateChangelogEntryCommand | Validates version, title, content, items collection | `AbstractValidator<>` | |
| 58 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/DismissAnnouncement/DismissAnnouncementCommand.cs` | Command + handler to dismiss an announcement for a user | Creates AnnouncementDismissal linking user to announcement | `IAnnouncementDismissalRepository` | |
| 59 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/DismissAnnouncement/DismissAnnouncementValidator.cs` | FluentValidation for DismissAnnouncementCommand | Validates announcement ID and user ID | `AbstractValidator<>` | |
| 60 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/PublishAnnouncement/PublishAnnouncementCommand.cs` | Command + handler to publish a draft announcement | Loads announcement, calls Publish(), saves | `IAnnouncementRepository`, `TimeProvider` | |
| 61 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/PublishAnnouncement/PublishAnnouncementValidator.cs` | FluentValidation for PublishAnnouncementCommand | Validates announcement ID is not empty | `AbstractValidator<>` | |
| 62 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/PublishChangelogEntry/PublishChangelogEntryCommand.cs` | Command + handler to publish a changelog entry | Loads entry, calls Publish(), saves | `IChangelogRepository` | |
| 63 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/PublishChangelogEntry/PublishChangelogEntryValidator.cs` | FluentValidation for PublishChangelogEntryCommand | Validates changelog entry ID is not empty | `AbstractValidator<>` | |
| 64 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/UpdateAnnouncement/UpdateAnnouncementCommand.cs` | Command + handler to update an existing announcement | Loads announcement, applies changes, saves | `IAnnouncementRepository`, `AnnouncementDto` | |
| 65 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Commands/UpdateAnnouncement/UpdateAnnouncementValidator.cs` | FluentValidation for UpdateAnnouncementCommand | Validates ID, title, content, type fields | `AbstractValidator<>` | |

### Announcements / DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 66 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/DTOs/AnnouncementDto.cs` | Data transfer object for announcement data | Record with announcement fields for command/query payloads | None | |
| 67 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/DTOs/ChangelogEntryDto.cs` | Data transfer object for changelog entry data | Record with version, title, items for command payloads | None | |

### Announcements / Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 68 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Interfaces/IAnnouncementDismissalRepository.cs` | Repository interface for announcement dismissals | CRUD operations for AnnouncementDismissal entities | Domain entities | |
| 69 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Interfaces/IAnnouncementRepository.cs` | Repository interface for announcements | CRUD + query operations for Announcement entities | Domain entities | |
| 70 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Interfaces/IChangelogRepository.cs` | Repository interface for changelog entries | CRUD operations for ChangelogEntry entities | Domain entities | |

### Announcements / Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 71 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Queries/GetActiveAnnouncements/GetActiveAnnouncementsQuery.cs` | Query + handler for active announcements visible to user | Filters by published status, tenant, targeting, dismissals | `IAnnouncementRepository`, `IAnnouncementDismissalRepository` | |
| 72 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Queries/GetAllAnnouncements/GetAllAnnouncementsQuery.cs` | Query + handler for all announcements (admin) | Returns all announcements for tenant regardless of status | `IAnnouncementRepository` | |
| 73 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Queries/GetChangelog/GetChangelogQuery.cs` | Query + handler for paginated changelog | Returns changelog entries ordered by release date | `IChangelogRepository` | |
| 74 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Queries/GetChangelogEntry/GetChangelogEntryQuery.cs` | Query + handler for single changelog entry by ID | Returns changelog entry with items | `IChangelogRepository` | |

### Announcements / Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 75 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Announcements/Services/AnnouncementTargetingService.cs` | Service to evaluate announcement targeting rules | Checks if user matches target criteria (all, role, specific user) | `AnnouncementTarget` enum | |

### Channels / Email / Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 76 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Commands/SendEmail/SendEmailCommand.cs` | Command record for sending an email | Carries to, subject, body, notification type | None | |
| 77 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Commands/SendEmail/SendEmailHandler.cs` | Handler that sends email via provider | Creates EmailMessage, sends via IEmailProvider, updates status | `IEmailProvider`, `IEmailMessageRepository` | |
| 78 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Commands/SendEmail/SendEmailValidator.cs` | FluentValidation for SendEmailCommand | Validates email address, subject, body not empty | `AbstractValidator<>` | |
| 79 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Commands/UpdateEmailPreferences/UpdateEmailPreferencesCommand.cs` | Command record for updating email preferences | Carries user ID, notification type, enabled flag | None | |
| 80 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Commands/UpdateEmailPreferences/UpdateEmailPreferencesHandler.cs` | Handler to create/update email preference | Upserts email preference for user + notification type | `IEmailPreferenceRepository` | |
| 81 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Commands/UpdateEmailPreferences/UpdateEmailPreferencesValidator.cs` | FluentValidation for UpdateEmailPreferencesCommand | Validates user ID and notification type | `AbstractValidator<>` | |

### Channels / Email / DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 82 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/DTOs/EmailDto.cs` | Data transfer object for email data | Record with email fields for internal transfer | None | |
| 83 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/DTOs/EmailPreferenceDto.cs` | Data transfer object for email preference data | Record with preference fields | None | |

### Channels / Email / EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 84 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/EventHandlers/PasswordResetRequestedEventHandler.cs` | Handles cross-module PasswordResetRequestedEvent | Renders password reset email template, dispatches SendEmailCommand | `IMessageBus`, `IEmailTemplateService` | |
| 85 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/EventHandlers/SendEmailRequestedEventHandler.cs` | Handles cross-module SendEmailRequestedEvent | Bridges Shared.Contracts event to internal SendEmailCommand | `IMessageBus` | |
| 86 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/EventHandlers/UserRegisteredEventHandler.cs` | Handles cross-module UserRegisteredEvent for welcome email | Renders welcome email template, dispatches SendEmailCommand | `IMessageBus`, `IEmailTemplateService` | |

### Channels / Email / Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 87 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Extensions/ApplicationExtensions.cs` | DI registration for email channel application services | Registers email-specific services in IServiceCollection | `IServiceCollection` | |

### Channels / Email / Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 88 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Interfaces/IEmailMessageRepository.cs` | Repository interface for email messages | CRUD + query operations for EmailMessage entities | Domain entities | |
| 89 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Interfaces/IEmailPreferenceRepository.cs` | Repository interface for email preferences | CRUD operations for EmailPreference entities | Domain entities | |
| 90 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Interfaces/IEmailProvider.cs` | Abstraction for email sending providers | SendAsync method for provider implementations (SMTP, etc.) | None | |
| 91 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Interfaces/IEmailTemplateService.cs` | Abstraction for email template rendering | RenderTemplate method for generating email HTML | None | |

### Channels / Email / Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 92 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Mappings/EmailMappings.cs` | Extension methods mapping EmailMessage to EmailDto | ToDto() mapping from domain entity to DTO | `EmailMessage`, `EmailDto` | |

### Channels / Email / Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 93 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Queries/GetEmailPreferences/GetEmailPreferencesHandler.cs` | Handler to retrieve user email preferences | Loads all preferences for a user from repository | `IEmailPreferenceRepository` | |
| 94 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Queries/GetEmailPreferences/GetEmailPreferencesQuery.cs` | Query record for email preferences lookup | Carries UserId | None | |

### Channels / Email / Telemetry

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 95 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Email/Telemetry/EmailModuleTelemetry.cs` | OpenTelemetry metrics for email operations | Counters for emails sent, failed, retried | `System.Diagnostics.Metrics` | |

### Channels / InApp / Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 96 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/ArchiveNotification/ArchiveNotificationCommand.cs` | Command record to archive a notification | Carries notification ID and user ID | None | |
| 97 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/ArchiveNotification/ArchiveNotificationHandler.cs` | Handler to archive a notification | Loads notification, calls Archive(), saves | `INotificationRepository` | |
| 98 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadCommand.cs` | Command record to mark all notifications as read | Carries user ID | None | |
| 99 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadHandler.cs` | Handler to batch-mark all user notifications as read | Bulk update via repository | `INotificationRepository` | |
| 100 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/MarkNotificationRead/MarkNotificationReadCommand.cs` | Command record to mark single notification as read | Carries notification ID and user ID | None | |
| 101 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/MarkNotificationRead/MarkNotificationReadHandler.cs` | Handler to mark single notification as read | Loads notification, calls MarkAsRead(), saves | `INotificationRepository` | |
| 102 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/SendNotification/SendNotificationCommand.cs` | Command record to send an in-app notification | Carries user ID, type, title, message | None | |
| 103 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/SendNotification/SendNotificationHandler.cs` | Handler to create and deliver in-app notification | Creates Notification entity, saves, pushes via INotificationService | `INotificationRepository`, `INotificationService` | |
| 104 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Commands/SendNotification/SendNotificationValidator.cs` | FluentValidation for SendNotificationCommand | Validates user ID, title, message not empty | `AbstractValidator<>` | |

### Channels / InApp / DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 105 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/DTOs/NotificationDto.cs` | Data transfer object for notification data | Record with notification fields | None | |

### Channels / InApp / EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 106 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/EventHandlers/AnnouncementPublishedEventHandler.cs` | Creates in-app notifications when announcement is published | Dispatches SendNotificationCommand for targeted users | `IMessageBus` | |
| 107 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/EventHandlers/UserRegisteredEventHandler.cs` | Creates welcome notification for newly registered users | Dispatches SendNotificationCommand with welcome message | `IMessageBus` | |

### Channels / InApp / Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 108 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Extensions/ApplicationExtensions.cs` | DI registration for in-app notification application services | Registers in-app-specific services in IServiceCollection | `IServiceCollection` | |

### Channels / InApp / Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 109 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Interfaces/INotificationRepository.cs` | Repository interface for notifications | CRUD + query operations for Notification entities | Domain entities | |
| 110 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Interfaces/INotificationService.cs` | Abstraction for real-time notification delivery | SendAsync for pushing notifications (e.g. SignalR) | None | |

### Channels / InApp / Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 111 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Mappings/NotificationMappings.cs` | Extension methods mapping Notification to NotificationDto | ToDto() mapping from domain entity to DTO | `Notification`, `NotificationDto` | |

### Channels / InApp / Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 112 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Queries/GetUnreadCount/GetUnreadCountHandler.cs` | Handler to get unread notification count for user | Queries repository for count of unread notifications | `INotificationRepository` | |
| 113 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Queries/GetUnreadCount/GetUnreadCountQuery.cs` | Query record for unread notification count | Carries UserId | None | |
| 114 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Queries/GetUserNotifications/GetUserNotificationsHandler.cs` | Handler to get paginated notifications for user | Queries repository with pagination params | `INotificationRepository` | |
| 115 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Queries/GetUserNotifications/GetUserNotificationsQuery.cs` | Query record for user notifications | Carries UserId, page, pageSize | None | |

### Channels / InApp / Telemetry

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 116 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/InApp/Telemetry/NotificationsModuleTelemetry.cs` | OpenTelemetry metrics for notification operations | Counters for notifications sent, read, archived | `System.Diagnostics.Metrics` | |

### Channels / Sms / Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 117 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Sms/Commands/SendSms/SendSmsCommand.cs` | Command record for sending an SMS | Carries phone number and body | None | |
| 118 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Sms/Commands/SendSms/SendSmsHandler.cs` | Handler that sends SMS via provider | Creates SmsMessage, sends via ISmsProvider, updates status | `ISmsProvider`, `ISmsMessageRepository` | |
| 119 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Sms/Commands/SendSms/SendSmsValidator.cs` | FluentValidation for SendSmsCommand | Validates phone number format, body not empty, body length | `AbstractValidator<>` | |

### Channels / Sms / EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 120 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Sms/EventHandlers/SendSmsRequestedEventHandler.cs` | Handles cross-module SendSmsRequestedEvent | Bridges Shared.Contracts event to internal SendSmsCommand | `IMessageBus` | |

### Channels / Sms / Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 121 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Sms/Interfaces/ISmsMessageRepository.cs` | Repository interface for SMS messages | CRUD operations for SmsMessage entities | Domain entities | |
| 122 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Channels/Sms/Interfaces/ISmsProvider.cs` | Abstraction for SMS sending providers | SendAsync method for provider implementations (Twilio, etc.) | None | |

### EventHandlers (Root)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 123 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/EventHandlers/InquirySubmittedEventHandler.cs` | Handles cross-module InquirySubmittedEvent | Sends notification email when inquiry form is submitted | `IMessageBus` | |

### Extensions (Root)

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 124 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Extensions/ApplicationExtensions.cs` | DI registration for all application layer services | Registers all sub-channel extensions, validators, services | `IServiceCollection` | |

### Messaging / Commands

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 125 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/CreateConversation/CreateConversationCommand.cs` | Command record to create a conversation | Carries participant IDs and optional subject | None | |
| 126 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/CreateConversation/CreateConversationHandler.cs` | Handler to create a direct or group conversation | Calls Conversation.CreateDirect/CreateGroup, saves | `IConversationRepository` | |
| 127 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/CreateConversation/CreateConversationValidator.cs` | FluentValidation for CreateConversationCommand | Validates participant list not empty, min 2 participants | `AbstractValidator<>` | |
| 128 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/MarkConversationRead/MarkConversationReadCommand.cs` | Command record to mark conversation as read | Carries conversation ID and user ID | None | |
| 129 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/MarkConversationRead/MarkConversationReadHandler.cs` | Handler to mark a conversation as read for user | Updates participant read timestamp | `IConversationRepository` | |
| 130 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/SendMessage/SendMessageCommand.cs` | Command record to send a message in a conversation | Carries conversation ID, sender ID, body | None | |
| 131 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/SendMessage/SendMessageHandler.cs` | Handler to send a message in a conversation | Loads conversation, calls SendMessage(), saves | `IConversationRepository` | |
| 132 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Commands/SendMessage/SendMessageValidator.cs` | FluentValidation for SendMessageCommand | Validates conversation ID, sender ID, body not empty | `AbstractValidator<>` | |

### Messaging / DTOs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 133 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/DTOs/ConversationDto.cs` | Data transfer object for conversation data | Record with conversation fields, participants, last message | None | |
| 134 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/DTOs/MessageDto.cs` | Data transfer object for message data | Record with message fields | None | |
| 135 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/DTOs/ParticipantDto.cs` | Data transfer object for participant data | Record with participant fields | None | |

### Messaging / EventHandlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 136 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/EventHandlers/ConversationCreatedEventHandler.cs` | Handles ConversationCreatedDomainEvent | Publishes integration event for cross-module notification | `IMessageBus` | |
| 137 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/EventHandlers/MessageSentEventHandler.cs` | Handles MessageSentDomainEvent | Publishes integration event, triggers push notifications | `IMessageBus` | |

### Messaging / Interfaces

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 138 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Interfaces/IConversationRepository.cs` | Repository interface for conversations | CRUD operations for Conversation aggregate | Domain entities | |
| 139 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Interfaces/IMessagingQueryService.cs` | Dapper-based read query interface for messaging | Paginated inbox, cursor-based messages, unread counts | None | |

### Messaging / Queries

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 140 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Queries/GetConversations/GetConversationsHandler.cs` | Handler to get user conversations (inbox) | Uses IMessagingQueryService for paginated Dapper reads | `IMessagingQueryService` | |
| 141 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Queries/GetConversations/GetConversationsQuery.cs` | Query record for user conversations | Carries user ID and pagination params | None | |
| 142 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Queries/GetMessages/GetMessagesHandler.cs` | Handler to get messages in a conversation | Uses IMessagingQueryService for cursor-based Dapper reads | `IMessagingQueryService` | |
| 143 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Queries/GetMessages/GetMessagesQuery.cs` | Query record for conversation messages | Carries conversation ID, cursor, limit | None | |
| 144 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Queries/GetUnreadConversationCount/GetUnreadConversationCountHandler.cs` | Handler to get unread conversation count for user | Uses IMessagingQueryService for unread count | `IMessagingQueryService` | |
| 145 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Messaging/Queries/GetUnreadConversationCount/GetUnreadConversationCountQuery.cs` | Query record for unread conversation count | Carries UserId | None | |

### Preferences

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 146 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Preferences/Commands/SetChannelPreferenceCommand.cs` | Command + handler to set a channel preference | Creates/updates ChannelPreference for user + channel type | `IChannelPreferenceRepository` | |
| 147 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Preferences/DTOs/ChannelPreferenceDto.cs` | Data transfer object for channel preference data | Record with preference fields | None | |
| 148 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Preferences/Interfaces/IChannelPreferenceRepository.cs` | Repository interface for channel preferences | CRUD operations for ChannelPreference entities | Domain entities | |
| 149 | [ ] | `src/Modules/Communications/Foundry.Communications.Application/Preferences/Queries/GetChannelPreferencesQuery.cs` | Query + handler for user channel preferences | Loads all channel preferences for a user | `IChannelPreferenceRepository` | |

---

## Infrastructure Layer

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 150 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Extensions/CommunicationsModuleExtensions.cs` | Module DI registration and configuration | Registers DbContext, repositories, providers, jobs, Polly resilience | `IServiceCollection`, EF Core, Polly | |

### Jobs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 151 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Jobs/RetryFailedEmailsJob.cs` | Background job to retry failed email deliveries | Loads failed emails, re-sends via IEmailService, updates status | `IEmailMessageRepository`, `IEmailService` | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 152 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260302023424_AddNotificationEnhancements.cs` | EF migration: notification enhancements | Adds columns/indexes for enhanced notification support | EF Core migration | |
| 153 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260302023424_AddNotificationEnhancements.Designer.cs` | Auto-generated designer file for notification migration | EF Core snapshot metadata | EF Core migration | |
| 154 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260302051451_AddChannelPreference.cs` | EF migration: channel preference table | Creates ChannelPreference table in communications schema | EF Core migration | |
| 155 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260302051451_AddChannelPreference.Designer.cs` | Auto-generated designer file for channel preference migration | EF Core snapshot metadata | EF Core migration | |
| 156 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260302060317_AddMessaging.cs` | EF migration: messaging tables | Creates Conversation, Message, Participant tables | EF Core migration | |
| 157 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260302060317_AddMessaging.Designer.cs` | Auto-generated designer file for messaging migration | EF Core snapshot metadata | EF Core migration | |
| 158 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260302065916_AddSmsMessageColumns.cs` | EF migration: SMS message columns | Adds SMS-specific columns to communications schema | EF Core migration | |
| 159 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260302065916_AddSmsMessageColumns.Designer.cs` | Auto-generated designer file for SMS migration | EF Core snapshot metadata | EF Core migration | |
| 160 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260305025100_SyncModelChanges.cs` | EF migration: sync model changes | Aligns DB schema with latest domain model changes | EF Core migration | |
| 161 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/20260305025100_SyncModelChanges.Designer.cs` | Auto-generated designer file for sync migration | EF Core snapshot metadata | EF Core migration | |
| 162 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Migrations/CommunicationsDbContextModelSnapshot.cs` | EF Core model snapshot for communications schema | Current state of all entity configurations | EF Core migration | |

### Persistence / DbContext

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 163 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/CommunicationsDbContext.cs` | EF Core DbContext for communications schema | Configures all entity sets, applies configurations, sets schema | EF Core, `communications` schema | |

### Persistence / Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 164 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/AnnouncementConfiguration.cs` | EF entity configuration for Announcement | Table mapping, column types, indexes, value conversions | `IEntityTypeConfiguration<Announcement>` | |
| 165 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/AnnouncementDismissalConfiguration.cs` | EF entity configuration for AnnouncementDismissal | Table mapping, foreign key to Announcement, unique constraint | `IEntityTypeConfiguration<AnnouncementDismissal>` | |
| 166 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/ChangelogEntryConfiguration.cs` | EF entity configuration for ChangelogEntry | Table mapping, owns ChangelogItem collection | `IEntityTypeConfiguration<ChangelogEntry>` | |
| 167 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/ChangelogItemConfiguration.cs` | EF entity configuration for ChangelogItem | Table mapping, foreign key to ChangelogEntry | `IEntityTypeConfiguration<ChangelogItem>` | |
| 168 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/ChannelPreferenceConfiguration.cs` | EF entity configuration for ChannelPreference | Table mapping, unique constraint on user+channel | `IEntityTypeConfiguration<ChannelPreference>` | |
| 169 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/ConversationConfiguration.cs` | EF entity configuration for Conversation | Table mapping, owns Participants and Messages | `IEntityTypeConfiguration<Conversation>` | |
| 170 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/EmailMessageConfiguration.cs` | EF entity configuration for EmailMessage | Table mapping, value object conversions for EmailAddress | `IEntityTypeConfiguration<EmailMessage>` | |
| 171 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/EmailPreferenceConfiguration.cs` | EF entity configuration for EmailPreference | Table mapping, unique constraint on user+type | `IEntityTypeConfiguration<EmailPreference>` | |
| 172 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/MessageConfiguration.cs` | EF entity configuration for Message | Table mapping, foreign key to Conversation | `IEntityTypeConfiguration<Message>` | |
| 173 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs` | EF entity configuration for Notification | Table mapping, indexes on user+read status | `IEntityTypeConfiguration<Notification>` | |
| 174 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/ParticipantConfiguration.cs` | EF entity configuration for Participant | Table mapping, foreign key to Conversation | `IEntityTypeConfiguration<Participant>` | |
| 175 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/SmsMessageConfiguration.cs` | EF entity configuration for SmsMessage | Table mapping, value object conversion for PhoneNumber | `IEntityTypeConfiguration<SmsMessage>` | |
| 176 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Configurations/SmsPreferenceConfiguration.cs` | EF entity configuration for SmsPreference | Table mapping, unique constraint on user+type | `IEntityTypeConfiguration<SmsPreference>` | |

### Persistence / Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 177 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/AnnouncementDismissalRepository.cs` | EF Core repository for AnnouncementDismissal | Implements IAnnouncementDismissalRepository with DbContext | `CommunicationsDbContext` | |
| 178 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/AnnouncementRepository.cs` | EF Core repository for Announcement | Implements IAnnouncementRepository with DbContext | `CommunicationsDbContext` | |
| 179 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/ChangelogRepository.cs` | EF Core repository for ChangelogEntry | Implements IChangelogRepository with DbContext | `CommunicationsDbContext` | |
| 180 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/ChannelPreferenceRepository.cs` | EF Core repository for ChannelPreference | Implements IChannelPreferenceRepository with DbContext | `CommunicationsDbContext` | |
| 181 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/ConversationRepository.cs` | EF Core repository for Conversation | Implements IConversationRepository with eager loading of participants/messages | `CommunicationsDbContext` | |
| 182 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/EmailMessageRepository.cs` | EF Core repository for EmailMessage | Implements IEmailMessageRepository with DbContext | `CommunicationsDbContext` | |
| 183 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/EmailPreferenceRepository.cs` | EF Core repository for EmailPreference | Implements IEmailPreferenceRepository with DbContext | `CommunicationsDbContext` | |
| 184 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/NotificationRepository.cs` | EF Core repository for Notification | Implements INotificationRepository with pagination support | `CommunicationsDbContext` | |
| 185 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Persistence/Repositories/SmsMessageRepository.cs` | EF Core repository for SmsMessage | Implements ISmsMessageRepository with DbContext | `CommunicationsDbContext` | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 186 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/EmailProviderAdapter.cs` | Adapter wrapping IEmailProvider with resilience | Adds Polly retry/timeout policies around email sending | `IEmailProvider`, Polly | |
| 187 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/MessagingQueryService.cs` | Dapper-based query service for messaging reads | Paginated inbox, cursor-based message history, unread counts via raw SQL | Dapper, `IDbConnection` | |
| 188 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/NullSmsProvider.cs` | No-op SMS provider for development/testing | Logs message instead of sending; implements ISmsProvider | `ISmsProvider`, `ILogger` | |
| 189 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/SignalRNotificationService.cs` | SignalR-based real-time notification delivery | Pushes notifications to connected clients via hub | `IHubContext`, `INotificationService` | |
| 190 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/SimpleEmailTemplateService.cs` | Basic email template rendering service | Replaces placeholders in HTML templates | `IEmailTemplateService` | |
| 191 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/SmtpConnectionPool.cs` | Connection pool for SMTP clients | Manages reusable SMTP connections for performance | `SmtpClient`, connection pooling | |
| 192 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/SmtpEmailProvider.cs` | SMTP-based email provider implementation | Sends emails via SMTP using MailKit/SmtpClient | `IEmailProvider`, `SmtpSettings` | |
| 193 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/SmtpSettings.cs` | Configuration POCO for SMTP settings | Host, port, username, password, SSL settings | Options pattern | |
| 194 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/TwilioSettings.cs` | Configuration POCO for Twilio SMS settings | AccountSid, AuthToken, FromNumber settings | Options pattern | |
| 195 | [ ] | `src/Modules/Communications/Foundry.Communications.Infrastructure/Services/TwilioSmsProvider.cs` | Twilio-based SMS provider implementation | Sends SMS via Twilio REST API | `ISmsProvider`, `TwilioSettings` | |

---

## Api Layer

### Contracts / Announcements

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 196 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Announcements/Responses/AnnouncementResponse.cs` | API response records for announcements and changelog | AnnouncementResponse, ChangelogEntryResponse, ChangelogItemResponse records | None | |

### Contracts / Email

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 197 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Email/Enums/ApiNotificationType.cs` | API-layer notification type enum | TaskAssigned, TaskCompleted, BillingInvoice, SystemNotification | None | |
| 198 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Email/Requests/UpdateEmailPreferenceRequest.cs` | API request record for updating email preferences | Carries ApiNotificationType and IsEnabled flag | `ApiNotificationType` | |
| 199 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Email/Responses/EmailPreferenceResponse.cs` | API response record for email preferences | Record with Id, UserId, NotificationType, IsEnabled, timestamps | `ApiNotificationType` | |

### Contracts / InApp

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 200 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/InApp/Responses/NotificationResponse.cs` | API response record for notifications | Record with Id, UserId, Type, Title, Message, IsRead, timestamps | None | |
| 201 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/InApp/Responses/PagedNotificationResponse.cs` | API response record for paginated notifications | Record with Items, TotalCount, PageNumber, PageSize, navigation flags | `NotificationResponse` | |
| 202 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/InApp/Responses/UnreadCountResponse.cs` | API response record for unread notification count | Record with Count property | None | |

### Contracts / Messaging

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 203 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Messaging/Requests/CreateConversationRequest.cs` | API request record for creating a conversation | Carries ParticipantIds list and optional Subject | None | |
| 204 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Messaging/Requests/SendMessageRequest.cs` | API request record for sending a message | Carries Body text | None | |
| 205 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Messaging/Responses/ConversationResponse.cs` | API response record for conversations | Record with Id, Type, Participants, LastMessage, UnreadCount | `ParticipantDto`, `MessageDto` | |
| 206 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Messaging/Responses/MessagePageResponse.cs` | API response record for paginated messages | Record with Items, NextCursor, HasMore | `MessageResponse` | |
| 207 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Messaging/Responses/MessageResponse.cs` | API response record for a message | Record with Id, ConversationId, SenderId, Body, Status, SentAt | None | |
| 208 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Contracts/Messaging/Responses/UnreadCountResponse.cs` | API response record for unread conversation count | Record with Count property | None | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 209 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Controllers/AdminAnnouncementsController.cs` | Admin endpoints for announcement management | CRUD + publish/archive; requires AnnouncementManage permission; HTML sanitization | `IMessageBus`, `IHtmlSanitizationService` | |
| 210 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Controllers/AdminChangelogController.cs` | Admin endpoints for changelog management | Create + publish changelog entries; requires ChangelogManage permission | `IMessageBus`, `IHtmlSanitizationService` | |
| 211 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Controllers/AnnouncementsController.cs` | User-facing endpoints for announcements | Get active announcements, dismiss; tenant-scoped, user-targeted | `IMessageBus`, `ITenantContext`, `ICurrentUserService` | |
| 212 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Controllers/ChangelogController.cs` | Public endpoints for changelog viewing | Get changelog list, get single entry; AllowAnonymous | `IMessageBus` | |
| 213 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Controllers/ConversationsController.cs` | Endpoints for user-to-user messaging | Create conversation, send message, get inbox, mark read, unread count | `IMessageBus`, `ICurrentUserService`, `IHtmlSanitizationService` | |
| 214 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Controllers/EmailPreferencesController.cs` | Endpoints for email preference management | Get/update email preferences for current user | `IMessageBus`, `ICurrentUserService` | |
| 215 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Controllers/NotificationsController.cs` | Endpoints for in-app notification management | Get notifications (paged), mark read, mark all read, get unread count | `IMessageBus`, `ICurrentUserService` | |

### Mappings

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|-------------|------------|
| 216 | [ ] | `src/Modules/Communications/Foundry.Communications.Api/Mappings/EnumMappings.cs` | Bidirectional mapping between API and Domain enums | ToApi()/ToDomain() extension methods for NotificationType | `ApiNotificationType`, `NotificationType` | |

---

## Test Files

### Api / Contracts

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 217 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Contracts/RequestContractTests.cs` | Tests API request contract shapes | Verifies request records have expected properties and defaults | |
| 218 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Contracts/ResponseContractTests.cs` | Tests API response contract shapes | Verifies response records have expected properties and defaults | |

### Api / Controllers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 219 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Controllers/AdminAnnouncementsControllerTests.cs` | Tests admin announcement controller | CRUD, publish, archive endpoints with auth and validation | |
| 220 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Controllers/AdminChangelogControllerTests.cs` | Tests admin changelog controller | Create, publish changelog endpoints with auth | |
| 221 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Controllers/AnnouncementsControllerTests.cs` | Tests user-facing announcement controller | Get active, dismiss endpoints with tenant/user context | |
| 222 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Controllers/ChangelogControllerTests.cs` | Tests public changelog controller | Get changelog list and single entry endpoints | |
| 223 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Controllers/EmailPreferencesControllerTests.cs` | Tests email preferences controller | Get/update preference endpoints | |
| 224 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Controllers/NotificationsControllerTests.cs` | Tests notifications controller | Get paged, mark read, mark all read, unread count endpoints | |

### Api / Mappings

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 225 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Api/Mappings/EnumMappingsTests.cs` | Tests enum mapping extensions | ToApi()/ToDomain() roundtrip and edge cases | |

### Application / Announcements / Handlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 226 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Handlers/ArchiveAnnouncementHandlerTests.cs` | Tests ArchiveAnnouncementHandler | Archive success, not found, already archived scenarios | |
| 227 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Handlers/CreateAnnouncementHandlerTests.cs` | Tests CreateAnnouncementHandler | Create with valid data, repository save verification | |
| 228 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Handlers/CreateChangelogEntryHandlerTests.cs` | Tests CreateChangelogEntryHandler | Create with items, repository save verification | |
| 229 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Handlers/CreateChangelogEntryMappingTests.cs` | Tests changelog entry mapping logic | DTO to domain entity mapping accuracy | |
| 230 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Handlers/DismissAnnouncementHandlerTests.cs` | Tests DismissAnnouncementHandler | Dismiss success, duplicate dismissal handling | |
| 231 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Handlers/PublishAnnouncementHandlerTests.cs` | Tests PublishAnnouncementHandler | Publish draft, not found, already published scenarios | |
| 232 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Handlers/PublishChangelogEntryHandlerTests.cs` | Tests PublishChangelogEntryHandler | Publish changelog entry lifecycle | |
| 233 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Handlers/UpdateAnnouncementHandlerTests.cs` | Tests UpdateAnnouncementHandler | Update fields, not found scenarios | |

### Application / Announcements / Queries

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 234 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Queries/ChangelogMappingTests.cs` | Tests changelog query mapping | Domain to DTO mapping for changelog queries | |
| 235 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Queries/GetActiveAnnouncementsHandlerTests.cs` | Tests GetActiveAnnouncementsHandler | Filtering by status, targeting, dismissals | |
| 236 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Queries/GetAllAnnouncementsHandlerTests.cs` | Tests GetAllAnnouncementsHandler | Returns all announcements for admin view | |
| 237 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Queries/GetChangelogEntryHandlerTests.cs` | Tests GetChangelogEntryHandler | Single entry retrieval with items | |
| 238 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Queries/GetChangelogHandlerTests.cs` | Tests GetChangelogHandler | Paginated changelog retrieval | |

### Application / Announcements / Services

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 239 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Services/AnnouncementTargetingEdgeCaseTests.cs` | Tests targeting service edge cases | Null/empty targets, boundary conditions | |
| 240 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Services/AnnouncementTargetingServiceTests.cs` | Tests AnnouncementTargetingService | All/Role/User targeting evaluation | |

### Application / Announcements / Validators

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 241 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Validators/CreateAnnouncementValidatorTests.cs` | Tests CreateAnnouncementValidator | Required fields, length limits, enum validation | |
| 242 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Validators/CreateChangelogEntryValidatorTests.cs` | Tests CreateChangelogEntryValidator | Version format, required fields, items validation | |
| 243 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Validators/SimpleValidatorTests.cs` | Tests simple announcement validators | Archive, Dismiss, Publish validators with ID validation | |
| 244 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Announcements/Validators/UpdateAnnouncementValidatorTests.cs` | Tests UpdateAnnouncementValidator | Required fields, length limits for updates | |

### Application / Channels / Email / EventHandlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 245 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/EventHandlers/PasswordResetRequestedEventHandlerTests.cs` | Tests PasswordResetRequestedEventHandler | Template rendering, email dispatch for password reset | |
| 246 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/EventHandlers/SendEmailRequestedEventHandlerTests.cs` | Tests SendEmailRequestedEventHandler | Cross-module event bridging to SendEmailCommand | |
| 247 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/EventHandlers/UserRegisteredEmailEventHandlerTests.cs` | Tests UserRegisteredEventHandler (email) | Welcome email template rendering and dispatch | |

### Application / Channels / Email / Handlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 248 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/Handlers/SendEmailHandlerTests.cs` | Tests SendEmailHandler | Email creation, provider send, status updates, failure handling | |
| 249 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/Handlers/UpdateEmailPreferencesHandlerTests.cs` | Tests UpdateEmailPreferencesHandler | Create new preference, update existing preference | |

### Application / Channels / Email / Mappings

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 250 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/Mappings/EmailMappingsTests.cs` | Tests EmailMappings extensions | EmailMessage to EmailDto mapping accuracy | |

### Application / Channels / Email / Queries

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 251 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/Queries/GetEmailPreferencesHandlerTests.cs` | Tests GetEmailPreferencesHandler | Preference retrieval for user, empty results | |

### Application / Channels / Email / Validators

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 252 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/Validators/SendEmailValidatorTests.cs` | Tests SendEmailValidator | Email format, required subject/body validation | |
| 253 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Email/Validators/UpdateEmailPreferencesValidatorTests.cs` | Tests UpdateEmailPreferencesValidator | User ID and notification type validation | |

### Application / Channels / InApp / EventHandlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 254 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/EventHandlers/AnnouncementPublishedEventHandlerTests.cs` | Tests AnnouncementPublishedEventHandler | Notification dispatch on announcement publish | |
| 255 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/EventHandlers/AnnouncementPublishedMarkdownTests.cs` | Tests markdown rendering for announcement notifications | Markdown content formatting in notifications | |
| 256 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/EventHandlers/UserRegisteredEventHandlerTests.cs` | Tests UserRegisteredEventHandler (in-app) | Welcome notification creation on registration | |

### Application / Channels / InApp / Handlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 257 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/Handlers/ArchiveNotificationHandlerTests.cs` | Tests ArchiveNotificationHandler | Archive success, not found, already archived | |
| 258 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/Handlers/MarkAllNotificationsReadHandlerTests.cs` | Tests MarkAllNotificationsReadHandler | Bulk mark read, empty notifications | |
| 259 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/Handlers/MarkNotificationReadHandlerTests.cs` | Tests MarkNotificationReadHandler | Single mark read, not found, already read | |
| 260 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/Handlers/SendNotificationHandlerTests.cs` | Tests SendNotificationHandler | Create notification, push via service, repository save | |

### Application / Channels / InApp / Mappings

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 261 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/Mappings/NotificationMappingsTests.cs` | Tests NotificationMappings extensions | Notification to NotificationDto mapping accuracy | |

### Application / Channels / InApp / Queries

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 262 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/Queries/GetUnreadCountHandlerTests.cs` | Tests GetUnreadCountHandler | Unread count for user, zero count | |
| 263 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/Queries/GetUserNotificationsHandlerTests.cs` | Tests GetUserNotificationsHandler | Paginated retrieval, empty results | |

### Application / Channels / InApp / Validators

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 264 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/InApp/Validators/SendNotificationValidatorTests.cs` | Tests SendNotificationValidator | Required user ID, title, message validation | |

### Application / Channels / Sms

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 265 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Sms/SendSmsHandlerTests.cs` | Tests SendSmsHandler | SMS creation, provider send, status updates, failure handling | |
| 266 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Sms/SendSmsRequestedEventHandlerTests.cs` | Tests SendSmsRequestedEventHandler | Cross-module event bridging to SendSmsCommand | |
| 267 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Channels/Sms/SendSmsValidatorTests.cs` | Tests SendSmsValidator | Phone format, body length, required fields | |

### Application / DTOs

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 268 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/DTOs/AnnouncementDtoTests.cs` | Tests AnnouncementDto record | Construction, equality, default values | |
| 269 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/DTOs/EmailDtoTests.cs` | Tests EmailDto record | Construction, equality, default values | |

### Application / Extensions

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 270 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Extensions/ApplicationExtensionsTests.cs` | Tests root ApplicationExtensions registration | Service collection registrations | |
| 271 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Extensions/EmailApplicationExtensionsTests.cs` | Tests email ApplicationExtensions registration | Email service registrations | |
| 272 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Extensions/InAppApplicationExtensionsTests.cs` | Tests in-app ApplicationExtensions registration | In-app service registrations | |

### Application / Handlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 273 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Handlers/AnnouncementHandlerTests.cs` | Integration-style tests for announcement handlers | End-to-end handler flows for announcements | |
| 274 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Handlers/NotificationHandlerTests.cs` | Integration-style tests for notification handlers | End-to-end handler flows for notifications | |

### Application / Messaging / Commands

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 275 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Messaging/Commands/CreateConversationHandlerTests.cs` | Tests CreateConversationHandler | Direct/group creation, participant validation | |
| 276 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Messaging/Commands/MarkConversationReadHandlerTests.cs` | Tests MarkConversationReadHandler | Mark read, not found, non-participant scenarios | |
| 277 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Messaging/Commands/SendMessageHandlerTests.cs` | Tests SendMessageHandler | Send message, archived conversation rejection, non-participant | |

### Application / Messaging / EventHandlers

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 278 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Messaging/EventHandlers/MessageSentEventHandlerTests.cs` | Tests MessageSentEventHandler | Integration event publishing, notification trigger | |

### Application / Telemetry

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 279 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Telemetry/EmailModuleTelemetryTests.cs` | Tests EmailModuleTelemetry metrics | Counter increments for send, fail, retry | |
| 280 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Application/Telemetry/NotificationsModuleTelemetryTests.cs` | Tests NotificationsModuleTelemetry metrics | Counter increments for send, read, archive | |

### Channels / Email / Domain / Entities

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 281 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Channels/Email/Domain/Entities/EmailMessageTests.cs` | Tests EmailMessage aggregate root | Create, MarkAsSent, MarkAsFailed lifecycle, domain events | |

### Channels / InApp / Domain / Entities

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 282 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Channels/InApp/Domain/Entities/NotificationTests.cs` | Tests Notification aggregate root | Create, MarkAsRead, Archive lifecycle, domain events | |

### Domain / Announcements

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 283 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Announcements/AnnouncementCreateTests.cs` | Tests Announcement.Create factory | Valid creation, required fields, default values | |
| 284 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Announcements/AnnouncementDismissalCreateTests.cs` | Tests AnnouncementDismissal.Create factory | Valid creation, ID and timestamp assignment | |
| 285 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Announcements/AnnouncementStatusTests.cs` | Tests announcement status transitions | Draft->Published->Archived, invalid transitions | |
| 286 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Announcements/AnnouncementUpdateTests.cs` | Tests Announcement.Update method | Field updates, status validation for updates | |
| 287 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Announcements/ChangelogEntryTests.cs` | Tests ChangelogEntry entity | Create, Publish lifecycle, item management | |
| 288 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Announcements/ChangelogItemTests.cs` | Tests ChangelogItem entity | Create factory, description and type assignment | |

### Domain / Channels / Sms

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 289 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Channels/Sms/PhoneNumberTests.cs` | Tests PhoneNumber value object | E.164 validation, valid/invalid formats | |
| 290 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Channels/Sms/SmsMessageTests.cs` | Tests SmsMessage aggregate root | Create, lifecycle transitions, 1600-char limit | |

### Domain / Email

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 291 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Email/EmailAddressTests.cs` | Tests EmailAddress value object | Valid/invalid email format validation | |
| 292 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Email/EmailContentTests.cs` | Tests EmailContent value object | Subject and body content validation | |
| 293 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Email/EmailPreferenceTests.cs` | Tests EmailPreference entity | Create, toggle enabled/disabled | |
| 294 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Email/InvalidEmailAddressExceptionTests.cs` | Tests InvalidEmailAddressException | Exception message and properties | |

### Domain / Entities

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 295 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Entities/NotificationTests.cs` | Tests Notification entity (root domain) | Create, read, archive behavior | |

### Domain / Events

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 296 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Events/DomainEventTests.cs` | Tests domain event records | Construction, equality, property access | |

### Domain / Identity

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 297 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Identity/StronglyTypedIdTests.cs` | Tests strongly-typed ID value objects | Create, equality, GUID conversion for all ID types | |

### Domain / Messaging

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 298 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Messaging/ConversationTests.cs` | Tests Conversation aggregate root | CreateDirect/CreateGroup, SendMessage, Archive, invariant enforcement | |
| 299 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Messaging/MessageTests.cs` | Tests Message entity | Create factory, property assignment | |
| 300 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Messaging/ParticipantTests.cs` | Tests Participant entity | Create factory, read state tracking | |

### Domain / Preferences

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 301 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Domain/Preferences/ChannelPreferenceTests.cs` | Tests ChannelPreference entity | Create, toggle, channel type validation | |

### GlobalUsings

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 302 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/GlobalUsings.cs` | Global using directives for test project | Imports xUnit, NSubstitute, FluentAssertions | |

### Infrastructure / Jobs

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 303 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Jobs/RetryFailedEmailsJobTests.cs` | Tests RetryFailedEmailsJob | Retry logic, failed email loading, status updates | |

### Infrastructure / Persistence

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 304 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Persistence/CommunicationsDbContextTests.cs` | Tests CommunicationsDbContext | DbSet availability, schema configuration | |

### Infrastructure / Persistence / Repositories

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 305 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Persistence/Repositories/AnnouncementDismissalRepositoryTests.cs` | Tests AnnouncementDismissalRepository | CRUD operations against in-memory DB | |
| 306 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Persistence/Repositories/AnnouncementRepositoryTests.cs` | Tests AnnouncementRepository | CRUD + query operations against in-memory DB | |
| 307 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Persistence/Repositories/ChangelogRepositoryTests.cs` | Tests ChangelogRepository | CRUD operations against in-memory DB | |
| 308 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Persistence/Repositories/EmailMessageRepositoryTests.cs` | Tests EmailMessageRepository | CRUD operations, failed email query | |
| 309 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Persistence/Repositories/EmailPreferenceRepositoryTests.cs` | Tests EmailPreferenceRepository | CRUD operations, user preference lookup | |
| 310 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Persistence/Repositories/NotificationRepositoryTests.cs` | Tests NotificationRepository | CRUD + pagination, unread count queries | |

### Infrastructure / Services

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 311 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Services/EmailProviderAdapterTests.cs` | Tests EmailProviderAdapter | Polly resilience wrapping, retry/timeout behavior | |
| 312 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Services/SignalRNotificationServiceEdgeCaseTests.cs` | Tests SignalR service edge cases | Null user, disconnected client, hub errors | |
| 313 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Services/SignalRNotificationServiceTests.cs` | Tests SignalRNotificationService | Notification push to connected clients | |
| 314 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Services/SimpleEmailTemplateServiceEdgeCaseTests.cs` | Tests template service edge cases | Missing placeholders, null values, empty templates | |
| 315 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Services/SimpleEmailTemplateServiceTests.cs` | Tests SimpleEmailTemplateService | Template rendering with placeholder substitution | |
| 316 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Services/SmtpEmailProviderTests.cs` | Tests SmtpEmailProvider | SMTP connection, send, error handling | |
| 317 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Services/SmtpResilienceTests.cs` | Tests SMTP resilience policies | Retry, timeout, circuit breaker behavior | |
| 318 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Infrastructure/Services/SmtpSettingsTests.cs` | Tests SmtpSettings configuration | Settings binding, validation, defaults | |

### Integration / Messaging

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 319 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Integration/Messaging/ConversationIntegrationTests.cs` | Integration tests for conversation flows | End-to-end create, send, read conversation scenarios | |

### Integration / Sms

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| 320 | [ ] | `tests/Modules/Communications/Foundry.Communications.Tests/Integration/Sms/SmsIntegrationTests.cs` | Integration tests for SMS flows | End-to-end SMS send with provider scenarios | |
