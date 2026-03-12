# Communications Module Split — Design Spec v2 (Reactive Notifications)

**Date:** 2026-03-12
**Status:** Draft
**Migration Strategy:** Big bang (single PR)
**Supersedes:** `docs/plans/2026-03-12-communications-module-split-design.md`

## Overview

Split the Communications module into three focused modules: **Notifications**, **Messaging**, and **Announcements**.

The Notifications module is a pure reactive delivery engine. It subscribes to domain events published by other modules and owns all notification logic: routing decisions, content rendering, preference checking, and delivery. No module sends delivery commands — modules publish domain events about things that happened, and Notifications reacts.

## Key Architectural Principle

**Events describe facts, not commands.** The original design used `SendEmailRequestedEvent`, `SendSmsRequestedEvent`, and `SendPushRequestedEvent` — imperative commands disguised as events. The revised design eliminates these entirely. Modules publish real domain events (`UserRegisteredEvent`, `InquirySubmittedEvent`, etc.) and Notifications decides what to send.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Notification ownership | Notifications owns routing, content rendering, and delivery | Single place for all notification logic; modules stay focused on their domain |
| Event pattern | Reactive — Notifications subscribes to domain events | True event-driven architecture; events describe facts, not commands |
| Handler location | All notification handlers in Notifications.Application | Centralized, easy to audit, no notification concerns leak into source modules |
| Handler implementation | Code-based Wolverine handlers per event | Explicit, testable, type-safe; easy to see what every event triggers |
| Coupling direction | Notifications references Shared.Contracts from all modules | Standard fan-in pattern; same direction all modules already use |
| Send*RequestedEvent | Eliminated from Shared.Contracts | These were commands, not events; they violate event-driven principles |
| IEmailService | Moved to Notifications.Application (internal) | Not a shared contract; only Notifications delivers email |
| Preference bypass | Handler-level decision, no flag on events | Security-critical handlers simply skip the preference check |
| Template ownership | Notifications owns all templates and rendering | Modules don't construct email bodies or notification text |
| Confirmation events | Published per delivery action, not batched | Each email/SMS/push/in-app delivery publishes its own confirmation event immediately |
| Cross-module communication | Integration events only | Fully decoupled, consistent with existing inter-module pattern |
| Feature flags | Separate per module | `Modules.Notifications`, `Modules.Messaging`, `Modules.Announcements` |
| Real-time (SignalR) | Notifications owns all real-time delivery | Single hub, single source for frontend subscriptions |

---

## Shared Contracts Reorganization

### Before (current)

```
Shared.Contracts/
  Communications/
    Email/
      Events/SendEmailRequestedEvent.cs     ← command-as-event (DELETE)
      Events/EmailSentEvent.cs              ← keep, move
      IEmailService.cs                      ← move to Notifications internal
    Sms/Events/SendSmsRequestedEvent.cs     ← command-as-event (DELETE)
    Push/Events/SendPushRequestedEvent.cs   ← command-as-event (DELETE)
    Notifications/Events/NotificationCreatedEvent.cs  ← keep, move
    Messaging/Events/ConversationCreatedIntegrationEvent.cs  ← keep, move
    Messaging/Events/MessageSentIntegrationEvent.cs          ← keep, move
    Announcements/Events/AnnouncementPublishedEvent.cs       ← keep, move
```

### After (new)

```
Shared.Contracts/
  Notifications/                                    # Source: Notifications module
    Events/EmailSentEvent.cs                        # Published after email delivery
    Events/SmsSentEvent.cs                          # Published after SMS delivery (NEW)
    Events/PushSentEvent.cs                         # Published after push delivery (NEW)
    Events/NotificationCreatedEvent.cs              # Published after in-app creation
  Messaging/                                        # Source: Messaging module
    Events/ConversationCreatedIntegrationEvent.cs
    Events/MessageSentIntegrationEvent.cs
  Announcements/                                    # Source: Announcements module
    Events/AnnouncementPublishedEvent.cs
```

### Deleted contracts

- `SendEmailRequestedEvent` — command-as-event, eliminated
- `SendSmsRequestedEvent` — command-as-event, eliminated
- `SendPushRequestedEvent` — command-as-event, eliminated
- `IEmailService` — moved to Notifications.Application as internal interface
- `SendNotificationRequestedEvent` — never created (was in original plan)
- `NotificationTypes` constants — internal to Notifications only

### New contracts

- `SmsSentEvent` — published by Notifications after SMS delivery
- `PushSentEvent` — published by Notifications after push delivery

### Enriched contracts

