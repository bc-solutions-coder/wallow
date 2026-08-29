# Notifications Module — Agent Guide

## Module Role

A **consumer-only** module: it publishes no integration events. It listens to events from
Identity, Announcements, and Inquiries via Wolverine and dispatches notifications through email,
SMS, in-app, and push channels.

## Code Organization

The Application layer is organized by **channel** (`Email`, `InApp`, `Push`, `Sms`) rather than
by entity — each channel directory has its own `Commands/`, `Queries/`, `DTOs/`, `Interfaces/`,
and `Mappings/`. A cross-channel `Preferences` directory handles global channel enable/disable.
Integration-event handlers live in a flat `EventHandlers/` directory at the Application root,
not inside channels.

## Adding an Event Handler

1. Create a static handler class in `Application/EventHandlers/` with
   `public static async Task Handle(EventType message, IMessageBus bus)` — Wolverine
   auto-discovers it, no registration.
2. Check user preferences via `INotificationPreferenceChecker` before sending when appropriate.
3. Naming: `{EventName}NotificationHandler.cs` (email), `{EventName}InAppHandler.cs` (in-app),
   `{EventName}SseHandler.cs` (SSE real-time).

## Provider Pattern

Each channel has an abstraction (`IEmailProvider`, `ISmsProvider`, `IPushProvider`):

- **Email**: `SmtpEmailProvider` (default), wrapped by `EmailProviderAdapter` implementing `IEmailService`
- **SMS**: `TwilioSmsProvider` (when configured) or `NullSmsProvider` (fallback)
- **Push**: `FcmPushProvider`, `ApnsPushProvider`, `WebPushPushProvider`, `LogPushProvider` —
  selected by `PushProviderFactory` based on `PushPlatform`
- **In-App real-time**: `SseNotificationService` using `ISseDispatcher` from
  `Wallow.Shared.Contracts.Realtime` — separate from persistent in-app notifications

## Conventions

- Message entities (EmailMessage, SmsMessage, PushMessage) follow
  `Pending → Sent/Delivered | Failed` with retry support (`CanRetry(maxRetries)`,
  `ResetForRetry()`); all implement `ITenantScoped`.
- Entities use strongly-typed IDs internally; integration events use plain `Guid`.
- `RetryFailedEmailsJob` is registered as scoped but invoked externally, not auto-scheduled here.
- Push credentials are encrypted via `IPushCredentialEncryptor` (ASP.NET Data Protection);
  `TenantPushConfiguration` stores per-tenant, per-platform credentials.
- Email templates go through `IEmailTemplateService` (`SimpleEmailTemplateService`).

## Database

Schema: `notifications`; context: `NotificationsDbContext`.

## Testing

`./scripts/run-tests.sh notifications`
