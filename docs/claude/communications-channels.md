# Communications Channels

> **HISTORICAL DOCUMENT** — This document describes the former Communications module architecture. The Communications module was subsequently split into three separate modules: **Notifications** (email, SMS, in-app notifications), **Messaging** (user-to-user conversations), and **Announcements** (tenant-wide broadcasts). All file paths in this document reference the old `Wallow.Communications.*` project structure which no longer exists. For current architecture, refer to the individual module CLAUDE.md files under `src/Modules/Notifications/`, `src/Modules/Messaging/`, and `src/Modules/Announcements/`.

The (former) Communications module organized delivery mechanisms into **channels**. Each channel had its own domain entities, preferences, and provider abstraction.

---

## Channel Model

### ChannelType enum

Defined in `Wallow.Communications.Domain/Preferences/ChannelType.cs`:

```csharp
public enum ChannelType
{
    Email = 0,
    Sms = 1,
    InApp = 2,
    Push = 3,
    Webhook = 4
}
```

`Email`, `Sms`, and `InApp` are implemented. `Push` and `Webhook` are reserved for future use.

### NotificationTypes constants

Defined in `Wallow.Communications.Domain/Preferences/NotificationTypes.cs`:

```csharp
public static class NotificationTypes
{
    public const string TaskAssigned = "task_assigned";
    public const string BillingInvoice = "billing_invoice";
    public const string SystemAlert = "system_alert";
    public const string UserMention = "user_mention";
    public const string Announcement = "announcement";
}
```

These string constants identify the category of a notification across all channels. They are used for preference lookups and routing decisions.

---

## Channel Entity Structure

### Email channel

**Domain entities** in `Domain/Channels/Email/`:

| Entity | Base class | Key properties |
|--------|-----------|----------------|
| `EmailMessage` | `AggregateRoot<EmailMessageId>`, `ITenantScoped` | `To` (EmailAddress), `From` (EmailAddress?), `Content` (EmailContent), `Status` (EmailStatus), `SentAt`, `FailureReason`, `RetryCount` |
| `EmailPreference` | `AggregateRoot<EmailPreferenceId>`, `ITenantScoped` | `UserId`, `NotificationType` (enum), `IsEnabled` |

**Value objects:**
- `EmailAddress` -- validated email string wrapper
- `EmailContent` -- holds `Subject` and `Body`

**Enums:**
- `EmailStatus` -- `Pending`, `Sent`, `Failed`
- `NotificationType` -- per-channel enum for email preference categories (e.g. `SystemNotification`)

**Domain events:** `EmailSentDomainEvent`, `EmailFailedDomainEvent`

**Lifecycle:** `EmailMessage.Create()` sets status to `Pending`. The `SendEmailHandler` persists the message, calls `IEmailService.SendAsync()`, then calls `MarkAsSent()` or `MarkAsFailed()`. Failed messages support `ResetForRetry()` with `CanRetry(maxRetries)` guard.

### SMS channel

**Domain entities** in `Domain/Channels/Sms/`:

| Entity | Base class | Key properties |
|--------|-----------|----------------|
| `SmsMessage` | `AggregateRoot<SmsMessageId>`, `ITenantScoped` | `To` (PhoneNumber), `From` (PhoneNumber?), `Body` (string, max 1600 chars), `Status` (SmsStatus), `SentAt`, `FailureReason`, `RetryCount` |
| `SmsPreference` | `Entity<SmsPreferenceId>`, `ITenantScoped` | `UserId`, `PhoneNumber` (value object), `IsOptedIn` |

**Value objects:**
- `PhoneNumber` -- validated phone number wrapper

**Enums:**
- `SmsStatus` -- `Pending`, `Sent`, `Failed`

**Domain events:** `SmsSentDomainEvent`, `SmsFailedDomainEvent`

**Lifecycle:** `SmsMessage.Create()` validates body length (max 1600 chars) and sets status to `Pending`. The `SendSmsHandler` persists the message, calls `ISmsProvider.SendAsync()`, then marks sent or failed. Same retry pattern as Email.

### InApp channel

**Domain entities** in `Domain/Channels/InApp/`:

| Entity | Base class | Key properties |
|--------|-----------|----------------|
| `Notification` | `AggregateRoot<NotificationId>`, `ITenantScoped` | `UserId`, `Type` (NotificationType), `Title`, `Message`, `IsRead`, `ReadAt`, `ActionUrl`, `SourceModule`, `ExpiresAt`, `IsArchived` |

**Enums:**
- `NotificationType` -- InApp-specific enum (e.g. `Announcement`)

