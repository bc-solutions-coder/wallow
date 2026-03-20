# Communications Module Split — Design Spec

**Date:** 2026-03-12
**Status:** Draft
**Migration Strategy:** Big bang (single PR)

## Overview

Split the Communications module into three focused modules: **Notifications**, **Messaging**, and **Announcements**. The Notifications module becomes a pure delivery engine with no business logic about what triggers notifications. Each publishing module owns its own notification content and templates.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Notification preferences | Notifications module owns them | Single place for preference logic; delivery layer checks before sending |
| Cross-module communication | Integration events only | Fully decoupled, consistent with existing inter-module pattern |
| Feature flags | Separate per module | `Modules.Notifications`, `Modules.Messaging`, `Modules.Announcements` for independent toggling |
| Shared contracts namespace | Reorganize to match new modules | Clean alignment between contracts and module boundaries |
| InquirySubmittedEventHandler | Move to Inquiries module | Inquiries publishes `SendEmailRequestedEvent` with rendered content |
| Real-time (SignalR) | Notifications owns all real-time delivery | Single hub, single source for frontend subscriptions |
| Email templates | Two-layer: modules render content, Notifications wraps in shared layout | Modules own their content; branding/header/footer centralized in Notifications |
| Preference bypass | `IsCritical` flag on send events | Critical notifications (password reset, security alerts) skip preference checks |
| Notification content ownership | Publishing module provides all content | Generic `SendNotificationRequestedEvent` pattern — Notifications never interprets domain events |

## Module Boundaries

### Notifications Module

Pure delivery engine. Receives send requests, checks preferences, wraps emails in shared layout, delivers via the appropriate channel.

**Domain entities:**
- `EmailMessage` — aggregate root with Pending/Sent/Failed lifecycle, 3-retry max
- `SmsMessage` — aggregate root, E.164 validation, 1600-char limit
- `PushMessage` — outbound push with status tracking
- `TenantPushConfiguration` — encrypted credentials per platform (FCM/APNs/WebPush) per tenant
- `DeviceRegistration` — user device tokens
- `Notification` — in-app notification with read/archive support
- `ChannelPreference` — per-user preference keyed by (UserId, ChannelType, NotificationType)
- `EmailPreference`, `SmsPreference` — legacy per-channel preferences (migrate to ChannelPreference or keep)

**Value objects:** `EmailAddress`, `EmailContent`, `PhoneNumber`

**Enums:** `PushPlatform` (Fcm, Apns, WebPush)

**Infrastructure:**
- SMTP/MailKit with Polly resilience (exponential backoff, 3 retries, 30s timeout)
- `SmtpConnectionPool` (singleton)
- Twilio SMS provider (or `NullSmsProvider` fallback)
- FCM, APNs, WebPush providers via `PushProviderFactory`
- `PushCredentialEncryptor` (DataProtection)
- `SignalRNotificationService` — real-time delivery via `IRealtimeDispatcher`
- `RetryFailedEmailsJob` — background retry for failed emails
- Shared email layout engine (header/footer/branding wrapper)

**Inbound events (consumes):**

| Event | Action |
|-------|--------|
| `SendEmailRequestedEvent` | Check preferences, wrap in layout, deliver via SMTP |
| `SendSmsRequestedEvent` | Check preferences, deliver via Twilio |
| `SendPushRequestedEvent` | Check preferences, deliver via platform provider |
| `SendNotificationRequestedEvent` (new) | Check preferences, create InApp record, push via SignalR |

**Outbound events (publishes):**
- `EmailSentEvent`
- `NotificationCreatedEvent`

**API controllers:**
- `PushDevicesController` — device registration CRUD
- `PushConfigurationController` — admin tenant push config
- `NotificationsController` — in-app notification read/archive/unread-count
- `UserNotificationSettingsController` — preference management
- `NotificationsSettingsController` — module settings (replaces `CommunicationsSettingsController`)

**Database schema:** `notifications`

**Feature flag:** `Modules.Notifications`

**Settings key namespace:** `notifications`

