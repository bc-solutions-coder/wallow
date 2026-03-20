# Phase 10: Notifications Module

**Scope:** `src/Modules/Notifications/`
**Status:** Not Started
**Files:** 190 source files, 22 test files

## How to Use This Document
- Work through files top-to-bottom within each section
- Check boxes as each file is reviewed and confirmed
- Add notes in the "Your Notes" column during review
- Mark status as: In Progress / Completed when done

---

## Domain Layer

### Email Channel

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 1 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/Entities/EmailMessage.cs` | Aggregate root for outbound email messages | Status machine: Pending→Sent/Failed; RetryCount tracking; raises EmailSentDomainEvent and EmailFailedDomainEvent; CanRetry(maxRetries=3) guard | AggregateRoot, EmailMessageId, EmailAddress, EmailContent, EmailStatus, ITenantScoped | |
| 2 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/Entities/EmailPreference.cs` | Per-user email preference aggregate | Stores user opt-in/opt-out for email notifications | AggregateRoot, EmailPreferenceId, ITenantScoped | |
| 3 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/Enums/EmailStatus.cs` | Enum for email delivery states | Pending, Sent, Failed | — | |
| 4 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/Events/EmailFailedDomainEvent.cs` | Domain event raised when email delivery fails | Carries EmailMessageId, To, FailureReason, RetryCount | IDomainEvent | |
| 5 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/Events/EmailSentDomainEvent.cs` | Domain event raised on successful email delivery | Carries EmailMessageId, To, Subject | IDomainEvent | |
| 6 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/Exceptions/InvalidEmailAddressException.cs` | Domain exception for malformed email addresses | Thrown during EmailAddress VO creation | DomainException | |
| 7 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/Identity/EmailMessageId.cs` | Strongly-typed ID for EmailMessage | Wraps Guid; New() factory | StronglyTypedId | |
| 8 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/Identity/EmailPreferenceId.cs` | Strongly-typed ID for EmailPreference | Wraps Guid; New() factory | StronglyTypedId | |
| 9 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/ValueObjects/EmailAddress.cs` | Value object validating RFC email format | Create() factory; throws InvalidEmailAddressException on bad input | ValueObject | |
| 10 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Email/ValueObjects/EmailContent.cs` | Value object for email subject + body | Create() factory; encapsulates Subject and Body as a unit | ValueObject | |

### InApp Channel

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 11 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/InApp/Entities/Notification.cs` | Aggregate root for in-app notifications | Raises NotificationCreatedDomainEvent on Create(); MarkAsRead() raises NotificationReadDomainEvent; Archive() sets IsArchived; supports ActionUrl, SourceModule, ExpiresAt | AggregateRoot, NotificationId, NotificationType, ITenantScoped | |
| 12 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/InApp/Events/NotificationCreatedDomainEvent.cs` | Domain event raised when a notification is created | Carries NotificationId, UserId, Title, Type | IDomainEvent | |
| 13 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/InApp/Events/NotificationReadDomainEvent.cs` | Domain event raised when a notification is marked read | Carries NotificationId, UserId | IDomainEvent | |
| 14 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/InApp/Identity/NotificationId.cs` | Strongly-typed ID for Notification | Wraps Guid; New() factory | StronglyTypedId | |

### Push Channel

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 15 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/DeviceRegistration.cs` | Aggregate root for a user's registered push device | Stores Token, Platform, UserId; used to fan-out push delivery per device | AggregateRoot, DeviceRegistrationId, PushPlatform, ITenantScoped | |
| 16 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Entities/PushMessage.cs` | Aggregate root for an outbound push notification | Status machine: Pending→Sent/Failed; raises PushMessageSentDomainEvent and PushMessageFailedDomainEvent | AggregateRoot, PushMessageId, PushStatus, ITenantScoped | |
| 17 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Entities/TenantPushConfiguration.cs` | Aggregate root for tenant-level push provider configuration | Stores EncryptedCredentials and Platform; Enable/Disable/UpdateCredentials methods; IsEnabled flag gates delivery | AggregateRoot, TenantPushConfigurationId, PushPlatform, ITenantScoped | |
| 18 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Enums/PushPlatform.cs` | Enum for push provider platforms | Fcm, Apns, WebPush | — | |
| 19 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Enums/PushStatus.cs` | Enum for push message delivery states | Pending, Sent, Failed | — | |
| 20 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Events/PushMessageFailedDomainEvent.cs` | Domain event raised when push delivery fails | Carries PushMessageId, FailureReason | IDomainEvent | |
| 21 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Events/PushMessageSentDomainEvent.cs` | Domain event raised on successful push delivery | Carries PushMessageId | IDomainEvent | |
| 22 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Identity/DeviceRegistrationId.cs` | Strongly-typed ID for DeviceRegistration | Wraps Guid; New() factory | StronglyTypedId | |
| 23 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Identity/PushMessageId.cs` | Strongly-typed ID for PushMessage | Wraps Guid; New() factory | StronglyTypedId | |
| 24 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Push/Identity/TenantPushConfigurationId.cs` | Strongly-typed ID for TenantPushConfiguration | Wraps Guid; New() factory | StronglyTypedId | |