**Domain events:** `NotificationCreatedDomainEvent`, `NotificationReadDomainEvent`

**No separate preference entity** -- in-app notifications are always delivered. Read state and archival are tracked directly on the `Notification` aggregate. Computed property `IsExpired` checks `ExpiresAt < DateTime.UtcNow`.

**Lifecycle:** `Notification.Create()` sets `IsRead = false`, `IsArchived = false`, and raises `NotificationCreatedDomainEvent`. Users can `MarkAsRead()` and `Archive()`.

---

## Channel Preferences

Preferences control whether a user receives notifications through a given channel for a given notification type.

### Per-channel preferences (legacy)

Each channel originally defined its own preference entity:

- **Email:** `EmailPreference` with `UserId`, `NotificationType` (enum), `IsEnabled`
- **SMS:** `SmsPreference` with `UserId`, `PhoneNumber`, `IsOptedIn`
- **InApp:** No preference entity (always delivered)

### Unified ChannelPreference model

The `ChannelPreference` aggregate in `Domain/Preferences/Entities/ChannelPreference.cs` provides a cross-channel preference system:

```csharp
public sealed class ChannelPreference : AggregateRoot<ChannelPreferenceId>, ITenantScoped
{
    public TenantId TenantId { get; set; }
    public Guid UserId { get; private set; }
    public ChannelType ChannelType { get; private set; }
    public string NotificationType { get; private set; }  // uses NotificationTypes constants
    public bool IsEnabled { get; private set; }
}
```

Key behaviors: `Enable()`, `Disable()`, `Toggle()`. Factory method `Create()` validates `notificationType` is not null/empty and raises `ChannelPreferenceCreatedEvent`.

**Repository** (`Application/Preferences/Interfaces/IChannelPreferenceRepository.cs`):

```csharp
public interface IChannelPreferenceRepository
{
    Task<ChannelPreference?> GetByIdAsync(ChannelPreferenceId id, ...);
    Task<ChannelPreference?> GetByUserAndChannelAsync(Guid userId, ChannelType channelType, ...);
    Task<ChannelPreference?> GetByUserChannelAndNotificationTypeAsync(
        Guid userId, ChannelType channelType, string notificationType, ...);
    Task<IReadOnlyList<ChannelPreference>> GetByUserIdAsync(Guid userId, ...);
    void Add(ChannelPreference preference);
    void Update(ChannelPreference preference);
    void Delete(ChannelPreference preference);
    Task SaveChangesAsync(...);
}
```

**Command:** `SetChannelPreferenceCommand(TenantId, UserId, ChannelType, NotificationType, IsEnabled)` -- creates or updates a preference. Handler upserts via `GetByUserChannelAndNotificationTypeAsync`.

**Query:** `GetChannelPreferencesQuery(UserId)` -- returns all `ChannelPreferenceDto` records for a user.

**DTO:** `ChannelPreferenceDto(Id, UserId, ChannelType, NotificationType, IsEnabled, CreatedAt, UpdatedAt)`.

---

## Directory Structure

Each channel lives under `Channels/{ChannelName}/` in both the Domain and Application layers:

```
Wallow.Communications.Domain/
  Channels/
    Email/
      Entities/        EmailMessage, EmailPreference
      Enums/           EmailStatus, NotificationType
      Events/          EmailSentDomainEvent, EmailFailedDomainEvent
      Exceptions/      InvalidEmailAddressException
      Identity/        EmailMessageId, EmailPreferenceId
      ValueObjects/    EmailAddress, EmailContent
    InApp/
      Entities/        Notification
      Enums/           NotificationType
      Events/          NotificationCreatedDomainEvent, NotificationReadDomainEvent
      Identity/        NotificationId
    Sms/
      Entities/        SmsMessage, SmsPreference
      Enums/           SmsStatus
      Events/          SmsSentDomainEvent, SmsFailedDomainEvent
      Exceptions/      InvalidPhoneNumberException
      Identity/        SmsMessageId, SmsPreferenceId
      ValueObjects/    PhoneNumber
  Preferences/
    ChannelType.cs
    NotificationTypes.cs
    Entities/          ChannelPreference
    Events/            ChannelPreferenceCreatedEvent
    Identity/          ChannelPreferenceId
```

