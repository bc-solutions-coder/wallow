# Notifications Module

## Overview

The Notifications module handles multi-channel notification delivery (email, SMS, in-app, push) and user notification preferences. It is a consumer-only module: it listens to integration events from other modules via Wolverine in-memory messaging and dispatches notifications through the appropriate channels.

Each notification channel has its own domain entities, delivery pipeline, and status tracking with retry support. Users can configure per-channel and per-notification-type preferences to control what they receive.

## Architecture

```
src/Modules/Notifications/
+-- Wallow.Notifications.Domain         # Entities, Value Objects, Enums, Domain Events
+-- Wallow.Notifications.Application    # Commands, Queries, Handlers, Event Handlers, DTOs
+-- Wallow.Notifications.Infrastructure # EF Core, Repositories, Provider Integrations, Jobs
+-- Wallow.Notifications.Api            # Controllers, Request/Response Contracts
```

**Database Schema**: `notifications` (PostgreSQL)

## Notification Channels

### Email

- **EmailMessage** (Aggregate Root): Tracks email delivery with subject, body, recipient, status, and retry count.
- **EmailPreference**: Per-user, per-notification-type opt-in/out for email delivery.
- **Value Objects**: `EmailAddress` (validated format), `EmailContent` (subject + body).
- **Status**: `Pending -> Sent | Failed` (failed messages can be retried up to 3 times).
- **Provider**: Configurable via `Notifications:Email:Provider` (defaults to SMTP). Uses Polly resilience pipeline.
- **Background Job**: `RetryFailedEmailsJob` retries failed emails that haven't exceeded the retry limit.

### SMS

- **SmsMessage** (Aggregate Root): Tracks SMS delivery with phone number, body (max 1600 chars), status, and retry count.
- **Value Object**: `PhoneNumber` (E.164 format validation).
- **Status**: `Pending -> Sent | Failed`.
- **Provider**: Twilio (configured via `TwilioSettings`), falls back to `NullSmsProvider` when unconfigured.

### In-App

- **Notification** (Aggregate Root): Persistent in-app notification with title, message, type, read/archive state, optional action URL, and source module tracking.
- **Real-time Delivery**: SSE (Server-Sent Events) via `ISseDispatcher` for instant user and tenant-wide broadcasts.

### Push

- **PushMessage** (Aggregate Root): Tracks push notification delivery with status and retry support.
- **DeviceRegistration** (Entity): Maps a user's device token to a platform (FCM, APNS, WebPush).
- **TenantPushConfiguration** (Aggregate Root): Per-tenant push credentials with encryption, per-platform enable/disable.
- **Providers**: `FcmPushProvider`, `ApnsPushProvider`, `WebPushPushProvider`, `LogPushProvider` (fallback). Selected via `PushProviderFactory`.

## User Preferences

- **ChannelPreference**: Per-user, per-channel, per-notification-type granular enable/disable control.
- **NotificationPreferenceChecker**: Checks preferences before dispatching to determine if a user should receive a notification on a given channel.

## Enums

| Enum | Values |
|------|--------|
| `NotificationType` | TaskAssigned, TaskCompleted, TaskComment, SystemAlert, BillingInvoice, Mention, Announcement, SystemNotification, InquirySubmitted, InquiryComment |
| `ChannelType` | Email, Sms, InApp, Push, Webhook |
| `EmailStatus` | Pending, Sent, Failed |
| `SmsStatus` | Pending, Sent, Failed |
| `PushStatus` | Pending, Delivered, Failed |
| `PushPlatform` | Fcm, Apns, WebPush |

## Integration Events Consumed

This module handles events from other modules via Wolverine. All event types are defined in `Wallow.Shared.Contracts`.

| Source Module | Events |
|---------------|--------|
| Identity | `EmailVerificationRequestedEvent`, `EmailVerifiedEvent`, `UserRegisteredEvent`, `PasswordChangedEvent`, `PasswordResetEvent`, `UserRoleChangedEvent`, `OrganizationCreatedEvent`, `OrganizationMemberAddedEvent`, `OrganizationMemberRemovedEvent` |
| Billing | `InvoicePaidEvent`, `InvoiceOverdueEvent`, `PaymentReceivedEvent` |
| Announcements | `AnnouncementPublishedEvent` |
| Inquiries | `InquirySubmittedEvent`, `InquiryCommentAddedEvent`, `InquiryStatusChangedEvent` |
| Messaging | `MessageSentEvent` |

## API Endpoints

All endpoints require authentication.

### In-App Notifications (`/api/v1/notifications`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/notifications` | Get paginated notification history |
| `GET` | `/api/v1/notifications/unread-count` | Get unread count |
| `POST` | `/api/v1/notifications/{id}/read` | Mark notification as read |
| `POST` | `/api/v1/notifications/read-all` | Mark all as read |

### Notification Settings (`/api/v1/notification-settings`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/notification-settings` | Get user notification settings |
| `PUT` | `/api/v1/notification-settings/channel` | Enable/disable a channel |
| `PUT` | `/api/v1/notification-settings/type` | Enable/disable a notification type per channel |

### Push Devices (`/api/v1/push`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/v1/push/devices` | Register a device |
| `DELETE` | `/api/v1/push/devices/{id}` | Deregister a device |
| `GET` | `/api/v1/push/devices` | Get user's registered devices |
| `POST` | `/api/v1/push/send` | Send a push notification |

### Push Configuration - Admin (`/api/v1/admin/push/config`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/admin/push/config` | Get tenant push config |
| `PUT` | `/api/v1/admin/push/config` | Upsert tenant push config |
| `PATCH` | `/api/v1/admin/push/config/enabled` | Enable/disable push for a platform |
| `DELETE` | `/api/v1/admin/push/config/{platform}` | Remove tenant push config |

## Configuration

| Section | Purpose |
|---------|---------|
| `ConnectionStrings:DefaultConnection` | Shared PostgreSQL connection |
| `Smtp` | SMTP email provider settings |
| `Notifications:Email:Provider` | Email provider selection (default: `Smtp`) |
| `TwilioSettings` | Twilio SMS credentials (optional; `NullSmsProvider` used when absent) |
| `PushSettings` | Push notification settings |

## Dependencies

| Project | Purpose |
|---------|---------|
| `Wallow.Shared.Kernel` | Base entities, value objects, multi-tenancy, Result pattern, identity types |
| `Wallow.Shared.Contracts` | Integration event definitions from other modules |
| `Wallow.Shared.Infrastructure.Core` | `TenantAwareDbContext`, resilience pipelines, read DB context |

## Testing

```bash
./scripts/run-tests.sh notifications
```

## EF Core Migrations

```bash
dotnet ef migrations add MigrationName \
    --project src/Modules/Notifications/Wallow.Notifications.Infrastructure \
    --startup-project src/Wallow.Api \
    --context NotificationsDbContext
```