### SMS Channel

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 25 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/Entities/SmsMessage.cs` | Aggregate root for outbound SMS messages | Create() validates body length (max 1600 chars); status machine Pending→Sent/Failed; RetryCount tracking; raises SmsSentDomainEvent and SmsFailedDomainEvent; CanRetry(maxRetries=3) | AggregateRoot, SmsMessageId, PhoneNumber, SmsStatus, ITenantScoped | |
| 26 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/Entities/SmsPreference.cs` | Per-user SMS preference aggregate | Stores user opt-in/opt-out for SMS notifications | AggregateRoot, SmsPreferenceId, ITenantScoped | |
| 27 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/Enums/SmsStatus.cs` | Enum for SMS delivery states | Pending, Sent, Failed | — | |
| 28 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/Events/SmsFailedDomainEvent.cs` | Domain event raised when SMS delivery fails | Carries SmsMessageId, FailureReason | IDomainEvent | |
| 29 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/Events/SmsSentDomainEvent.cs` | Domain event raised on successful SMS delivery | Carries SmsMessageId | IDomainEvent | |
| 30 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/Exceptions/InvalidPhoneNumberException.cs` | Domain exception for malformed phone numbers | Thrown during PhoneNumber VO creation | DomainException | |
| 31 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/Identity/SmsMessageId.cs` | Strongly-typed ID for SmsMessage | Wraps Guid; New() factory | StronglyTypedId | |
| 32 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/Identity/SmsPreferenceId.cs` | Strongly-typed ID for SmsPreference | Wraps Guid; New() factory | StronglyTypedId | |
| 33 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Channels/Sms/ValueObjects/PhoneNumber.cs` | Value object validating E.164 phone number format | Create() factory; throws InvalidPhoneNumberException on bad input | ValueObject | |

### Preferences

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 34 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Preferences/Entities/ChannelPreference.cs` | Aggregate root for per-user, per-channel, per-notification-type opt-in preference | Create() raises ChannelPreferenceCreatedEvent; Enable()/Disable() toggle IsEnabled; keyed on (UserId, ChannelType, NotificationType) | AggregateRoot, ChannelPreferenceId, ChannelType, ITenantScoped | |
| 35 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Preferences/Events/ChannelPreferenceCreatedEvent.cs` | Domain event raised when a channel preference is first created | Carries ChannelPreferenceId, UserId, ChannelType, NotificationType, IsEnabled | IDomainEvent | |
| 36 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Preferences/Identity/ChannelPreferenceId.cs` | Strongly-typed ID for ChannelPreference | Wraps Guid; New() factory | StronglyTypedId | |
| 37 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Preferences/ChannelType.cs` | Enum for notification delivery channels | Email, InApp, Push, Sms | — | |

### Domain Shared

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 38 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/Enums/NotificationType.cs` | Enum classifying notification categories | Announcement, Message, Invoice, Organization, System, etc. | — | |
| 39 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Domain/INotificationsDomainMarker.cs` | Assembly marker interface for the Domain project | Used by Wolverine and DI scanning | — | |

---

## Application Layer

### Channels / Email

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 40 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Commands/SendEmail/SendEmailCommand.cs` | CQRS command to send a single email | Record with To, From?, Subject, Body, UserId?, NotificationType? | — | |
| 41 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Commands/SendEmail/SendEmailHandler.cs` | Handles SendEmailCommand; checks preferences then persists and delivers | Preference check via INotificationPreferenceChecker; creates EmailMessage aggregate; calls IEmailService; marks Sent/Failed; returns EmailDto | IEmailMessageRepository, IEmailService, ITenantContext, INotificationPreferenceChecker, TimeProvider | |
| 42 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Commands/SendEmail/SendEmailValidator.cs` | FluentValidation for SendEmailCommand | Validates To format, Subject non-empty, Body non-empty | FluentValidation | |
| 43 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Commands/UpdateEmailPreferences/UpdateEmailPreferencesCommand.cs` | CQRS command to update a user's email preferences | Record with UserId and preference payload | — | |
| 44 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Commands/UpdateEmailPreferences/UpdateEmailPreferencesHandler.cs` | Handles UpdateEmailPreferencesCommand | Upserts EmailPreference aggregate in repository | IEmailPreferenceRepository, TimeProvider | |
| 45 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Commands/UpdateEmailPreferences/UpdateEmailPreferencesValidator.cs` | FluentValidation for UpdateEmailPreferencesCommand | Validates UserId present | FluentValidation | |
| 46 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/DTOs/EmailDto.cs` | DTO representing a persisted email message | Maps from EmailMessage aggregate | — | |
| 47 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/DTOs/EmailPreferenceDto.cs` | DTO representing an email preference record | Maps from EmailPreference aggregate | — | |
| 48 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Interfaces/IEmailMessageRepository.cs` | Repository interface for EmailMessage persistence | Add, GetFailedRetryableAsync, SaveChangesAsync | Domain | |
| 49 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Interfaces/IEmailPreferenceRepository.cs` | Repository interface for EmailPreference persistence | Upsert, GetByUserAsync | Domain | |
| 50 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Interfaces/IEmailProvider.cs` | Low-level email delivery abstraction | SendAsync(EmailDeliveryRequest) → EmailDeliveryResult | Shared.Contracts | |
| 51 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Interfaces/IEmailTemplateService.cs` | Template rendering abstraction | RenderAsync(templateName, model) → string | — | |
| 52 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Mappings/EmailMappings.cs` | Extension methods mapping EmailMessage to EmailDto | ToDto() | EmailMessage, EmailDto | |
| 53 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Queries/GetEmailPreferences/GetEmailPreferencesHandler.cs` | Handles GetEmailPreferencesQuery | Fetches email preferences for a user; returns list of EmailPreferenceDto | IEmailPreferenceRepository | |
| 54 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Queries/GetEmailPreferences/GetEmailPreferencesQuery.cs` | CQRS query to get email preferences for a user | Record with UserId | — | |
| 55 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Email/Telemetry/EmailModuleTelemetry.cs` | OpenTelemetry ActivitySource for email tracing | Static ActivitySource used in SmtpEmailProvider | System.Diagnostics | |

### Channels / InApp

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 56 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/ArchiveNotification/ArchiveNotificationCommand.cs` | CQRS command to archive a notification | Record with NotificationId, UserId | — | |
| 57 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/ArchiveNotification/ArchiveNotificationHandler.cs` | Handles ArchiveNotificationCommand | Loads Notification, calls Archive(), saves | INotificationRepository, TimeProvider | |
| 58 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadCommand.cs` | CQRS command to bulk-mark all unread notifications as read | Record with UserId | — | |
| 59 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadHandler.cs` | Handles MarkAllNotificationsReadCommand | Fetches unread notifications for user; calls MarkAsRead() on each; saves | INotificationRepository, TimeProvider | |
| 60 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/MarkNotificationRead/MarkNotificationReadCommand.cs` | CQRS command to mark a single notification as read | Record with NotificationId, UserId | — | |
| 61 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/MarkNotificationRead/MarkNotificationReadHandler.cs` | Handles MarkNotificationReadCommand | Loads Notification by ID, validates ownership, calls MarkAsRead() | INotificationRepository, TimeProvider | |
| 62 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/SendNotification/SendNotificationCommand.cs` | CQRS command to create an in-app notification | Record with UserId, Type, Title, Message, ActionUrl?, SourceModule?, ExpiresAt? | — | |
| 63 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/SendNotification/SendNotificationHandler.cs` | Handles SendNotificationCommand; checks preferences, persists, dispatches real-time | Creates Notification aggregate; saves via INotificationRepository; calls INotificationService.SendToUserAsync for real-time delivery | INotificationRepository, INotificationService, ITenantContext, TimeProvider | |
| 64 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Commands/SendNotification/SendNotificationValidator.cs` | FluentValidation for SendNotificationCommand | Validates UserId, Title, Message non-empty | FluentValidation | |
| 65 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/DTOs/NotificationDto.cs` | DTO representing an in-app notification | Maps from Notification aggregate; includes IsRead, ReadAt, ActionUrl | — | |
| 66 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Interfaces/INotificationRepository.cs` | Repository interface for Notification persistence | Add, GetByUserAsync (paged), GetUnreadCountAsync, MarkAllReadAsync | Domain | |
| 67 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Interfaces/INotificationService.cs` | Real-time notification delivery abstraction | SendToUserAsync, BroadcastToTenantAsync | — | |
| 68 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Mappings/NotificationMappings.cs` | Extension methods mapping Notification to NotificationDto | ToDto() | Notification, NotificationDto | |
| 69 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Queries/GetUnreadCount/GetUnreadCountHandler.cs` | Handles GetUnreadCountQuery | Returns integer count of unread notifications for a user | INotificationRepository | |
| 70 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Queries/GetUnreadCount/GetUnreadCountQuery.cs` | CQRS query for unread notification count | Record with UserId | — | |
| 71 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Queries/GetUserNotifications/GetUserNotificationsHandler.cs` | Handles GetUserNotificationsQuery | Returns paginated NotificationDto list for a user | INotificationRepository | |
| 72 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Queries/GetUserNotifications/GetUserNotificationsQuery.cs` | CQRS query for paginated user notifications | Record with UserId, PageNumber, PageSize | — | |
| 73 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/InApp/Telemetry/NotificationsModuleTelemetry.cs` | OpenTelemetry ActivitySource for in-app notification tracing | Static ActivitySource | System.Diagnostics | |

### Channels / Preferences

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 74 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Preferences/Commands/SetChannelEnabled/SetChannelEnabledCommand.cs` | CQRS command to enable/disable a channel for a user | Record with UserId, ChannelType, IsEnabled | — | |
| 75 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Preferences/Commands/SetChannelEnabled/SetChannelEnabledHandler.cs` | Handles SetChannelEnabledCommand | Updates all ChannelPreference records for user+channel combination; creates if missing | IChannelPreferenceRepository, TimeProvider | |
| 76 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Preferences/Commands/SetChannelEnabled/SetChannelEnabledValidator.cs` | FluentValidation for SetChannelEnabledCommand | Validates UserId and ChannelType | FluentValidation | |
| 77 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Preferences/DTOs/UserNotificationSettingsDto.cs` | DTO for a user's full notification settings across all channels | Aggregates ChannelPreferenceDto list | — | |
| 78 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Preferences/Queries/GetUserNotificationSettings/GetUserNotificationSettingsHandler.cs` | Handles GetUserNotificationSettingsQuery | Returns UserNotificationSettingsDto with all channel preferences for a user | IChannelPreferenceRepository | |
| 79 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Preferences/Queries/GetUserNotificationSettings/GetUserNotificationSettingsQuery.cs` | CQRS query to retrieve all notification settings for a user | Record with UserId | — | |

### Channels / Push

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 80 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/DeliverPush/DeliverPushCommand.cs` | Internal CQRS command to deliver a push to a single device | Record with PushMessageId, DeviceRegistrationId, Token, Platform | — | |
| 81 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/DeliverPush/DeliverPushHandler.cs` | Handles DeliverPushCommand; calls provider for single device | Resolves provider via IPushProviderFactory by Platform; calls SendAsync; marks PushMessage Sent/Failed | IPushProviderFactory, IPushMessageRepository, TimeProvider | |
| 82 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/DeregisterDevice/DeregisterDeviceCommand.cs` | CQRS command to remove a device registration | Record with DeviceToken, UserId | — | |
| 83 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/DeregisterDevice/DeregisterDeviceHandler.cs` | Handles DeregisterDeviceCommand; removes device record | Looks up device by token+userId; deletes via repository | IDeviceRegistrationRepository | |
| 84 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/DeregisterDevice/DeregisterDeviceValidator.cs` | FluentValidation for DeregisterDeviceCommand | Validates DeviceToken non-empty, UserId present | FluentValidation | |
| 85 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/RegisterDevice/RegisterDeviceCommand.cs` | CQRS command to register a device for push | Record with UserId, Token, Platform | — | |
| 86 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/RegisterDevice/RegisterDeviceHandler.cs` | Handles RegisterDeviceCommand; upserts DeviceRegistration | Creates or updates DeviceRegistration aggregate; saves | IDeviceRegistrationRepository, ITenantContext, TimeProvider | |
| 87 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/RegisterDevice/RegisterDeviceValidator.cs` | FluentValidation for RegisterDeviceCommand | Validates Token non-empty, Platform valid | FluentValidation | |
| 88 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/RemoveTenantPushConfig/RemoveTenantPushConfigCommand.cs` | CQRS command to delete a tenant's push configuration | Record with TenantId, Platform | — | |
| 89 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/RemoveTenantPushConfig/RemoveTenantPushConfigHandler.cs` | Handles RemoveTenantPushConfigCommand | Finds and deletes TenantPushConfiguration for tenant+platform | ITenantPushConfigurationRepository | |
| 90 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/SendPush/SendPushCommand.cs` | CQRS command to send a push notification to a user | Record with TenantId, RecipientId, Title, Body, NotificationType | — | |
| 91 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/SendPush/SendPushHandler.cs` | Handles SendPushCommand; checks preferences, fans out to devices | Preference check via INotificationPreferenceChecker; creates PushMessage; fetches active devices for user; publishes DeliverPushCommand per device | INotificationPreferenceChecker, IPushMessageRepository, IDeviceRegistrationRepository, IMessageBus, TimeProvider | |
| 92 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/SendPush/SendPushValidator.cs` | FluentValidation for SendPushCommand | Validates Title, Body, RecipientId | FluentValidation | |
| 93 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/SetTenantPushEnabled/SetTenantPushEnabledCommand.cs` | CQRS command to toggle push enabled for a tenant+platform | Record with TenantId, Platform, IsEnabled | — | |
| 94 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/SetTenantPushEnabled/SetTenantPushEnabledHandler.cs` | Handles SetTenantPushEnabledCommand | Calls Enable()/Disable() on TenantPushConfiguration aggregate | ITenantPushConfigurationRepository, TimeProvider | |
| 95 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/SetTenantPushEnabled/SetTenantPushEnabledValidator.cs` | FluentValidation for SetTenantPushEnabledCommand | Validates TenantId and Platform | FluentValidation | |
| 96 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/UpsertTenantPushConfig/UpsertTenantPushConfigCommand.cs` | CQRS command to create or update push credentials for a tenant+platform | Record with TenantId, Platform, Credentials (plaintext, encrypted in handler) | — | |
| 97 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/UpsertTenantPushConfig/UpsertTenantPushConfigHandler.cs` | Handles UpsertTenantPushConfigCommand; encrypts credentials | Encrypts via IPushCredentialEncryptor; creates or updates TenantPushConfiguration aggregate | ITenantPushConfigurationRepository, IPushCredentialEncryptor, ITenantContext, TimeProvider | |
| 98 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Commands/UpsertTenantPushConfig/UpsertTenantPushConfigValidator.cs` | FluentValidation for UpsertTenantPushConfigCommand | Validates TenantId, Platform, Credentials non-empty | FluentValidation | |
| 99 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/DTOs/DeviceRegistrationDto.cs` | DTO representing a registered device | Maps from DeviceRegistration aggregate | — | |
| 100 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/DTOs/TenantPushConfigDto.cs` | DTO representing a tenant push config (credentials redacted) | Maps from TenantPushConfiguration; credentials not exposed | — | |
| 101 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Interfaces/IDeviceRegistrationRepository.cs` | Repository interface for DeviceRegistration | Add, GetActiveByUserAsync, GetByTokenAsync, Remove, SaveChangesAsync | Domain | |
| 102 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Interfaces/IPushCredentialEncryptor.cs` | Encryption abstraction for push credentials | Encrypt(plaintext) → string; Decrypt(ciphertext) → string | — | |
| 103 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Interfaces/IPushMessageRepository.cs` | Repository interface for PushMessage | Add, GetByIdAsync, SaveChangesAsync | Domain | |
| 104 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Interfaces/IPushProvider.cs` | Push delivery abstraction per platform | SendAsync(token, title, body) → bool | — | |
| 105 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Interfaces/IPushProviderFactory.cs` | Factory to resolve IPushProvider by PushPlatform | GetProvider(PushPlatform) → IPushProvider | — | |
| 106 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Interfaces/ITenantPushConfigurationRepository.cs` | Repository interface for TenantPushConfiguration | Add, GetByPlatformAsync, GetByTenantAndPlatformAsync, Remove, SaveChangesAsync | Domain | |
| 107 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Queries/GetTenantPushConfig/GetTenantPushConfigHandler.cs` | Handles GetTenantPushConfigQuery | Returns TenantPushConfigDto (credentials redacted) for a tenant+platform | ITenantPushConfigurationRepository | |
| 108 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Queries/GetTenantPushConfig/GetTenantPushConfigQuery.cs` | CQRS query for tenant push configuration | Record with TenantId, Platform | — | |
| 109 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Queries/GetUserDevices/GetUserDevicesHandler.cs` | Handles GetUserDevicesQuery | Returns list of DeviceRegistrationDto for a user | IDeviceRegistrationRepository | |
| 110 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Push/Queries/GetUserDevices/GetUserDevicesQuery.cs` | CQRS query to list registered devices for a user | Record with UserId | — | |