```
Wallow.Communications.Application/
  Channels/
    Email/
      Commands/        SendEmail, UpdateEmailPreferences
      DTOs/            EmailDto, EmailPreferenceDto
      EventHandlers/   PasswordResetRequestedEventHandler, SendEmailRequestedEventHandler,
                       UserRegisteredEventHandler
      Extensions/      ApplicationExtensions
      Interfaces/      IEmailProvider, IEmailMessageRepository, IEmailPreferenceRepository,
                       IEmailTemplateService
      Mappings/        EmailMappings
      Queries/         GetEmailPreferences
      Telemetry/       EmailModuleTelemetry
    InApp/
      Commands/        ArchiveNotification, MarkAllNotificationsRead, MarkNotificationRead,
                       SendNotification
      DTOs/            NotificationDto
      EventHandlers/   AnnouncementPublishedEventHandler, UserRegisteredEventHandler
      Extensions/      ApplicationExtensions
      Interfaces/      INotificationRepository, INotificationService
      Mappings/        NotificationMappings
      Queries/         GetUnreadCount, GetUserNotifications
      Telemetry/       NotificationsModuleTelemetry
    Sms/
      Commands/        SendSms
      EventHandlers/   SendSmsRequestedEventHandler
      Interfaces/      ISmsProvider, ISmsMessageRepository
  Preferences/
    Commands/          SetChannelPreferenceCommand
    DTOs/              ChannelPreferenceDto
    Interfaces/        IChannelPreferenceRepository
    Queries/           GetChannelPreferencesQuery
```

```
Wallow.Communications.Infrastructure/
  Services/
    SmtpEmailProvider          IEmailProvider -> SMTP via MailKit
    EmailProviderAdapter       IEmailService -> delegates to IEmailProvider
    SimpleEmailTemplateService IEmailTemplateService implementation
    SignalRNotificationService INotificationService -> real-time push via SignalR
    TwilioSmsProvider          ISmsProvider -> Twilio REST API
    NullSmsProvider            ISmsProvider -> no-op for development
    SmtpSettings               Options class for SMTP configuration
    TwilioSettings             Options class for Twilio configuration
    MessagingQueryService      IMessagingQueryService -> Dapper queries
  Persistence/
    Repositories/              EF Core repositories for all entities
    Configurations/            Entity type configurations
  Jobs/
    RetryFailedEmailsJob       Background retry for failed emails
```

---

## Provider Abstraction

Each channel that sends messages externally uses a **provider interface** in the Application layer, with concrete implementations in Infrastructure. This allows swapping delivery backends (e.g., SMTP vs. SendGrid, Twilio vs. a null provider) without changing application logic.

### Email provider

**Interface** (`Application/Channels/Email/Interfaces/IEmailProvider.cs`):

```csharp
public record EmailDeliveryRequest(
    string To,
    string? From,
    string Subject,
    string Body,
    ReadOnlyMemory<byte>? Attachment = null,
    string? AttachmentName = null,
    string AttachmentContentType = "application/octet-stream");

public readonly record struct EmailDeliveryResult(bool Success, string? ErrorMessage);

public interface IEmailProvider
{
    Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryRequest request,
        CancellationToken cancellationToken = default);
}
```

**Implementations:**

| Class | Location | Description |
|-------|----------|-------------|
| `SmtpEmailProvider` | `Infrastructure/Services/SmtpEmailProvider.cs` | Sends via SMTP using MailKit. Supports retry with exponential backoff (`2^attempt * 1000ms`), TLS via `StartTls`, authentication, and 10MB attachment limit. Configured via `SmtpSettings` options (`Host`, `Port`, `UseSsl`, `Username`, `Password`, `DefaultFromName`, `DefaultFromAddress`, `MaxRetries`, `TimeoutSeconds`). |
| `EmailProviderAdapter` | `Infrastructure/Services/EmailProviderAdapter.cs` | Bridges `IEmailProvider` to the `IEmailService` contract from `Shared.Contracts`. Other modules call `IEmailService`; this adapter delegates to `IEmailProvider` internally. |

**Provider selection** (in `CommunicationsModuleExtensions.RegisterEmailProvider`):

```csharp
string provider = configuration.GetValue<string>("Communications:Email:Provider") ?? "Smtp";
// Currently only "Smtp" is recognized; unrecognized values log a warning and fall back to Smtp.
```

### SMS provider

**Interface** (`Application/Channels/Sms/Interfaces/ISmsProvider.cs`):

```csharp
public readonly record struct SmsDeliveryResult(
    bool Success,
    string? MessageSid,
    string? ErrorMessage);

public interface ISmsProvider
{
    Task<SmsDeliveryResult> SendAsync(
        string to,
        string body,
        CancellationToken cancellationToken = default);
}
```

**Implementations:**