Events must carry enough data for Notifications to act without querying other modules' repositories.

- `MessageSentIntegrationEvent` — add `List<Guid> ParticipantIds` (excluding sender). Messaging's domain event handler enriches the integration event by loading the conversation's participant list before publishing. This avoids Notifications needing access to `IConversationRepository`.
- `AnnouncementPublishedEvent` — add `List<Guid> TargetUserIds`. Announcements' domain event handler uses `AnnouncementTargetingService` to resolve target users before publishing. For "All" targeting in large tenants, this may be a large list — acceptable for the initial implementation; optimize with a broadcast pattern in a follow-up if needed.
- `InquirySubmittedEvent` — must include `AdminEmail` (the configured admin address for inquiry notifications). The Inquiries module decides who receives inquiry notifications; Notifications just handles delivery. This avoids Notifications reading `Inquiries:AdminEmail` from configuration.

---

## Module Boundaries

### Notifications Module

Pure reactive delivery engine. Subscribes to domain events from other modules, decides what to send, renders content, checks preferences, delivers via the appropriate channel.

**Domain entities:**
- `EmailMessage` — aggregate root with Pending/Sent/Failed lifecycle, 3-retry max
- `SmsMessage` — aggregate root, E.164 validation, 1600-char limit
- `PushMessage` — outbound push with status tracking
- `TenantPushConfiguration` — encrypted credentials per platform (FCM/APNs/WebPush) per tenant
- `DeviceRegistration` — user device tokens
- `Notification` — in-app notification with read/archive support
- `ChannelPreference` — per-user preference keyed by (UserId, ChannelType, NotificationType)
- `EmailPreference`, `SmsPreference` — legacy per-channel preferences

**Value objects:** `EmailAddress`, `EmailContent`, `PhoneNumber`

**Enums:** `EmailStatus`, `SmsStatus`, `PushStatus`, `PushPlatform` (Fcm, Apns, WebPush), `NotificationType`, `ChannelType`

**Note on legacy preferences:** `EmailPreference` and `SmsPreference` are retained as-is in this phase. Migration to `ChannelPreference` is deferred to a follow-up.

**Application layer — Event Handlers (reactive):**

| Domain Event (consumed) | Source Module | Handler | Actions |
|---|---|---|---|
| `UserRegisteredEvent` | Identity | `UserRegisteredNotificationHandler` | Welcome email + in-app notification |
| `PasswordResetRequestedEvent` | Identity | `PasswordResetNotificationHandler` | Password reset email (no preference check — security-critical) |
| `InquirySubmittedEvent` | Inquiries | `InquirySubmittedNotificationHandler` | Admin email + submitter confirmation email |
| `AnnouncementPublishedEvent` | Announcements | `AnnouncementPublishedNotificationHandler` | In-app notification + push (for pinned/alert types) |
| `MessageSentIntegrationEvent` | Messaging | `MessageSentNotificationHandler` | In-app notification + push for participants (except sender) |
| `InvoiceOverdueEvent` | Billing | `InvoiceOverdueNotificationHandler` | Overdue email to tenant admin **(NEW — not present in current Communications module)** |

Each handler:
1. Receives the domain event
2. Decides which channels to use (business logic lives here)
3. Checks user preferences per channel (unless security-critical)
4. Renders content (email template, notification text)
5. Delivers via internal services (SMTP, Twilio, push providers, SignalR)
6. Publishes confirmation event immediately after each successful delivery

**Application layer — Internal interfaces:**
- `IEmailService` — SMTP delivery (moved from Shared.Contracts)
- `INotificationPreferenceChecker` — checks user channel preferences
- `INotificationService` — SignalR real-time delivery

**Infrastructure:**
- SMTP/MailKit with Polly resilience (exponential backoff, 3 retries, 30s timeout)
- `SmtpConnectionPool` (singleton)
- Twilio SMS provider (or `NullSmsProvider` fallback)
- FCM, APNs, WebPush providers via `PushProviderFactory`
- `PushCredentialEncryptor` (DataProtection)
- `SignalRNotificationService` — real-time delivery via `IRealtimeDispatcher`
- `RetryFailedEmailsJob` — background retry for failed emails
- Email templates (owned by Notifications)

**Outbound events (publishes):**
- `EmailSentEvent` — after each email delivery
- `SmsSentEvent` — after each SMS delivery
- `PushSentEvent` — after each push delivery
- `NotificationCreatedEvent` — after each in-app notification creation