### Channels / SMS

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 111 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Sms/Commands/SendSms/SendSmsCommand.cs` | CQRS command to send an SMS | Record with To, Body, UserId?, NotificationType?, From? | — | |
| 112 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Sms/Commands/SendSms/SendSmsHandler.cs` | Handles SendSmsCommand; checks preferences, persists, delivers | Preference check; creates SmsMessage aggregate; calls ISmsProvider; marks Sent/Failed | ISmsMessageRepository, ISmsProvider, ITenantContext, INotificationPreferenceChecker, TimeProvider | |
| 113 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Sms/Commands/SendSms/SendSmsValidator.cs` | FluentValidation for SendSmsCommand | Validates To phone format, Body non-empty and within max length | FluentValidation | |
| 114 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Sms/Interfaces/ISmsMessageRepository.cs` | Repository interface for SmsMessage | Add, SaveChangesAsync | Domain | |
| 115 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Channels/Sms/Interfaces/ISmsProvider.cs` | SMS delivery abstraction | SendAsync(to, from?, body) → bool | — | |

### Application / Preferences

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 116 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Preferences/Commands/SetChannelPreferenceCommand.cs` | CQRS command to set a specific channel+type preference | Record with UserId, ChannelType, NotificationType, IsEnabled | — | |
| 117 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Preferences/DTOs/ChannelPreferenceDto.cs` | DTO representing a single channel preference record | Maps from ChannelPreference aggregate | — | |
| 118 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Preferences/Interfaces/IChannelPreferenceRepository.cs` | Repository interface for ChannelPreference | Add, GetByUserAsync, GetByUserAndChannelAsync, SaveChangesAsync | Domain | |
| 119 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Preferences/Interfaces/INotificationPreferenceChecker.cs` | Service abstraction used by send handlers to gate delivery | IsChannelEnabledAsync(userId, channelType, notificationType) → bool; defaults to enabled when no preference stored | — | |
| 120 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Preferences/Queries/GetChannelPreferencesQuery.cs` | CQRS query to retrieve channel preferences for a user | Record with UserId, optional ChannelType filter | — | |