---

### Messaging Module

Owns user-to-user direct and group conversations.

**Domain entities:**
- `Conversation` — aggregate root with `CreateDirect` / `CreateGroup` factory methods
- `Participant` — child entity with active/inactive status, read tracking
- `Message` — child entity

**Application layer:**
- Commands: `CreateConversation`, `SendMessage`, `MarkConversationRead`
- Queries: `GetConversations`, `GetMessages` (cursor-based), `GetUnreadConversationCount`
- Internal event handlers: on `MessageSentDomainEvent`, publish `SendNotificationRequestedEvent` and `SendPushRequestedEvent` to Notifications for participant notification; publish `MessageSentIntegrationEvent` for any external consumers

**Infrastructure:**
- `MessagingQueryService` — Dapper read models for inbox, cursor-based message history, unread counts
- HTML sanitization for message bodies

**Outbound events (publishes):**
- `ConversationCreatedIntegrationEvent`
- `MessageSentIntegrationEvent`
- `SendNotificationRequestedEvent` (to Notifications, for participant in-app alerts)
- `SendPushRequestedEvent` (to Notifications, for participant push alerts)
- `SendEmailRequestedEvent` (to Notifications, if offline participants should get email)

**API controllers:**
- `ConversationsController` — full conversation CRUD with participant authorization

**Database schema:** `messaging`

**Feature flag:** `Modules.Messaging`

---

### Announcements Module

Owns tenant announcements and product changelog.

**Domain entities:**
- `Announcement` — aggregate root with Draft/Scheduled/Published/Expired/Archived lifecycle
- `AnnouncementDismissal` — per-user dismissal tracking
- `ChangelogEntry` — aggregate root owning `ChangelogItem` children

**Application layer:**
- Commands: `CreateAnnouncement`, `UpdateAnnouncement`, `PublishAnnouncement`, `ArchiveAnnouncement`, `DismissAnnouncement`, `CreateChangelogEntry`, `PublishChangelogEntry`
- Queries: `GetActiveAnnouncements`, `GetAllAnnouncements`, `GetChangelog`, `GetChangelogEntry`, `GetLatestChangelog`
- `AnnouncementTargetingService` — determines which users an announcement targets
- On publish: publishes `SendNotificationRequestedEvent` and/or `SendPushRequestedEvent` for pinned/alert announcements

**Outbound events (publishes):**
- `AnnouncementPublishedEvent`
- `SendNotificationRequestedEvent` (to Notifications, for pinned/alert announcements)
- `SendPushRequestedEvent` (to Notifications, for push-worthy announcements)

**API controllers:**
- `AdminAnnouncementsController` — admin CRUD + publish
- `AnnouncementsController` — user-facing active list + dismiss
- `AdminChangelogController` — admin changelog management
- `ChangelogController` — public changelog (anonymous access)

**Database schema:** `announcements`

**Feature flag:** `Modules.Announcements`

---

## Shared Contracts Reorganization

Current namespace structure under `Wallow.Shared.Contracts.Communications/` gets reorganized.

**Namespace convention:** `Shared.Contracts` follows the source-module convention where events live under the module that defines them. However, the `Send*RequestedEvent` types are **delivery API contracts** — they define the interface for requesting delivery from the Notifications module and are published by many different modules (Identity, Messaging, Announcements, Inquiries). These are grouped under `Delivery/` as a neutral, cross-cutting namespace that does not imply a single source module.

### Before

```
Shared.Contracts/
  Communications/
    Email/
      Events/SendEmailRequestedEvent.cs
      Events/EmailSentEvent.cs
      IEmailService.cs
    Sms/Events/SendSmsRequestedEvent.cs
    Push/Events/SendPushRequestedEvent.cs
    Notifications/Events/NotificationCreatedEvent.cs
    Announcements/Events/AnnouncementPublishedEvent.cs
    Messaging/Events/ConversationCreatedIntegrationEvent.cs
    Messaging/Events/MessageSentIntegrationEvent.cs
```

### After