**API controllers:**
- `PushDevicesController` — device registration CRUD
- `PushConfigurationController` — admin tenant push config
- `NotificationsController` — in-app notification read/archive/unread-count
- `UserNotificationSettingsController` — preference management
- `NotificationsSettingsController` — module settings

**Database schema:** `notifications`

**Feature flag:** `Modules.Notifications`

**Settings key namespace:** `notifications`

---

### Messaging Module

Owns user-to-user direct and group conversations. Publishes domain events — does not think about notifications.

**Domain entities:**
- `Conversation` — aggregate root with `CreateDirect` / `CreateGroup` factory methods
- `Participant` — child entity with active/inactive status, read tracking
- `Message` — child entity

**Application layer:**
- Commands: `CreateConversation`, `SendMessage`, `MarkConversationRead`
- Queries: `GetConversations`, `GetMessages` (cursor-based), `GetUnreadConversationCount`
- Domain event handlers: `ConversationCreatedDomainEvent` → publishes `ConversationCreatedIntegrationEvent`; `MessageSentDomainEvent` → publishes `MessageSentIntegrationEvent`

**Infrastructure:**
- `MessagingQueryService` — Dapper read models for inbox, cursor-based message history, unread counts
- HTML sanitization for message bodies

**Outbound events (publishes):**
- `ConversationCreatedIntegrationEvent`
- `MessageSentIntegrationEvent`

**API controllers:**
- `ConversationsController` — full conversation CRUD with participant authorization

**Database schema:** `messaging`

**Feature flag:** `Modules.Messaging`

---

### Announcements Module

Owns tenant announcements and product changelog. Publishes domain events — does not think about notifications.

**Domain entities:**
- `Announcement` — aggregate root with Draft/Scheduled/Published/Expired/Archived lifecycle
- `AnnouncementDismissal` — per-user dismissal tracking
- `ChangelogEntry` — aggregate root owning `ChangelogItem` children

**Application layer:**
- Commands: `CreateAnnouncement`, `UpdateAnnouncement`, `PublishAnnouncement`, `ArchiveAnnouncement`, `DismissAnnouncement`, `CreateChangelogEntry`, `PublishChangelogEntry`
- Queries: `GetActiveAnnouncements`, `GetAllAnnouncements`, `GetChangelog`, `GetChangelogEntry`, `GetLatestChangelog`
- `AnnouncementTargetingService` — determines which users an announcement targets
- Domain event handler: `AnnouncementPublishedDomainEvent` → publishes `AnnouncementPublishedEvent`

**Outbound events (publishes):**
- `AnnouncementPublishedEvent`

**API controllers:**
- `AdminAnnouncementsController` — admin CRUD + publish
- `AnnouncementsController` — user-facing active list + dismiss
- `AdminChangelogController` — admin changelog management
- `ChangelogController` — public changelog (anonymous access)

**Database schema:** `announcements`

**Feature flag:** `Modules.Announcements`

---

## Event Flow Examples

### User Registration
```
Identity Module                    Notifications Module
─────────────                      ────────────────────
User signs up
  → raises UserRegisteredEvent ──→ UserRegisteredNotificationHandler
                                     → check email preferences
                                     → render welcome email template
                                     → send via SMTP
                                     → publishes EmailSentEvent
                                     → create in-app Notification entity
                                     → push via SignalR
                                     → publishes NotificationCreatedEvent
```

### Password Reset (security-critical)
```
Identity Module                    Notifications Module
─────────────                      ────────────────────
User requests reset
  → raises PasswordResetRequestedEvent ──→ PasswordResetNotificationHandler
                                            → NO preference check (security-critical)
                                            → render reset email with token
                                            → send via SMTP
                                            → publishes EmailSentEvent
```

### Message Sent
```
Messaging Module                   Notifications Module
────────────────                   ────────────────────
User sends message
  → raises MessageSentIntegrationEvent ──→ MessageSentNotificationHandler
                                            → for each participant (except sender):
                                              → check in-app preferences
                                              → create Notification entity
                                              → push via SignalR
                                              → publishes NotificationCreatedEvent
                                              → check push preferences
                                              → send push notification
                                              → publishes PushSentEvent
```

### Inquiry Submitted
```
Inquiries Module                   Notifications Module
────────────────                   ────────────────────
Form submitted
  → raises InquirySubmittedEvent ──→ InquirySubmittedNotificationHandler
                                      → render admin notification email
                                      → send to admin via SMTP
                                      → publishes EmailSentEvent
                                      → render submitter confirmation email
                                      → send to submitter via SMTP
                                      → publishes EmailSentEvent
```