### Application / Event Handlers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 121 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/AnnouncementPublishedNotificationHandler.cs` | Handles AnnouncementPublishedEvent from Announcements module | Iterates TargetUserIds; dispatches SendNotificationCommand per user with Type=Announcement | Shared.Contracts.Announcements, IMessageBus | |
| 122 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InquiryStatusChangedNotificationHandler.cs` | Handles InquiryStatusChangedEvent from another module | Sends in-app notification to inquiry submitter about status change | Shared.Contracts, IMessageBus | |
| 123 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InquirySubmittedNotificationHandler.cs` | Handles InquirySubmittedEvent | Notifies relevant staff via in-app and/or email when a new inquiry is submitted | Shared.Contracts, IMessageBus | |
| 124 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InvoiceOverdueNotificationHandler.cs` | Handles InvoiceOverdueEvent from Billing module | Sends overdue alert via email and in-app to the invoice recipient | Shared.Contracts.Billing, IMessageBus | |
| 125 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/InvoicePaidNotificationHandler.cs` | Handles InvoicePaidEvent from Billing module | Sends payment confirmation via email and in-app | Shared.Contracts.Billing, IMessageBus | |
| 126 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/MessageSentNotificationHandler.cs` | Handles MessageSentEvent from Messaging module | Notifies recipient of new message via in-app notification | Shared.Contracts.Messaging, IMessageBus | |
| 127 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/OrganizationCreatedNotificationHandler.cs` | Handles OrganizationCreatedEvent from Identity module | Sends welcome notification to organization owner | Shared.Contracts.Identity, IMessageBus | |
| 128 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/OrganizationMemberAddedNotificationHandler.cs` | Handles OrganizationMemberAddedEvent from Identity module | Notifies new member they have been added to an organization | Shared.Contracts.Identity, IMessageBus | |
| 129 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/OrganizationMemberRemovedNotificationHandler.cs` | Handles OrganizationMemberRemovedEvent from Identity module | Notifies removed member they have been removed from an organization | Shared.Contracts.Identity, IMessageBus | |
| 130 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/PasswordResetNotificationHandler.cs` | Handles PasswordResetEvent from Identity module | Sends password reset email with reset link | Shared.Contracts.Identity, IMessageBus | |
| 131 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/PaymentReceivedNotificationHandler.cs` | Handles PaymentReceivedEvent from Billing module | Sends payment receipt notification via email and in-app | Shared.Contracts.Billing, IMessageBus | |
| 132 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/UserRegisteredNotificationHandler.cs` | Handles UserRegisteredEvent from Identity module | Sends welcome email; optionally sends welcome SMS if phone number present | Shared.Contracts.Identity, IMessageBus | |
| 133 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/EventHandlers/UserRoleChangedNotificationHandler.cs` | Handles UserRoleChangedEvent from Identity module | Notifies user their role has changed via in-app notification | Shared.Contracts.Identity, IMessageBus | |