| Class | Location | Description |
|-------|----------|-------------|
| `TwilioSmsProvider` | `Infrastructure/Services/TwilioSmsProvider.cs` | Sends via Twilio REST API using `HttpClient`. Uses Basic auth with `AccountSid:AuthToken`. Configured via `TwilioSettings` (`AccountSid`, `AuthToken`, `FromNumber`). |
| `NullSmsProvider` | `Infrastructure/Services/NullSmsProvider.cs` | No-op provider for development/testing. Logs the suppressed message and returns success with `"null-sid"`. |

**Provider selection** (in `CommunicationsModuleExtensions.AddCommunicationsServices`):

```csharp
string? twilioAccountSid = configuration["TwilioSettings:AccountSid"];
if (!string.IsNullOrEmpty(twilioAccountSid))
    services.AddScoped<ISmsProvider, TwilioSmsProvider>();  // also registers HttpClient
else
    services.AddScoped<ISmsProvider, NullSmsProvider>();
```

### InApp provider

InApp notifications do not use a provider interface. The `INotificationService` interface handles real-time delivery:

```csharp
// Application/Channels/InApp/Interfaces/INotificationService.cs
public interface INotificationService
{
    Task BroadcastToTenantAsync(TenantId tenantId, string title, string message,
        string type, CancellationToken cancellationToken = default);
    // ... other methods for user-specific notifications
}
```

Implemented by `SignalRNotificationService` in Infrastructure.

### How to swap a provider implementation

1. Create your new provider class implementing the channel's interface (e.g., `SendGridEmailProvider : IEmailProvider`).
2. Add a configuration options class if needed (e.g., `SendGridSettings`).
3. Update the provider selection logic in `CommunicationsModuleExtensions` to register your class based on config.
4. Add the config section to `appsettings.json`.

No changes to Application or Domain layers are needed.

### Cross-module email access

Other modules do not reference `IEmailProvider` directly. Instead, they depend on `IEmailService` from `Shared.Contracts`:

```csharp
// Shared.Contracts/Communications/Email/IEmailService.cs
public interface IEmailService
{
    Task SendAsync(string to, string? from, string subject, string body,
        CancellationToken cancellationToken = default);

    Task SendWithAttachmentAsync(string to, string? from, string subject, string body,
        byte[] attachment, string attachmentName,
        string attachmentContentType = "application/octet-stream",
        CancellationToken cancellationToken = default);
}
```

The `EmailProviderAdapter` in Infrastructure implements `IEmailService` by delegating to `IEmailProvider`. This keeps the provider pattern internal to the Communications module.

---

## Cross-Channel Event Flow

This section describes how an event originating from another module flows through to a channel handler and ultimately to delivery.

### Pattern 1: Integration event from another module

Example: `UserRegisteredEvent` from the Identity module triggers a welcome email.

```
Identity module                    Communications module
┌──────────────────┐              ┌──────────────────────────────────────────┐
│ User registers   │              │                                          │
│   -> publishes   │──RabbitMQ──> │ UserRegisteredEventHandler (Email)       │
│ UserRegistered   │              │   1. Check EmailPreference for user      │
│ Event            │              │   2. If enabled: render template via     │
│                  │              │      IEmailTemplateService               │
└──────────────────┘              │   3. Call IEmailService.SendAsync()      │
                                  │      -> EmailProviderAdapter             │
                                  │      -> IEmailProvider (SmtpEmailProvider)│
                                  │      -> SMTP delivery                    │
                                  └──────────────────────────────────────────┘
```

### Pattern 2: Cross-module send request via integration event

Example: Any module publishes `SendEmailRequestedEvent` or `SendSmsRequestedEvent` to request delivery.

```
Any module                         Communications module
┌──────────────────┐              ┌──────────────────────────────────────────┐
│ Publishes        │              │                                          │
│ SendEmailRequest │──RabbitMQ──> │ SendEmailRequestedEventHandler           │
│ edEvent          │              │   -> IEmailService.SendAsync()           │
│                  │              │   -> EmailProviderAdapter                │
└──────────────────┘              │   -> IEmailProvider -> SMTP              │
                                  └──────────────────────────────────────────┘

┌──────────────────┐              ┌──────────────────────────────────────────┐
│ Publishes        │              │                                          │
│ SendSmsRequested │──RabbitMQ──> │ SendSmsRequestedEventHandler             │
│ Event            │              │   -> Wolverine IMessageBus.InvokeAsync() │
│                  │              │   -> SendSmsHandler                      │
└──────────────────┘              │   -> ISmsProvider -> Twilio/Null         │
                                  └──────────────────────────────────────────┘
```

**Key difference:** The Email handler calls `IEmailService` directly, while the SMS handler dispatches a `SendSmsCommand` via Wolverine's `IMessageBus`, which routes to `SendSmsHandler`. The SMS handler also persists an `SmsMessage` aggregate to track delivery state.