```
Shared.Contracts/
  Delivery/                                            # Cross-cutting delivery API contracts
    Email/
      Events/SendEmailRequestedEvent.cs                # Published by any module
      Events/EmailSentEvent.cs                         # Published by Notifications after delivery
      IEmailService.cs                                 # Synchronous email interface
    Sms/Events/SendSmsRequestedEvent.cs                # Published by any module
    Push/Events/SendPushRequestedEvent.cs              # Published by any module
    InApp/Events/SendNotificationRequestedEvent.cs     # NEW - Published by any module
    InApp/Events/NotificationCreatedEvent.cs           # Published by Notifications after creation
  Messaging/                                           # Source: Messaging module
    Events/ConversationCreatedIntegrationEvent.cs
    Events/MessageSentIntegrationEvent.cs
  Announcements/                                       # Source: Announcements module
    Events/AnnouncementPublishedEvent.cs
```

### New Event: SendNotificationRequestedEvent

```csharp
namespace Wallow.Shared.Contracts.Delivery.InApp.Events;

public sealed record SendNotificationRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Type { get; init; }     // Must match NotificationType values (see below)
    public string? ActionUrl { get; init; }
    public string? SourceModule { get; init; }
    public bool IsCritical { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
```

### NotificationType Validation Contract

The `Type` field on `SendNotificationRequestedEvent` must correspond to the `NotificationType` enum values defined in the Notifications domain. To avoid coupling publishers to the domain enum, a static class of string constants is provided in `Shared.Contracts`:

```csharp
namespace Wallow.Shared.Contracts.Delivery.InApp;

public static class NotificationTypes
{
    public const string System = "System";
    public const string Message = "Message";
    public const string Announcement = "Announcement";
    public const string Security = "Security";
    // Add new types here as needed
}
```

The Notifications module's `SendNotificationRequestedEventHandler` validates the `Type` against known values and logs a warning for unrecognized types (does not dead-letter — creates the notification with the raw type string for forward compatibility).

### Updated Event: SendEmailRequestedEvent

Add `IsCritical` flag (same addition to `SendSmsRequestedEvent` and `SendPushRequestedEvent`):

```csharp
public sealed record SendEmailRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string To { get; init; }
    public string? From { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }         // Module-rendered content (inner)
    public string? SourceModule { get; init; }
    public Guid? CorrelationId { get; init; }
    public bool IsCritical { get; init; }              // NEW — bypasses preference checks
}
```

Same `IsCritical` addition to `SendSmsRequestedEvent` and `SendPushRequestedEvent`.

---

## Project Structure

### New directory layout

```
src/Modules/
  Notifications/
    Wallow.Notifications.Domain/
    Wallow.Notifications.Application/
    Wallow.Notifications.Infrastructure/
    Wallow.Notifications.Api/
  Messaging/
    Wallow.Messaging.Domain/
    Wallow.Messaging.Application/
    Wallow.Messaging.Infrastructure/
    Wallow.Messaging.Api/
  Announcements/
    Wallow.Announcements.Domain/
    Wallow.Announcements.Application/
    Wallow.Announcements.Infrastructure/
    Wallow.Announcements.Api/

tests/Modules/
  Notifications/Wallow.Notifications.Tests/
  Messaging/Wallow.Messaging.Tests/
  Announcements/Wallow.Announcements.Tests/
```

The `Communications` module directory is deleted entirely after the split.

### WallowModules.cs changes

```csharp
// Before
if (featureManager.IsEnabledAsync("Modules.Communications").GetAwaiter().GetResult())
    services.AddCommunicationsModule(configuration);

// After
if (featureManager.IsEnabledAsync("Modules.Notifications").GetAwaiter().GetResult())
    services.AddNotificationsModule(configuration);

if (featureManager.IsEnabledAsync("Modules.Messaging").GetAwaiter().GetResult())
    services.AddMessagingModule(configuration);

if (featureManager.IsEnabledAsync("Modules.Announcements").GetAwaiter().GetResult())
    services.AddAnnouncementsModule(configuration);
```