### Announcement Published
```
Announcements Module               Notifications Module
────────────────────               ────────────────────
Admin publishes announcement
  → raises AnnouncementPublishedEvent ──→ AnnouncementPublishedNotificationHandler
                                           → filter: only pinned or Alert type
                                           → resolve target users via event data
                                           → for each target user:
                                             → create Notification entity
                                             → push via SignalR
                                             → publishes NotificationCreatedEvent
                                             → send push notification
                                             → publishes PushSentEvent
```

---

## What Does NOT Happen

```
❌ Any module publishes SendEmailRequestedEvent
❌ Any module constructs email bodies or notification text
❌ Any module decides which notification channels to use
❌ Any module checks notification preferences
❌ Identity or Inquiries modules have notification handler code
```

---

## Delta From Original Design (v1)

| Aspect | Original (v1) | Revised (v2) |
|---|---|---|
| **Shared Contracts** | New `Delivery/` namespace with `Send*RequestedEvent` types | Eliminate `Delivery/` entirely; add `Notifications/` with `*SentEvent` types |
| **IEmailService** | Kept in Shared.Contracts | Moved to Notifications.Application (internal) |
| **Event pattern** | Modules render content, publish commands | Modules publish domain events, Notifications reacts |
| **Handler location** | Spread across modules (Identity, Inquiries, Announcements own handlers) | All notification handlers centralized in Notifications |
| **IsCritical flag** | On Send*RequestedEvent DTOs | Handler-level decision; no flag needed on events |
| **SendNotificationRequestedEvent** | New contract in Shared.Contracts | Does not exist |
| **NotificationTypes constants** | In Shared.Contracts for publishers | Internal to Notifications only |
| **Template ownership** | Modules render content, Notifications wraps in layout | Notifications owns full template rendering |
| **Identity module changes** | Gets new Wolverine handlers + package reference | No changes needed |
| **Inquiries module changes** | Gets new handler for email | No changes needed |
| **Messaging event handler** | Rewired to publish Send*RequestedEvent | Publishes MessageSentIntegrationEvent only |
| **New outbound events** | None | `SmsSentEvent`, `PushSentEvent` |
| **New handlers (scope expansion)** | N/A | `InvoiceOverdueNotificationHandler` — net-new, not a migration from Communications |

### What stays the same from v1
- Module split into Notifications, Messaging, Announcements (3 modules)
- Clean Architecture per module (Domain → Application → Infrastructure → Api)
- Database schema-per-module with `ALTER TABLE SET SCHEMA` migrations
- Feature flags per module
- All domain entities, value objects, repositories unchanged
- Delivery infrastructure (SMTP, Twilio, push providers, SignalR) unchanged
- API controllers unchanged

---

## Project Structure

### New directory layout

```
src/Modules/
  Notifications/
    Foundry.Notifications.Domain/
    Foundry.Notifications.Application/
    Foundry.Notifications.Infrastructure/
    Foundry.Notifications.Api/
  Messaging/
    Foundry.Messaging.Domain/
    Foundry.Messaging.Application/
    Foundry.Messaging.Infrastructure/
    Foundry.Messaging.Api/
  Announcements/
    Foundry.Announcements.Domain/
    Foundry.Announcements.Application/
    Foundry.Announcements.Infrastructure/
    Foundry.Announcements.Api/

tests/Modules/
  Notifications/Foundry.Notifications.Tests/
  Messaging/Foundry.Messaging.Tests/
  Announcements/Foundry.Announcements.Tests/
```

The `Communications` module directory is deleted entirely after the split.

### FoundryModules.cs changes

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

Same pattern for `InitializeFoundryModulesAsync` — replace `InitializeCommunicationsModuleAsync` with three new initialization calls.

### Wolverine handler assembly registration

Wolverine auto-discovers handlers via `opts.Discovery.IncludeAssembly(assembly)` for all assemblies matching `Foundry.*`. The three new Application assemblies are discovered automatically:
- `Foundry.Notifications.Application` — contains all reactive notification handlers
- `Foundry.Messaging.Application` — contains domain event → integration event handlers
- `Foundry.Announcements.Application` — contains domain event → integration event handlers

No manual registration changes are needed in `Program.cs` because the existing wildcard pattern (`a.GetName().Name?.StartsWith("Foundry.")`) matches the new assemblies. Verify this during integration testing.

---

## Database Migration

Fresh database assumed — no data migration from the old `communications` schema. Each module gets a standard EF Core initial migration that creates its schema and tables from scratch.

### Schemas