### Application / Extensions & Markers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 134 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/Extensions/ApplicationExtensions.cs` | DI registration for application-layer services | Registers validators, mappings, application services | Microsoft.Extensions.DependencyInjection | |
| 135 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Application/INotificationsApplicationMarker.cs` | Assembly marker interface for the Application project | Used by Wolverine and DI scanning | — | |

---

## Infrastructure Layer

### Configurations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 136 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/ChannelPreferenceConfiguration.cs` | EF Core entity type configuration for ChannelPreference | Table/schema mapping, unique index on (UserId, ChannelType, NotificationType), owned VO mappings | EF Core | |
| 137 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/DeviceRegistrationConfiguration.cs` | EF Core entity type configuration for DeviceRegistration | Table mapping, index on (UserId, Token), enum conversion for Platform | EF Core | |
| 138 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/EmailMessageConfiguration.cs` | EF Core entity type configuration for EmailMessage | Table mapping, enum conversion for EmailStatus, owned EmailAddress and EmailContent VOs | EF Core | |
| 139 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/EmailPreferenceConfiguration.cs` | EF Core entity type configuration for EmailPreference | Table/schema mapping, index on UserId | EF Core | |
| 140 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs` | EF Core entity type configuration for Notification | Table mapping, index on (UserId, IsRead), enum conversion for NotificationType | EF Core | |
| 141 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/PushMessageConfiguration.cs` | EF Core entity type configuration for PushMessage | Table mapping, enum conversion for PushStatus | EF Core | |
| 142 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/SmsMessageConfiguration.cs` | EF Core entity type configuration for SmsMessage | Table mapping, enum conversion for SmsStatus, owned PhoneNumber VOs | EF Core | |
| 143 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/SmsPreferenceConfiguration.cs` | EF Core entity type configuration for SmsPreference | Table/schema mapping, index on UserId | EF Core | |
| 144 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Configurations/TenantPushConfigurationConfiguration.cs` | EF Core entity type configuration for TenantPushConfiguration | Table mapping, unique index on (TenantId, Platform), enum conversion | EF Core | |