---

## Database Migration

### Schema changes

| Old Schema | New Schema | Tables Moving |
|-----------|-----------|--------------|
| `communications` | `notifications` | EmailMessages, SmsMessages, PushMessages, TenantPushConfigurations, DeviceRegistrations, Notifications, ChannelPreferences, EmailPreferences, SmsPreferences, TenantSettings, UserSettings |
| `communications` | `messaging` | Conversations, Participants, Messages |
| `communications` | `announcements` | Announcements, AnnouncementDismissals, ChangelogEntries, ChangelogItems |

**Note:** The `TenantSettings` and `UserSettings` tables from the keyed settings system move to the `notifications` schema since the settings key namespace changes to `notifications`.

Each new module gets an EF Core migration that:
1. Creates the new schema
2. Moves tables from `communications` to the new schema via `ALTER TABLE communications.X SET SCHEMA new_schema`
3. Creates the `__EFMigrationsHistory` table in the new schema

The old `communications` schema is dropped after all tables are moved.

---

## Event Handler Relocation

### Handlers moving OUT of Communications

| Handler | Current Location | New Location | Change |
|---------|-----------------|-------------|--------|
| `UserRegisteredEventHandler` (email) | Communications.Application | Identity.Application | See note 1 below |
| `UserRegisteredEventHandler` (in-app) | Communications.Application | Identity.Application | See note 1 below |
| `PasswordResetRequestedEventHandler` | Communications.Application | Identity.Application | See note 2 below |
| `InquirySubmittedEventHandler` | Communications.Application | Inquiries.Application | See note 3 below |
| `AnnouncementPublishedEventHandler` | Communications.Application | Announcements.Application | On publish, emits `SendNotificationRequestedEvent` / `SendPushRequestedEvent` for pinned/alert items |
| `MessageSentEventHandler` (notifications) | Communications.Application | Messaging.Application | See note 4 below |