### Pattern 3: Internal command (direct API call)

When the Communications API receives a direct request (e.g., POST to send an email), it creates a command that is handled by Wolverine:

```
API Controller
  -> SendEmailCommand / SendSmsCommand / SendNotificationCommand
  -> Wolverine handler
  -> Persists domain entity (EmailMessage / SmsMessage / Notification)
  -> Calls provider (IEmailProvider / ISmsProvider / INotificationService)
  -> Updates entity status (Sent / Failed)
  -> Saves to DB
```

### Pattern 4: InApp broadcast from domain event

Example: `AnnouncementPublishedEvent` triggers in-app notifications to all tenant users.

```
Announcements feature              InApp channel
┌──────────────────┐              ┌──────────────────────────────────────────┐
│ Announcement     │              │ AnnouncementPublishedEventHandler        │
│ published        │──RabbitMQ──> │   1. Filter: only pinned or Alert type  │
│                  │              │   2. Format title based on type          │
│                  │              │   3. Strip markdown, truncate to 200ch   │
│                  │              │   4. INotificationService                │
└──────────────────┘              │      .BroadcastToTenantAsync()           │
                                  │   -> SignalRNotificationService          │
                                  └──────────────────────────────────────────┘
```

### Integration events in Shared.Contracts

These events in `Wallow.Shared.Contracts/Communications/` enable cross-module communication:

| Event | Direction | Purpose |
|-------|-----------|---------|
| `SendEmailRequestedEvent` | Inbound | Other modules request email delivery |
| `SendSmsRequestedEvent` | Inbound | Other modules request SMS delivery |
| `EmailSentEvent` | Outbound | Notifies other modules that an email was sent |
| `NotificationCreatedEvent` | Outbound | Notifies other modules that an in-app notification was created |
| `AnnouncementPublishedEvent` | Inbound | Triggers in-app notification for published announcements |
| `ConversationCreatedIntegrationEvent` | Outbound | Notifies that a messaging conversation was created |
| `MessageSentIntegrationEvent` | Outbound | Notifies that a direct message was sent |

---

## Adding a New Channel Type

To add a new channel (e.g., Push notifications):

### 1. Add domain entities

Create the directory structure under `Domain/Channels/Push/`:

```
Channels/Push/
  Entities/      PushSubscription.cs
  Enums/         PushStatus.cs
  Events/        PushSentDomainEvent.cs, PushFailedDomainEvent.cs
  Identity/      PushSubscriptionId.cs
  ValueObjects/  DeviceToken.cs
```

### 2. Define the provider interface

In `Application/Channels/Push/Interfaces/`:

```csharp
public readonly record struct PushDeliveryResult(bool Success, string? ErrorMessage);

public interface IPushProvider
{
    Task<PushDeliveryResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        CancellationToken cancellationToken = default);
}
```

### 3. Create the application layer

Follow the existing channel pattern under `Application/Channels/Push/`:

- Commands: `SendPushNotification`
- EventHandlers: subscribe to relevant integration events
- Interfaces: `IPushSubscriptionRepository`
- DTOs and Mappings

### 4. Implement the provider

In `Infrastructure/Services/`, create the concrete provider:

```csharp
public sealed class FirebasePushProvider(
    IOptions<FirebaseSettings> settings,
    ILogger<FirebasePushProvider> logger) : IPushProvider
{
    public async Task<PushDeliveryResult> SendAsync(
        string deviceToken, string title, string body,
        CancellationToken cancellationToken = default)
    {
        // Firebase Cloud Messaging implementation
    }
}
```

Also create a `NullPushProvider` for development environments.

### 5. Register in DI

In `CommunicationsModuleExtensions.AddCommunicationsServices`, register the provider based on configuration:

```csharp
string pushProvider = configuration.GetValue<string>("Push:Provider") ?? "Null";
if (pushProvider == "Firebase")
    services.AddSingleton<IPushProvider, FirebasePushProvider>();
else
    services.AddSingleton<IPushProvider, NullPushProvider>();
```

### 6. Add EF Core configuration

Create entity configurations in `Infrastructure/Persistence/Configurations/` and a migration for the new tables in the `communications` schema.

### 7. Add ChannelPreference support

The unified `ChannelPreference` model already has `ChannelType.Push` in the enum. No domain changes needed -- users can immediately create preferences for the new channel via `SetChannelPreferenceCommand`.

### 8. Expose via API (optional)

Add controllers in `Wallow.Communications.Api/Controllers/` for managing push subscriptions and preferences.