### DbContext

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 145 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/NotificationsDbContext.cs` | EF Core DbContext for the Notifications module | Registers all DbSets; uses `notifications` PostgreSQL schema; applies all entity configurations | EF Core, BaseDbContext | |

### Repositories

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 146 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/ChannelPreferenceRepository.cs` | IChannelPreferenceRepository EF Core implementation | Queries with tenant filtering; upsert logic for preference records | NotificationsDbContext | |
| 147 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/DeviceRegistrationRepository.cs` | IDeviceRegistrationRepository EF Core implementation | GetActiveByUserAsync filters by IsActive flag; deduplicates on token | NotificationsDbContext | |
| 148 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/EmailMessageRepository.cs` | IEmailMessageRepository EF Core implementation | GetFailedRetryableAsync returns Failed messages with RetryCount < maxRetries ordered by CreatedAt, limited to batch size | NotificationsDbContext | |
| 149 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/EmailPreferenceRepository.cs` | IEmailPreferenceRepository EF Core implementation | GetByUserAsync returns all preferences scoped to tenant+user | NotificationsDbContext | |
| 150 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/NotificationRepository.cs` | INotificationRepository EF Core implementation | GetByUserAsync returns paged results ordered by CreatedAt desc; GetUnreadCountAsync; MarkAllReadAsync bulk update | NotificationsDbContext | |
| 151 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/PushMessageRepository.cs` | IPushMessageRepository EF Core implementation | Standard Add/GetByIdAsync/SaveChangesAsync | NotificationsDbContext | |
| 152 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/SmsMessageRepository.cs` | ISmsMessageRepository EF Core implementation | Standard Add/SaveChangesAsync; tenant-scoped | NotificationsDbContext | |
| 153 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Persistence/Repositories/TenantPushConfigurationRepository.cs` | ITenantPushConfigurationRepository EF Core implementation | GetByPlatformAsync resolves current tenant from context; GetByTenantAndPlatformAsync for admin use | NotificationsDbContext | |

### Services

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 154 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/ApnsPushProvider.cs` | IPushProvider implementation for Apple Push Notification Service | Sends HTTP/2 requests to APNs gateway; handles JWT token auth; parses APNs error responses | HttpClient, ILogger | |
| 155 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/EmailProviderAdapter.cs` | IEmailService implementation that bridges to IEmailProvider | Translates SendAsync(to, from, subject, body) calls into EmailDeliveryRequest; delegates to IEmailProvider | IEmailProvider, IEmailTemplateService | |
| 156 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/FcmPushProvider.cs` | IPushProvider implementation for Firebase Cloud Messaging | Sends HTTP POST to FCM v1 API; handles OAuth2 token; parses FCM error responses | HttpClient, ILogger | |
| 157 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/LogPushProvider.cs` | IPushProvider no-op/dev implementation | Logs push payloads instead of delivering; used when no platform config exists | ILogger | |
| 158 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/NotificationPreferenceChecker.cs` | INotificationPreferenceChecker implementation | Queries IChannelPreferenceRepository; returns true (enabled) if no preference exists (default-enabled pattern) | IChannelPreferenceRepository | |
| 159 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/NullSmsProvider.cs` | ISmsProvider no-op/dev implementation | Logs SMS payloads instead of delivering; used as default when Twilio is not configured | ILogger | |
| 160 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/PushCredentialEncryptor.cs` | IPushCredentialEncryptor implementation using AES-256 | Encrypts/decrypts push credentials with a key from configuration; protects FCM/APNS secrets at rest | IOptions<PushSettings>, System.Security.Cryptography | |
| 161 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/PushProviderFactory.cs` | IPushProviderFactory implementation | Looks up TenantPushConfiguration by platform; decrypts credentials; instantiates FcmPushProvider, ApnsPushProvider, WebPushPushProvider, or LogPushProvider | ITenantPushConfigurationRepository, IPushCredentialEncryptor, IHttpClientFactory, ILoggerFactory | |
| 162 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/PushSettings.cs` | Settings POCO for push credential encryption | EncryptionKey property bound from configuration | — | |
| 163 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/SignalRNotificationService.cs` | INotificationService implementation using SignalR via IRealtimeDispatcher | SendToUserAsync sends to user connection group; BroadcastToTenantAsync sends to `tenant:{tenantId}` group; wraps payload in RealtimeEnvelope | IRealtimeDispatcher, TimeProvider, ILogger | |
| 164 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/SimpleEmailTemplateService.cs` | IEmailTemplateService basic implementation | Returns template string with simple token replacement; no external engine dependency | — | |
| 165 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/SmtpConnectionPool.cs` | SMTP connection pooling for MailKit | Maintains a pool of authenticated MailKit SmtpClient instances; thread-safe borrow/return; used by SmtpEmailProvider | MailKit, IOptions<SmtpSettings>, ILogger | |
| 166 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/SmtpEmailProvider.cs` | IEmailProvider implementation via SMTP/MailKit | Builds MimeMessage with optional attachment (10 MB limit); executes via Polly `smtp` pipeline from ResiliencePipelineProvider; emits OpenTelemetry activity spans | SmtpConnectionPool, IOptions<SmtpSettings>, ResiliencePipelineProvider, ILogger | |
| 167 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/SmtpSettings.cs` | Settings POCO for SMTP configuration | Host, Port, Username, Password, DefaultFromName, DefaultFromAddress, UseSsl | — | |
| 168 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/TwilioSettings.cs` | Settings POCO for Twilio SMS configuration | AccountSid, AuthToken, FromNumber | — | |
| 169 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/TwilioSmsProvider.cs` | ISmsProvider implementation via Twilio REST API | Sends SMS using Twilio SDK; reads credentials from TwilioSettings | Twilio SDK, IOptions<TwilioSettings>, ILogger | |
| 170 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Services/WebPushPushProvider.cs` | IPushProvider implementation for Web Push (VAPID) | Sends Web Push notifications using VAPID auth via HttpClient | HttpClient, ILogger | |

