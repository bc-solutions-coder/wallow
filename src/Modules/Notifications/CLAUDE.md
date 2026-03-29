# Notifications Module - Agent Guide

## Module Role

This is a **consumer-only** module. It does not publish integration events to other modules. It listens to events from Identity, Billing, Announcements, Inquiries, and Messaging modules via Wolverine in-memory messaging and dispatches notifications through email, SMS, in-app, and push channels.

## Key File Locations

- **Domain entities**: `Wallow.Notifications.Domain/Channels/{Email,InApp,Push,Sms}/Entities/`
- **Value objects**: `Wallow.Notifications.Domain/Channels/{Email,Sms}/ValueObjects/`
- **Enums**: `Wallow.Notifications.Domain/Enums/NotificationType.cs`, `Wallow.Notifications.Domain/Preferences/ChannelType.cs`, plus per-channel status/platform enums
- **Event handlers** (integration event consumers): `Wallow.Notifications.Application/EventHandlers/`
- **Channel commands/queries**: `Wallow.Notifications.Application/Channels/{Email,InApp,Push,Sms}/`
- **Preference commands/queries**: `Wallow.Notifications.Application/Channels/Preferences/` and `Wallow.Notifications.Application/Preferences/`
- **Infrastructure services**: `Wallow.Notifications.Infrastructure/Services/` (providers, adapters, SSE service)
- **Module registration**: `Wallow.Notifications.Infrastructure/Extensions/NotificationsModuleExtensions.cs`
- **DbContext**: `Wallow.Notifications.Infrastructure/Persistence/NotificationsDbContext.cs` (schema: `notifications`)
- **Controllers**: `Wallow.Notifications.Api/Controllers/`

## Code Organization Pattern

The Application layer is organized by **channel** (`Email`, `InApp`, `Push`, `Sms`) rather than by entity. Each channel directory contains its own `Commands/`, `Queries/`, `DTOs/`, `Interfaces/`, and `Mappings/` subdirectories. There is also a cross-channel `Preferences` directory for global channel enable/disable.

Event handlers for integration events live in a flat `EventHandlers/` directory at the Application root, not inside channels.

## Integration Events

All consumed events come from `Wallow.Shared.Contracts`. When adding a new event handler:
1. Create a static handler class in `Application/EventHandlers/`
2. Use `public static async Task Handle(EventType message, IMessageBus bus)` signature
3. Wolverine auto-discovers handlers — no registration needed
4. Check user preferences via `INotificationPreferenceChecker` before sending when appropriate

Event handler naming convention: `{EventName}NotificationHandler.cs` for email, `{EventName}InAppHandler.cs` for in-app, `{EventName}SseHandler.cs` for SSE real-time.

## Provider Pattern

Each channel has an abstraction (`IEmailProvider`, `ISmsProvider`, `IPushProvider`) with concrete implementations:
- **Email**: `SmtpEmailProvider` (default), wrapped by `EmailProviderAdapter` implementing `IEmailService`
- **SMS**: `TwilioSmsProvider` (when configured) or `NullSmsProvider` (fallback)
- **Push**: `FcmPushProvider`, `ApnsPushProvider`, `WebPushPushProvider`, `LogPushProvider` — selected by `PushProviderFactory` based on `PushPlatform`
- **In-App real-time**: `SseNotificationService` using `ISseDispatcher` from `Wallow.Shared.Contracts.Realtime`

## Entity Patterns

- All message entities (EmailMessage, SmsMessage, PushMessage) follow `Pending -> Sent/Delivered | Failed` with retry support (`CanRetry(maxRetries)`, `ResetForRetry()`)
- All entities implement `ITenantScoped` for multi-tenant isolation
- Entities use strongly-typed IDs internally; integration events use plain `Guid`
- State changes go through aggregate methods — never set `Status` directly

## Important Conventions

- The `RetryFailedEmailsJob` is registered as scoped but invoked externally (not auto-scheduled within this module)
- Push credentials are encrypted via `IPushCredentialEncryptor` (uses ASP.NET Data Protection)
- `TenantPushConfiguration` stores per-tenant, per-platform credentials — each tenant configures its own push providers
- Email templates use `IEmailTemplateService` (`SimpleEmailTemplateService` implementation)
- SSE handlers dispatch via `ISseDispatcher` for real-time browser updates, separate from persistent in-app notifications

## Testing

```bash
./scripts/run-tests.sh notifications
```
