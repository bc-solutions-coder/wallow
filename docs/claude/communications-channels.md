# Communications Channels

The Communications module organizes delivery mechanisms into **channels**. Each channel has its own domain entities, preferences, and provider abstraction.

---

## Channel Model

### ChannelType enum

Defined in `Foundry.Communications.Domain/Preferences/ChannelType.cs`:

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

Defined in `Foundry.Communications.Domain/Preferences/NotificationTypes.cs`:

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

### Channel preferences

Each channel defines its own preference entity that controls whether a user receives notifications through that channel.

**Email:** `EmailPreference` is an `AggregateRoot<EmailPreferenceId>` with `UserId`, `NotificationType` (enum), and `IsEnabled` flag. Located at `Domain/Channels/Email/Entities/EmailPreference.cs`.

**SMS:** `SmsPreference` is an `Entity<SmsPreferenceId>` with `UserId`, `PhoneNumber` (value object), and `IsOptedIn` flag. Located at `Domain/Channels/Sms/Entities/SmsPreference.cs`.

**InApp:** The `Notification` aggregate itself tracks read state (`IsRead`, `ReadAt`) and archival (`IsArchived`). There is no separate preference entity -- in-app notifications are always delivered. Located at `Domain/Channels/InApp/Entities/Notification.cs`.

### ChannelPreferenceId

`ChannelPreferenceId` is a strongly-typed ID (`Foundry.Communications.Domain/Preferences/Identity/ChannelPreferenceId.cs`) reserved for a future unified `ChannelPreference` aggregate that would span all channel types.

---

## Directory Structure

Each channel lives under `Channels/{ChannelName}/` in both the Domain and Application layers:

```
Foundry.Communications.Domain/
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
    Identity/          ChannelPreferenceId
```

```
Foundry.Communications.Application/
  Channels/
    Email/
      Commands/        SendEmail, UpdateEmailPreferences
      DTOs/            EmailDto, EmailPreferenceDto
      EventHandlers/   PasswordResetRequestedEventHandler, SendEmailRequestedEventHandler, UserRegisteredEventHandler
      Interfaces/      IEmailProvider, IEmailPreferenceRepository, IEmailTemplateService
      Mappings/        EmailMappings
      Queries/         GetEmailPreferences
      Telemetry/       EmailModuleTelemetry
    InApp/
      Commands/        MarkAllNotificationsRead, MarkNotificationRead, SendNotification
      DTOs/            NotificationDto
      EventHandlers/   AnnouncementPublishedEventHandler, UserRegisteredEventHandler
      Interfaces/      INotificationRepository, INotificationService
      Mappings/        NotificationMappings
      Queries/         GetUnreadCount, GetUserNotifications
      Telemetry/       NotificationsModuleTelemetry
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
- `SmtpEmailProvider` -- sends via SMTP using MailKit. Supports retry with exponential backoff, TLS, and authentication. Configured via `SmtpSettings` options.
- `EmailProviderAdapter` -- bridges the `IEmailProvider` interface to the `IEmailService` contract from `Shared.Contracts`, allowing other modules to send emails without depending on the Communications module directly.

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
- `TwilioSmsProvider` -- sends via Twilio REST API.
- `NullSmsProvider` -- no-op provider for development and testing.

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

In the Communications module's Infrastructure extensions, register the provider based on configuration:

```csharp
string pushProvider = configuration.GetValue<string>("Push:Provider") ?? "Null";
if (pushProvider == "Firebase")
    services.AddSingleton<IPushProvider, FirebasePushProvider>();
else
    services.AddSingleton<IPushProvider, NullPushProvider>();
```

### 6. Add EF Core configuration

Create entity configurations and a migration for the new tables in the `communications` schema.

### 7. Expose via API (optional)

Add controllers in `Foundry.Communications.Api/Controllers/` for managing push subscriptions and preferences.