### Jobs

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 171 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Jobs/RetryFailedEmailsJob.cs` | Background job retrying failed emails | Fetches up to 100 retryable failed emails (RetryCount < 3); calls ResetForRetry() then re-sends; marks Sent/Failed; saves batch; uses structured logging | IEmailMessageRepository, IEmailService, TimeProvider, ILogger | |

### Migrations

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 172 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Migrations/20260312201400_InitialCreate.cs` | EF Core initial migration for all Notifications tables | Creates tables for all 8 entities under `notifications` schema | EF Core Migrations | |
| 173 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Migrations/20260312201400_InitialCreate.Designer.cs` | EF Core migration designer snapshot | Auto-generated | EF Core Migrations | |
| 174 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Migrations/NotificationsDbContextModelSnapshot.cs` | EF Core model snapshot | Auto-generated; represents current schema state | EF Core Migrations | |

### Extensions

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 175 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Infrastructure/Extensions/NotificationsModuleExtensions.cs` | DI and middleware registration for the full Notifications module | Registers DbContext, repositories, SMTP/Twilio/push services, SignalR service, Polly pipelines, jobs, settings | Microsoft.Extensions.DependencyInjection, EF Core, MailKit, Twilio, Polly | |

---

## Api Layer

### Contracts / InApp

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 176 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/InApp/Responses/NotificationResponse.cs` | API response record for a single notification | Exposes Id, UserId, Type, Title, Message, IsRead, ReadAt, CreatedAt, UpdatedAt | — | |
| 177 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/InApp/Responses/PagedNotificationResponse.cs` | API response record for a paged list of notifications | Wraps list of NotificationResponse with pagination metadata | — | |
| 178 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/InApp/Responses/UnreadCountResponse.cs` | API response record for unread notification count | Single Count integer | — | |

### Contracts / Preferences

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 179 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/Preferences/SetChannelEnabledRequest.cs` | API request body for enabling/disabling a notification channel | IsEnabled boolean | — | |
| 180 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/Preferences/SetNotificationTypeEnabledRequest.cs` | API request body for enabling/disabling a specific notification type on a channel | NotificationType string, IsEnabled boolean | — | |
| 181 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/Preferences/UserNotificationSettingsResponse.cs` | API response for full user notification settings | Contains per-channel, per-type preference flags | — | |

### Contracts / Push

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 182 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/Push/DeviceRegistrationResponse.cs` | API response for a registered device | Id, Token, Platform, RegisteredAt | — | |
| 183 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/Push/RegisterDeviceRequest.cs` | API request body for device registration | Token (string), Platform (PushPlatform) | — | |
| 184 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/Push/SendPushRequest.cs` | API request body for sending a push notification | RecipientId, Title, Body, NotificationType | — | |
| 185 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/Push/TenantPushConfigResponse.cs` | API response for tenant push config (no credentials) | Platform, IsEnabled, ConfiguredAt | — | |
| 186 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Contracts/Push/UpsertTenantPushConfigRequest.cs` | API request body for upserting tenant push credentials | Platform, Credentials (JSON string) | — | |

### Controllers