| Module | Schema | Tables |
|---|---|---|
| Notifications | `notifications` | EmailMessages, SmsMessages, PushMessages, TenantPushConfigurations, DeviceRegistrations, Notifications, ChannelPreferences, EmailPreferences, SmsPreferences, TenantSettings*, UserSettings* |
| Messaging | `messaging` | Conversations, Participants, Messages |
| Announcements | `announcements` | Announcements, AnnouncementDismissals, ChangelogEntries, ChangelogItems |

*`TenantSettings` and `UserSettings` are shared keyed-settings infrastructure tables. They are not Notifications domain entities but reside in the `notifications` schema because the settings key namespace changes to `notifications`.

Each module gets a single initial migration generated via:
```bash
dotnet ef migrations add Initial{Module}Schema \
    --project src/Modules/{Module}/Foundry.{Module}.Infrastructure \
    --startup-project src/Foundry.Api \
    --context {Module}DbContext
```

---

## Configuration Changes

### appsettings.json

```jsonc
{
  "FeatureManagement": {
    "Modules.Notifications": true,    // replaces Modules.Communications
    "Modules.Messaging": true,        // new
    "Modules.Announcements": true     // new
  }
}
```

### Settings keys

- `communications.email_sender_name` → `notifications.email_sender_name`
- `communications.notification_preferences` → `notifications.notification_preferences`

---

## Email Template Rendering

Notifications owns all email templates and rendering. Templates are stored as embedded resources in `Notifications.Infrastructure/Templates/`.

Each notification handler specifies which template to use and provides a model with the data to render. The rendering pipeline:
1. Handler creates a typed model from the event data (e.g., `WelcomeEmailModel { FirstName, LastName, Email }`)
2. Handler calls `IEmailTemplateService.RenderAsync(templateName, model)`
3. Template service loads the embedded template and renders it with the model
4. Handler passes the rendered HTML to `IEmailService` for delivery

Template registry (handler → template mapping):
- `UserRegisteredNotificationHandler` → `WelcomeEmail` template
- `PasswordResetNotificationHandler` → `PasswordReset` template
- `InquirySubmittedNotificationHandler` → `InquiryAdminNotification` + `InquiryConfirmation` templates
- `InvoiceOverdueNotificationHandler` → `InvoiceOverdue` template

Data needed from events: Each event must carry enough data for Notifications to render the template without querying other modules. For example, `InquirySubmittedEvent` must include the submitter's name and email, the inquiry content, and the admin email address. See "Enriched contracts" section above.

---

## Feature Flag Behavior

When `Modules.Notifications` is disabled:
- Notification handlers are not registered with Wolverine
- Domain events from other modules are still published to RabbitMQ
- Events queue in RabbitMQ (queues are declared at application startup regardless of feature flag state)
- When the module is re-enabled, queued events are processed
- **Operational impact:** All email, SMS, push, and in-app delivery stops. This must be documented in operational runbooks.

When `Modules.Messaging` or `Modules.Announcements` is disabled:
- Only that module's features stop
- Events already published continue to be processed by Notifications
- No cross-module impact

---

## Risk Mitigation

| Risk | Mitigation |
|---|---|
| Large PR is hard to review | Organize commits logically: contracts first, then each module, then cleanup |
| Database migration | Fresh database assumed; standard EF Core initial migrations per module |
| Broken event routing after namespace changes | Wolverine auto-discovers handlers by assembly scanning; verify all handler assemblies are registered |
| Missing cross-module references | Architecture tests catch invalid module references |
| Notifications becomes a bottleneck | Each handler is independent; scale via Wolverine's parallelism and RabbitMQ consumer concurrency |
| Feature flag disabling drops handlers | Disabling `Modules.Notifications` silently stops all delivery. Document in operational runbooks. Disabling Messaging or Announcements is safer (only their features stop). |
| Adding notifications for new events | Requires a code change in Notifications module. This is deliberate — notification behavior should be explicit and testable, not implicit. |

---

## Testing Strategy

Each new module gets its own test project mirroring the source structure:

- **Domain tests** — entity behavior, value object validation, domain events
- **Application tests** — command/query handlers, validators, event handlers
- **Infrastructure tests** — repository tests, service tests (SMTP, Twilio, push providers for Notifications; Dapper queries for Messaging)
- **API tests** — controller tests, contract tests

Existing tests move to the appropriate new test project. Test namespaces update to match.

Notification handler tests verify:
- Correct channels are triggered for each event
- Preferences are checked (or bypassed for security-critical)
- Correct template is rendered with correct data
- Confirmation events are published after each delivery