**Note 1 — UserRegisteredEventHandler → Identity:**
This is the first integration-event handler in Identity.Application. Identity currently has no Wolverine CQRS handlers for integration events — only service account, SSO, and SCIM command handlers. Adding these handlers is a deliberate pattern expansion for the Identity module. The handler consumes `UserRegisteredEvent` (Identity's own domain event promoted to integration event), renders the welcome email HTML content, and publishes `SendEmailRequestedEvent` and `SendNotificationRequestedEvent`. These handlers reference only `Shared.Contracts` types — no cross-module dependency is introduced.

**Note 2 — PasswordResetRequestedEventHandler → Identity:**
**Behavioral change:** The current handler in Communications checks `EmailPreference` before sending the reset email. After the move, Identity publishes `SendEmailRequestedEvent` with `IsCritical = true`, which bypasses preference checks in the Notifications delivery layer. This is intentional — password reset emails are security-critical and must always be delivered. The `IEmailTemplateService` for rendering the reset email template moves to Identity.Infrastructure (or Identity renders the content inline). Identity does NOT need `IEmailPreferenceRepository` — preference checking is handled by Notifications.

**Note 3 — InquirySubmittedEventHandler → Inquiries:**
The current handler already publishes `SendEmailRequestedEvent` (it does not call `IEmailService` directly). It sends two emails: one to the configured admin and one confirmation to the submitter. After the move, the handler lives in Inquiries.Application and consumes `InquirySubmittedEvent` (the integration event from `Shared.Contracts.Inquiries.Events`). Note: Inquiries already has `InquirySubmittedDomainEventHandler` that publishes `InquirySubmittedEvent`. The relocated handler consumes the integration event (not the domain event) — these are two different handlers in the event chain: domain event → integration event → email requests.

**Note 4 — MessageSentEventHandler → Messaging:**
The current handler calls `INotificationService.SendToUserAsync(...)` directly (backed by SignalR). After the move, this handler **must drop the `INotificationService` dependency** and instead publish `SendNotificationRequestedEvent` via `IMessageBus.PublishAsync`. The handler still accesses `IConversationRepository` (which is fine — Messaging owns it) to load conversation/message data for constructing the notification content. It also publishes `MessageSentIntegrationEvent` for external consumers.

### Handlers staying in Notifications

| Handler | Purpose |
|---------|---------|
| `SendEmailRequestedEventHandler` | Consumes event, checks preferences (unless `IsCritical`), wraps in layout, sends via SMTP |
| `SendSmsRequestedEventHandler` | Consumes event, checks preferences (unless `IsCritical`), sends via Twilio |
| `SendPushRequestedEventHandler` | Consumes event, checks preferences (unless `IsCritical`), sends via platform provider |
| `SendNotificationRequestedEventHandler` (new) | Consumes event, checks preferences (unless `IsCritical`), creates InApp record, pushes via SignalR |

---

## Email Template Architecture

### Two-layer system

```
┌─────────────────────────────────────────┐
│           Shared Email Layout           │
│  ┌───────────────────────────────────┐  │
│  │         Header (logo, nav)        │  │
│  ├───────────────────────────────────┤  │
│  │                                   │  │
│  │    Module-rendered content body   │  │
│  │    (passed via SendEmailEvent)    │  │
│  │                                   │  │
│  ├───────────────────────────────────┤  │
│  │    Footer (unsubscribe, legal)    │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

- **Modules** render their content block (HTML fragment) and include it in `SendEmailRequestedEvent.Body`
- **Notifications** wraps the body in the shared layout before SMTP delivery
- The shared layout is owned by the Notifications module's infrastructure layer
- Layout customization per tenant is possible via tenant settings

---

## Testing Strategy

Each new module gets its own test project mirroring the source structure:

- **Domain tests** — entity behavior, value object validation, domain events
- **Application tests** — command/query handlers, validators, event handlers
- **Infrastructure tests** — repository tests, service tests (SMTP, Twilio, push providers for Notifications; Dapper queries for Messaging)
- **API tests** — controller tests, contract tests

Existing tests are moved to the appropriate new test project. Test namespaces update to match.

---

## Configuration Changes

### appsettings.json

```jsonc
{
  "FeatureManagement": {
    "Modules.Notifications": true,    // replaces Modules.Communications
    "Modules.Messaging": true,        // new
    "Modules.Announcements": true     // new
  },
  // Existing Communications config keys remain but are consumed by Notifications
  "Communications": {
    "Email": { /* SMTP settings */ },
    "Twilio": { /* SMS settings */ },
    "Push": { /* push settings */ }
  }
  // Consider renaming to "Notifications" in a follow-up
}
```

### Settings keys

- `communications.email_sender_name` → `notifications.email_sender_name`
- `communications.notification_preferences` → `notifications.notification_preferences`

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Large PR is hard to review | Organize commits logically: contracts first, then each module, then cleanup |
| Database migration on production data | Use `ALTER TABLE SET SCHEMA` (zero-copy in PostgreSQL) rather than recreating tables |
| Broken event routing after namespace changes | Wolverine auto-discovers handlers by assembly scanning; verify all handler assemblies are registered |
| Missing cross-module references | Architecture tests should catch invalid module references |
| IEmailService interface in Shared.Contracts | Keep it for now; modules that need synchronous email can still use it, delivered by Notifications |
| Feature flag disabling drops event handlers | Disabling `Modules.Notifications` silently drops all delivery handlers — email, SMS, push, and in-app will stop working. This is critical to document in operational runbooks. Disabling `Modules.Messaging` or `Modules.Announcements` is safer (only their own features stop). |
| Identity module pattern expansion | Moving `UserRegisteredEventHandler` and `PasswordResetRequestedEventHandler` to Identity introduces the first integration-event handlers in that module. This is a deliberate architectural decision that should be documented in Identity's module docs. |
| Vestigial config sections | Remove `Wallow.Modules.Communications` from `appsettings.json` if present. The `Wallow.Modules` section is not used by `WallowModules.cs` (which reads `FeatureManagement`). Clean it up or remove it in this PR. |