| # | Status | File | Purpose | Key Logic | Dependencies | Your Notes |
|---|--------|------|---------|-----------|--------------|------------|
| 187 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Controllers/NotificationsController.cs` | REST controller for in-app notifications | GET /notifications (paged), GET /notifications/unread-count, POST /notifications/{id}/read, POST /notifications/read-all; requires NotificationRead permission | IMessageBus, ICurrentUserService | |
| 188 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Controllers/PushConfigurationController.cs` | REST controller for tenant push configuration management | CRUD endpoints for TenantPushConfiguration; UpsertTenantPushConfig, GetTenantPushConfig, RemoveTenantPushConfig, SetTenantPushEnabled | IMessageBus | |
| 189 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Controllers/PushDevicesController.cs` | REST controller for device registration | POST /push/devices (register), DELETE /push/devices (deregister), GET /push/devices (list user devices) | IMessageBus, ICurrentUserService | |
| 190 | [ ] | `src/Modules/Notifications/Wallow.Notifications.Api/Controllers/UserNotificationSettingsController.cs` | REST controller for user notification preferences | GET /notification-settings, PUT /notification-settings/channels/{channel}, PUT /notification-settings/channels/{channel}/types/{type} | IMessageBus, ICurrentUserService | |

---

## Test Files

### Domain Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| T1 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/Domain/Entities/DeviceRegistrationTests.cs` | Unit tests for DeviceRegistration aggregate | Create, platform validation, token storage | |
| T2 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/Domain/Entities/NotificationCreateTests.cs` | Unit tests for Notification aggregate creation | Create raises NotificationCreatedDomainEvent, defaults IsRead=false, stores all fields | |
| T3 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/Domain/ValueObjects/EmailAddressTests.cs` | Unit tests for EmailAddress value object | Valid formats accepted; invalid formats throw InvalidEmailAddressException | |
| T4 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/Domain/ValueObjects/PhoneNumberTests.cs` | Unit tests for PhoneNumber value object | Valid E.164 formats accepted; invalid formats throw InvalidPhoneNumberException | |

### Application / Command Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| T5 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/Application/Commands/SetChannelEnabled/SetChannelEnabledHandlerTests.cs` | Unit tests for SetChannelEnabledHandler | Enables/disables all preferences for a user+channel; creates preferences when none exist | |
| T6 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/Application/Commands/SetChannelEnabled/SetChannelEnabledValidatorTests.cs` | Unit tests for SetChannelEnabledValidator | Valid and invalid command variations | |

### Application / Query Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| T7 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/Application/Queries/GetUnreadCount/GetUnreadCountHandlerTests.cs` | Unit tests for GetUnreadCountHandler | Returns count from repository; handles empty case | |
| T8 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/Application/Queries/GetUserNotificationSettings/GetUserNotificationSettingsHandlerTests.cs` | Unit tests for GetUserNotificationSettingsHandler | Returns aggregated settings DTO; handles user with no preferences | |

### Event Handler Tests

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| T9 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/AnnouncementPublishedNotificationHandlerTests.cs` | Unit tests for AnnouncementPublishedNotificationHandler | One SendNotificationCommand dispatched per TargetUserId | |
| T10 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/InquiryStatusChangedNotificationHandlerTests.cs` | Unit tests for InquiryStatusChangedNotificationHandler | Correct notification dispatched for status change event | |
| T11 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/InquirySubmittedNotificationHandlerTests.cs` | Unit tests for InquirySubmittedNotificationHandler | Staff notification dispatched on inquiry submission | |
| T12 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/InvoiceOverdueNotificationHandlerTests.cs` | Unit tests for InvoiceOverdueNotificationHandler | Email and in-app commands dispatched for overdue invoice | |
| T13 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/InvoicePaidNotificationHandlerTests.cs` | Unit tests for InvoicePaidNotificationHandler | Confirmation notifications dispatched on payment | |
| T14 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/MessageSentNotificationHandlerTests.cs` | Unit tests for MessageSentNotificationHandler | In-app notification dispatched to message recipient | |
| T15 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/OrganizationCreatedNotificationHandlerTests.cs` | Unit tests for OrganizationCreatedNotificationHandler | Welcome notification dispatched to organization owner | |
| T16 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/OrganizationMemberAddedNotificationHandlerTests.cs` | Unit tests for OrganizationMemberAddedNotificationHandler | New member notification dispatched | |
| T17 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/OrganizationMemberRemovedNotificationHandlerTests.cs` | Unit tests for OrganizationMemberRemovedNotificationHandler | Removal notification dispatched | |
| T18 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/PasswordResetNotificationHandlerTests.cs` | Unit tests for PasswordResetNotificationHandler | Password reset email dispatched with correct reset link | |
| T19 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/PaymentReceivedNotificationHandlerTests.cs` | Unit tests for PaymentReceivedNotificationHandler | Receipt notifications dispatched on payment received | |
| T20 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/UserRegisteredNotificationHandlerTests.cs` | Unit tests for UserRegisteredNotificationHandler | Welcome email always dispatched; SMS dispatched only when PhoneNumber present | |
| T21 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/EventHandlers/UserRoleChangedNotificationHandlerTests.cs` | Unit tests for UserRoleChangedNotificationHandler | Role change in-app notification dispatched | |

### Test Infrastructure

| # | Status | File | Purpose | What It Tests | Your Notes |
|---|--------|------|---------|---------------|------------|
| T22 | [ ] | `tests/Modules/Notifications/Wallow.Notifications.Tests/NotificationsTestsMarker.cs` | Assembly marker interface for test project | Used by xUnit for test discovery scoping | |
